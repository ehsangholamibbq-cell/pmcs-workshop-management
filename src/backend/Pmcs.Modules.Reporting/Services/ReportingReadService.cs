using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.Reporting.Contracts;
using Pmcs.Modules.Reporting.Domain;
using Pmcs.Modules.Reporting.Persistence;

namespace Pmcs.Modules.Reporting.Services;

internal sealed class ReportingReadService(
    ReportingDbContext dbContext,
    IProjectPermissionService permissionService,
    ReportingRuntimeOptions runtime) : IReportingReadService
{
    private const string CatalogPermission = "reporting.catalog.read";
    private const string OutputPermission = "reporting.output.download";

    public async Task<IReadOnlyCollection<ReportingCatalogEntry>> ListCatalogAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        if (!runtime.Phase1Enabled || !await HasPermissionAsync(
                tenantId,
                actorUserId,
                projectId,
                CatalogPermission,
                cancellationToken))
        {
            return [];
        }

        var permittedDefinitionCodes = await PermittedDefinitionCodesAsync(
            tenantId,
            actorUserId,
            projectId,
            cancellationToken);
        if (permittedDefinitionCodes.Length == 0)
        {
            return [];
        }

        var definitions = await dbContext.Definitions.AsNoTracking()
            .Where(item => permittedDefinitionCodes.Contains(item.Code) &&
                item.Status == ReportDefinitionStatus.Active)
            .OrderBy(item => item.Code)
            .ToArrayAsync(cancellationToken);
        var result = new List<ReportingCatalogEntry>(definitions.Length);
        foreach (var definition in definitions)
        {
            var template = await dbContext.TemplateVersions.AsNoTracking().SingleAsync(
                item => item.Id == definition.CurrentTemplateVersionId && !item.RetiredAt.HasValue,
                cancellationToken);
            result.Add(new ReportingCatalogEntry(
                definition.Code,
                definition.Title,
                definition.Description,
                definition.Scope,
                definition.Classification,
                definition.ParameterSchemaVersion,
                template.Version,
                Deserialize<ReportFormat[]>(definition.SupportedFormatsJson),
                Deserialize<string[]>(definition.RequiredPermissionsJson)));
        }

        return result;
    }

    public async Task<IReadOnlyCollection<ReportingRunStatusRecord>> ListRunsAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid projectId,
        ReportRunStatus? status = null,
        string? definitionCode = null,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (!runtime.Phase1Enabled || !await HasPermissionAsync(
                tenantId,
                actorUserId,
                projectId,
                CatalogPermission,
                cancellationToken))
        {
            return [];
        }

        var permittedDefinitionCodes = await PermittedDefinitionCodesAsync(
            tenantId,
            actorUserId,
            projectId,
            cancellationToken);
        if (permittedDefinitionCodes.Length == 0)
        {
            return [];
        }

        var query = dbContext.Runs.AsNoTracking()
            .Where(item => item.TenantId == tenantId &&
                item.ProjectId == projectId &&
                permittedDefinitionCodes.Contains(item.DefinitionCode));
        if (status.HasValue)
        {
            query = query.Where(item => item.Status == status.Value);
        }
        if (!string.IsNullOrWhiteSpace(definitionCode))
        {
            var normalized = definitionCode.Trim();
            query = query.Where(item => item.DefinitionCode == normalized);
        }

        var runs = await query
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .Take(Math.Clamp(limit, 1, 100))
            .ToArrayAsync(cancellationToken);
        var result = new List<ReportingRunStatusRecord>(runs.Length);
        foreach (var run in runs)
        {
            result.Add(await MapRunAsync(run, cancellationToken));
        }

        return result;
    }

    public async Task<ReportingRunStatusRecord?> FindRunAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid projectId,
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        if (!runtime.Phase1Enabled || !await HasPermissionAsync(
                tenantId,
                actorUserId,
                projectId,
                CatalogPermission,
                cancellationToken))
        {
            return null;
        }

        var run = await dbContext.Runs.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == runId && item.TenantId == tenantId && item.ProjectId == projectId,
            cancellationToken);
        return run is null || !await HasSourcePermissionAsync(
                tenantId,
                actorUserId,
                projectId,
                run.DefinitionCode,
                cancellationToken)
            ? null
            : await MapRunAsync(run, cancellationToken);
    }

    public async Task<ReportingOutputMetadataRecord?> FindOutputAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid projectId,
        Guid outputId,
        CancellationToken cancellationToken = default)
    {
        if (!runtime.OutputAccessEnabled || !await HasPermissionAsync(
                tenantId,
                actorUserId,
                projectId,
                OutputPermission,
                cancellationToken))
        {
            return null;
        }

        var context = await (
            from output in dbContext.Outputs.AsNoTracking()
            join run in dbContext.Runs.AsNoTracking() on output.RunId equals run.Id
            where output.Id == outputId &&
                output.TenantId == tenantId &&
                output.ProjectId == projectId &&
                run.TenantId == tenantId &&
                run.ProjectId == projectId
            select new { Output = output, run.DefinitionCode })
            .SingleOrDefaultAsync(cancellationToken);
        if (context is null || !await HasSourcePermissionAsync(
                tenantId,
                actorUserId,
                projectId,
                context.DefinitionCode,
                cancellationToken))
        {
            return null;
        }

        return MapOutput(context.Output);
    }

    private async Task<ReportingRunStatusRecord> MapRunAsync(
        ReportRun run,
        CancellationToken cancellationToken)
    {
        var snapshot = run.SnapshotId.HasValue
            ? await dbContext.Snapshots.AsNoTracking().SingleOrDefaultAsync(
                item => item.Id == run.SnapshotId.Value &&
                    item.TenantId == run.TenantId &&
                    item.ProjectId == run.ProjectId,
                cancellationToken)
            : null;
        var outputs = await dbContext.Outputs.AsNoTracking()
            .Where(item => item.RunId == run.Id &&
                item.TenantId == run.TenantId &&
                item.ProjectId == run.ProjectId)
            .OrderBy(item => item.Format)
            .Select(item => new ReportingOutputMetadataRecord(
                item.Id,
                item.RunId,
                item.ProjectId,
                item.Format,
                item.FileName,
                item.ContentType,
                item.SizeBytes,
                item.Sha256,
                item.VerificationCode,
                item.Classification,
                item.ArchiveState))
            .ToArrayAsync(cancellationToken);
        return new ReportingRunStatusRecord(
            run.Id,
            run.ProjectId,
            run.DefinitionCode,
            run.TemplateVersion,
            run.Status,
            run.PipelineStage,
            snapshot?.DataStatus,
            run.AsOfUtc,
            Deserialize<ReportFormat[]>(run.RequestedFormatsJson),
            run.AttemptCount,
            run.CreatedAt,
            run.StartedAt,
            run.CompletedAt,
            run.DiagnosticCode,
            snapshot?.Sha256,
            outputs);
    }

    private async Task<string[]> PermittedDefinitionCodesAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var permitted = new List<string>(ReportDefinitionRuntimePolicy.SupportedDefinitionCodes.Length);
        var permissionDecisions = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var definitionCode in ReportDefinitionRuntimePolicy.SupportedDefinitionCodes)
        {
            var allowed = true;
            foreach (var sourcePermission in
                     ReportDefinitionRuntimePolicy.RequireSourcePermissions(definitionCode))
            {
                if (!permissionDecisions.TryGetValue(sourcePermission, out var permissionAllowed))
                {
                    permissionAllowed = await HasPermissionAsync(
                        tenantId,
                        actorUserId,
                        projectId,
                        sourcePermission,
                        cancellationToken);
                    permissionDecisions.Add(sourcePermission, permissionAllowed);
                }
                allowed &= permissionAllowed;
            }
            if (allowed)
            {
                permitted.Add(definitionCode);
            }
        }
        return permitted.ToArray();
    }

    private async Task<bool> HasSourcePermissionAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid projectId,
        string definitionCode,
        CancellationToken cancellationToken)
    {
        if (!ReportDefinitionRuntimePolicy.TryGetSourcePermissions(
                definitionCode,
                out var permissions))
        {
            return false;
        }

        foreach (var permission in permissions)
        {
            if (!await HasPermissionAsync(
                    tenantId,
                    actorUserId,
                    projectId,
                    permission,
                    cancellationToken))
            {
                return false;
            }
        }
        return true;
    }

    private Task<bool> HasPermissionAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid projectId,
        string permission,
        CancellationToken cancellationToken) =>
        permissionService.HasProjectPermissionAsync(
            tenantId,
            actorUserId,
            projectId,
            permission,
            cancellationToken);

    private static ReportingOutputMetadataRecord MapOutput(ReportOutput output) => new(
        output.Id,
        output.RunId,
        output.ProjectId,
        output.Format,
        output.FileName,
        output.ContentType,
        output.SizeBytes,
        output.Sha256,
        output.VerificationCode,
        output.Classification,
        output.ArchiveState);

    private static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, CanonicalJson.SerializerOptions)
        ?? throw new InvalidOperationException("Stored reporting JSON is invalid.");
}
