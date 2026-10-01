using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.Reporting.Domain;
using Pmcs.Modules.Reporting.Persistence;
using Pmcs.Modules.Reporting.Services;

namespace Pmcs.Modules.Reporting.Endpoints;

internal static partial class PortfolioReportingEndpoints
{
    private static async Task<IResult> RetryRunAsync(
        Guid runId, HttpContext httpContext, ReportingRuntimeOptions runtime,
        ReportingExecutionOptions execution, ICurrentActor actor,
        IProjectPermissionService permissions, PortfolioSummaryReportSource source,
        ReportingDbContext db, IClock clock, ITransactionalSideEffectWriter sideEffects,
        IIdempotencyStore idempotency, CancellationToken cancellationToken)
    {
        var gate = await GateAsync(runtime, actor, permissions, CreateOperation, cancellationToken);
        if (gate is not null) return gate;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (!await LockRunAsync(db, runId, actor.TenantId, cancellationToken))
            return Results.NotFound(new { code = "reporting.run.not_found" });
        var run = await db.Runs.SingleAsync(item => item.Id == runId &&
            item.TenantId == actor.TenantId && item.ProjectId == null &&
            item.Scope == ReportDefinitionScope.Portfolio &&
            item.DefinitionCode == PortfolioSummaryReportRuntimeContract.DefinitionCode,
            cancellationToken);
        var pinned = ParsePinned(run);
        if (pinned is null || !await source.CanAccessPinnedAsync(pinned,
                actor.UserId, cancellationToken)) return SourceDenied();
        var replay = await MutationReplayAsync(httpContext, actor.TenantId,
            "reporting.run.retry", runId, idempotency, cancellationToken);
        if (replay.Result is not null) return replay.Result;
        var previous = run.DiagnosticCode;
        if (run.Status != ReportRunStatus.Failed || run.OutputCount > 0 ||
            run.AttemptCount >= execution.MaximumAttempts || !Retryable(previous))
            return Problem(StatusCodes.Status409Conflict, "reporting.run.not_retryable",
                "Portfolio report run cannot be retried.");
        var now = clock.UtcNow;
        run.RetryFailed(now);
        var response = await LoadResponseAsync(run, db, cancellationToken);
        var json = JsonSerializer.Serialize(response, CanonicalJson.SerializerOptions);
        await db.SaveChangesAsync(cancellationToken);
        await sideEffects.WriteAsync(db.Database.GetDbConnection(), transaction.GetDbTransaction(),
            new TransactionalSideEffectBatch(
                new AuditEntry(actor.TenantId, null, actor.UserId,
                    "CertifiedPortfolioReportRunRetried", "ReportRun", run.Id.ToString(),
                    now, new Dictionary<string, object?>
                    {
                        ["previousDiagnosticCode"] = previous,
                        ["attempt"] = run.AttemptCount,
                        ["snapshotPreserved"] = run.SnapshotId.HasValue
                    }, httpContext.TraceIdentifier),
                new OutboxEnvelope(Guid.NewGuid(), actor.TenantId, null,
                    "Reporting.ReportRunRetried", 1, now, json, httpContext.TraceIdentifier),
                new IdempotencyReceipt(actor.TenantId, replay.Key,
                    "reporting.run.retry", replay.Hash, StatusCodes.Status202Accepted,
                    json, now, now.AddDays(7))), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Accepted(response.Links.Self, response);
    }

    private static async Task<IResult> CancelRunAsync(
        Guid runId, HttpContext httpContext, ReportingRuntimeOptions runtime,
        ICurrentActor actor, IProjectPermissionService permissions,
        PortfolioSummaryReportSource source, ReportingDbContext db, IClock clock,
        ITransactionalSideEffectWriter sideEffects, IIdempotencyStore idempotency,
        CancellationToken cancellationToken)
    {
        var gate = await GateAsync(runtime, actor, permissions, CreateOperation, cancellationToken);
        if (gate is not null) return gate;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (!await LockRunAsync(db, runId, actor.TenantId, cancellationToken))
            return Results.NotFound(new { code = "reporting.run.not_found" });
        var run = await db.Runs.SingleAsync(item => item.Id == runId &&
            item.TenantId == actor.TenantId && item.ProjectId == null &&
            item.Scope == ReportDefinitionScope.Portfolio &&
            item.DefinitionCode == PortfolioSummaryReportRuntimeContract.DefinitionCode,
            cancellationToken);
        var pinned = ParsePinned(run);
        if (pinned is null || !await source.CanAccessPinnedAsync(pinned,
                actor.UserId, cancellationToken)) return SourceDenied();
        var replay = await MutationReplayAsync(httpContext, actor.TenantId,
            "reporting.run.cancel", runId, idempotency, cancellationToken);
        if (replay.Result is not null) return replay.Result;
        if (run.Status is ReportRunStatus.Succeeded or ReportRunStatus.Failed or
            ReportRunStatus.Cancelled || run.PipelineStage == ReportPipelineStage.Rendering ||
            run.OutputCount > 0)
            return Problem(StatusCodes.Status409Conflict, "reporting.run.already_final",
                "Portfolio report run can no longer be cancelled.");
        var now = clock.UtcNow;
        run.Cancel(now);
        var response = await LoadResponseAsync(run, db, cancellationToken);
        var json = JsonSerializer.Serialize(response, CanonicalJson.SerializerOptions);
        await db.SaveChangesAsync(cancellationToken);
        await sideEffects.WriteAsync(db.Database.GetDbConnection(), transaction.GetDbTransaction(),
            new TransactionalSideEffectBatch(
                new AuditEntry(actor.TenantId, null, actor.UserId,
                    "CertifiedPortfolioReportRunCancelled", "ReportRun", run.Id.ToString(),
                    now, new Dictionary<string, object?>
                    {
                        ["attempt"] = run.AttemptCount,
                        ["snapshotId"] = run.SnapshotId
                    }, httpContext.TraceIdentifier),
                new OutboxEnvelope(Guid.NewGuid(), actor.TenantId, null,
                    "Reporting.ReportRunCancelled", 1, now, json, httpContext.TraceIdentifier),
                new IdempotencyReceipt(actor.TenantId, replay.Key,
                    "reporting.run.cancel", replay.Hash, StatusCodes.Status200OK,
                    json, now, now.AddDays(7))), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<bool> LockRunAsync(ReportingDbContext db, Guid runId,
        Guid tenantId, CancellationToken cancellationToken)
    {
        var transaction = db.Database.CurrentTransaction ??
            throw new InvalidOperationException("A Portfolio mutation requires a transaction.");
        await using var command = new NpgsqlCommand(
            """
            select id from reporting.report_runs
            where id = @id and tenant_id = @tenant_id and project_id is null
              and scope = 'Portfolio' and definition_code = 'portfolio-summary-certified'
            for update;
            """, (NpgsqlConnection)db.Database.GetDbConnection(),
            (NpgsqlTransaction)transaction.GetDbTransaction());
        command.Parameters.AddWithValue("id", runId);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        return await command.ExecuteScalarAsync(cancellationToken) is Guid;
    }

    private static async Task<(string Key, string Hash, IResult? Result)> MutationReplayAsync(
        HttpContext httpContext, Guid tenantId, string operation, Guid runId,
        IIdempotencyStore idempotency, CancellationToken cancellationToken)
    {
        var key = httpContext.Request.Headers["Idempotency-Key"].ToString().Trim();
        IdempotencyKeyRules.Validate(key);
        var hash = RequestHash.Create(CanonicalJson.Serialize(new { tenantId, runId, operation }));
        var found = await idempotency.FindAsync(tenantId, key, operation, hash,
            cancellationToken);
        return (key, hash, found is null ? null : Results.Content(found.ResponseBody,
            "application/json", Encoding.UTF8, found.StatusCode));
    }

    private static bool Retryable(string? code) => code is
        "reporting.snapshot.transient" or "reporting.renderer.transient" or
        "reporting.renderer.license_unconfigured" or
        "reporting.renderer.license_unapproved" or
        "reporting.renderer.font_missing" or
        "reporting.renderer.font_integrity_failed" or
        "reporting.renderer.configuration_unpinned" or
        "reporting.run.timeout" or "documents.generated.storage_unavailable" or
        "documents.generated.persistence_failed";

    private static IResult SourceDenied() => Problem(StatusCodes.Status403Forbidden,
        "reporting.source_permission.denied", "Pinned Portfolio source is not permitted.");
}
