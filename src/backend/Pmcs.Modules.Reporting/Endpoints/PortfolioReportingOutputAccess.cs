using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.Documents.Contracts;
using Pmcs.Modules.Documents.Domain;
using Pmcs.Modules.Reporting.Domain;
using Pmcs.Modules.Reporting.Persistence;
using Pmcs.Modules.Reporting.Rendering;
using Pmcs.Modules.Reporting.Services;

namespace Pmcs.Modules.Reporting.Endpoints;

internal static partial class PortfolioReportingEndpoints
{
    private static async Task<IResult> DownloadOutputAsync(
        Guid outputId, HttpContext httpContext, ReportingRuntimeOptions runtime,
        ICurrentActor actor, IProjectPermissionService permissions,
        PortfolioSummaryReportSource source, ReportingDbContext db,
        ISharedDocumentDirectory documents, IAuditTrail audit, IClock clock,
        CancellationToken cancellationToken)
    {
        var gate = await OutputGateAsync(runtime, actor, permissions, cancellationToken);
        if (gate is not null) return gate;
        var context = await LoadPortfolioOutputAsync(db, actor.TenantId, outputId,
            cancellationToken);
        if (context is null) return Results.NotFound(new { code = "reporting.output.not_found" });
        var pinned = ParsePinned(context.Run);
        if (pinned is null || !await source.CanAccessPinnedAsync(pinned,
                actor.UserId, cancellationToken)) return SourceDenied();
        var content = await ReadVerifiedPortfolioOutputAsync(context, documents,
            cancellationToken);
        if (content is null)
        {
            await AuditPortfolioOutputAsync(context, "CertifiedPortfolioReportOutputIntegrityFailed",
                "Download", actor, audit, clock, httpContext.TraceIdentifier,
                cancellationToken);
            return OutputIntegrityProblem();
        }
        if (!await source.CanAccessPinnedAsync(pinned, actor.UserId, cancellationToken))
            return SourceDenied();
        await AuditPortfolioOutputAsync(context, "CertifiedPortfolioReportOutputDownloaded",
            "Download", actor, audit, clock, httpContext.TraceIdentifier,
            cancellationToken);
        httpContext.Response.Headers.CacheControl = "private, no-store";
        httpContext.Response.Headers.ETag = $"\"sha256-{context.Output.Sha256}\"";
        return Results.File(content.Bytes, context.Output.ContentType,
            context.Output.FileName, enableRangeProcessing: false);
    }

    private static async Task<IResult> VerifyOutputAsync(
        Guid outputId, HttpContext httpContext, ReportingRuntimeOptions runtime,
        ICurrentActor actor, IProjectPermissionService permissions,
        PortfolioSummaryReportSource source, ReportingDbContext db,
        ISharedDocumentDirectory documents, IAuditTrail audit, IClock clock,
        CancellationToken cancellationToken)
    {
        var gate = await OutputGateAsync(runtime, actor, permissions, cancellationToken);
        if (gate is not null) return gate;
        var context = await LoadPortfolioOutputAsync(db, actor.TenantId, outputId,
            cancellationToken);
        if (context is null) return Results.NotFound(new { code = "reporting.output.not_found" });
        var pinned = ParsePinned(context.Run);
        if (pinned is null || !await source.CanAccessPinnedAsync(pinned,
                actor.UserId, cancellationToken)) return SourceDenied();
        var content = await ReadVerifiedPortfolioOutputAsync(context, documents,
            cancellationToken);
        if (content is null)
        {
            await AuditPortfolioOutputAsync(context, "CertifiedPortfolioReportOutputIntegrityFailed",
                "Verify", actor, audit, clock, httpContext.TraceIdentifier,
                cancellationToken);
            return OutputIntegrityProblem();
        }
        if (!await source.CanAccessPinnedAsync(pinned, actor.UserId, cancellationToken))
            return SourceDenied();
        await AuditPortfolioOutputAsync(context, "CertifiedPortfolioReportOutputVerified",
            "Verify", actor, audit, clock, httpContext.TraceIdentifier,
            cancellationToken);
        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new ReportOutputVerificationResponse(
            context.Output.VerificationCode, "Valid", context.Run.DefinitionCode,
            context.Run.TemplateVersion, context.Run.AsOfUtc,
            context.Output.Sha256,
            context.Output.ArchiveState == ReportOutputArchiveState.Archived));
    }

    private static async Task<IResult?> OutputGateAsync(ReportingRuntimeOptions runtime,
        ICurrentActor actor, IProjectPermissionService permissions,
        CancellationToken cancellationToken)
    {
        if (!runtime.Phase1Enabled || !runtime.OutputAccessEnabled)
            return Results.NotFound();
        if (!actor.IsAuthenticated) return Results.Unauthorized();
        if (!await permissions.HasTenantPermissionAsync(actor.TenantId, actor.UserId,
                "reporting.output.download", cancellationToken) ||
            !await permissions.HasTenantPermissionAsync(actor.TenantId, actor.UserId,
                "portfolio.read", cancellationToken))
            return Problem(StatusCodes.Status403Forbidden, "reporting.permission.denied",
                "Portfolio report output access is not permitted.");
        return null;
    }

    private static async Task<PortfolioOutputContext?> LoadPortfolioOutputAsync(
        ReportingDbContext db, Guid tenantId, Guid outputId,
        CancellationToken cancellationToken)
    {
        var pair = await (
            from output in db.Outputs.AsNoTracking()
            join run in db.Runs.AsNoTracking() on output.RunId equals run.Id
            where output.Id == outputId && output.TenantId == tenantId &&
                output.ProjectId == null && output.Scope == ReportDefinitionScope.Portfolio &&
                run.TenantId == tenantId && run.ProjectId == null &&
                run.Scope == ReportDefinitionScope.Portfolio &&
                run.DefinitionCode == PortfolioSummaryReportRuntimeContract.DefinitionCode &&
                run.Status == ReportRunStatus.Succeeded && run.SnapshotId == output.SnapshotId
            select new { Output = output, Run = run }).SingleOrDefaultAsync(cancellationToken);
        if (pair is null) return null;
        var snapshot = await db.Snapshots.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == pair.Output.SnapshotId && item.RunId == pair.Run.Id &&
            item.TenantId == tenantId && item.ProjectId == null &&
            item.Scope == ReportDefinitionScope.Portfolio, cancellationToken);
        var template = await db.TemplateVersions.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == pair.Output.TemplateVersionId &&
            item.Id == pair.Run.TemplateVersionId &&
            item.DefinitionId == pair.Run.DefinitionId &&
            item.Version == pair.Run.TemplateVersion, cancellationToken);
        return new PortfolioOutputContext(pair.Output, pair.Run, snapshot, template);
    }

    private static async Task<ReleasedDocumentContent?> ReadVerifiedPortfolioOutputAsync(
        PortfolioOutputContext context, ISharedDocumentDirectory documents,
        CancellationToken cancellationToken)
    {
        var output = context.Output;
        var run = context.Run;
        var snapshot = context.Snapshot;
        var template = context.Template;
        if (snapshot is null || template is null ||
            snapshot.SourceCutoffUtc != run.AsOfUtc ||
            snapshot.Classification < ReportClassification.Confidential ||
            output.Classification != snapshot.Classification ||
            output.Id != ReportArtifactIdentity.OutputId(run.Id, output.Format) ||
            output.GeneratedDocumentId != ReportArtifactIdentity.DocumentId(output.Id) ||
            output.RetentionPolicy != DocumentRetentionPolicy.LongTerm.ToString())
            return null;
        try
        {
            if (CanonicalJson.Sha256(CanonicalJson.Normalize(
                    JsonSerializer.Deserialize<JsonElement>(snapshot.PayloadJson))) != snapshot.Sha256 ||
                CanonicalJson.Sha256(CanonicalJson.Normalize(
                    JsonSerializer.Deserialize<JsonElement>(snapshot.SourceManifestJson))) !=
                    snapshot.SourceManifestSha256)
                return null;
            var semantic = PortfolioSummaryReportRenderingContract.Parse(
                snapshot.PayloadJson, snapshot.SourceManifestJson);
            if (semantic.TenantId != run.TenantId || semantic.AsOfUtc != run.AsOfUtc ||
                semantic.DataStatus != snapshot.DataStatus)
                return null;
            var manifest = ReportArtifactIdentity.CreateManifest(run, snapshot,
                template, output.Format);
            var content = await documents.ReadReleasedAsync(output.TenantId,
                output.GeneratedDocumentId, DocumentOwnerType.TenantReportOutput,
                output.Id, cancellationToken);
            if (content is null) return null;
            var expectedClassification = output.Classification switch
            {
                ReportClassification.Confidential => DocumentClassification.Confidential,
                ReportClassification.Restricted => DocumentClassification.Restricted,
                _ => (DocumentClassification?)null
            };
            var asset = content.Document;
            var valid = asset.Id == output.GeneratedDocumentId &&
                asset.TenantId == output.TenantId && asset.ProjectId is null &&
                asset.OwnerType == DocumentOwnerType.TenantReportOutput &&
                asset.OwnerId == output.Id && asset.VersionNumber == 1 &&
                asset.OriginalFileName == output.FileName &&
                asset.ContentType == output.ContentType &&
                asset.SizeBytes == output.SizeBytes && asset.Sha256 == output.Sha256 &&
                asset.Classification == expectedClassification &&
                asset.RetentionPolicy == DocumentRetentionPolicy.LongTerm &&
                manifest.Sha256 == output.ManifestSha256 &&
                manifest.VerificationCode == output.VerificationCode &&
                manifest.ContentType == output.ContentType &&
                output.FileName == ReportArtifactIdentity.FileName(semantic, output.Format) &&
                content.Bytes.LongLength == output.SizeBytes &&
                ReportArtifactIdentity.Sha256(content.Bytes) == output.Sha256;
            return valid ? content : null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return null;
        }
    }

    private static Task AuditPortfolioOutputAsync(PortfolioOutputContext context,
        string eventType, string operation, ICurrentActor actor, IAuditTrail audit,
        IClock clock, string correlationId, CancellationToken cancellationToken) =>
        audit.WriteAsync(new AuditEntry(actor.TenantId, null, actor.UserId,
            eventType, "ReportOutput", context.Output.Id.ToString(), clock.UtcNow,
            new Dictionary<string, object?>
            {
                ["runId"] = context.Run.Id,
                ["operation"] = operation,
                ["format"] = context.Output.Format.ToString(),
                ["sha256"] = context.Output.Sha256,
                ["manifestSha256"] = context.Output.ManifestSha256,
                ["result"] = eventType.EndsWith("IntegrityFailed", StringComparison.Ordinal)
                    ? "Invalid" : "Valid"
            }, correlationId), cancellationToken);

    private static IResult OutputIntegrityProblem() => Problem(
        StatusCodes.Status502BadGateway, "reporting.output.integrity_failed",
        "Portfolio report output integrity verification failed.");

    private sealed record PortfolioOutputContext(ReportOutput Output, ReportRun Run,
        ReportSnapshot? Snapshot, ReportTemplateVersionRecord? Template);
}
