using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Pmcs.BuildingBlocks.Application;
using Pmcs.BuildingBlocks.Testing;
using Pmcs.Modules.QualityAssurance.Contracts;

namespace Pmcs.Modules.QualityAssurance.Endpoints;

internal static class QualityAssuranceEndpoints
{
    private const string GatewayPermission = "qa.gateway.use";

    public static void MapQualityAssuranceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var qa = endpoints.MapGroup("/api/qa/v1").WithTags("PMCS QA Gateway");
        qa.MapGet("/status", GetStatusAsync);
        qa.MapGet("/diagnostics", GetDiagnosticsAsync);
        qa.MapGet("/permissions/preview", PreviewPermissionsAsync);
    }

    private static async Task<IResult> GetStatusAsync(
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissions,
        IAuditTrail audit,
        IClock clock,
        QualityAssuranceRuntimeOptions options,
        CancellationToken cancellationToken)
    {
        var denied = await RequireGatewayPermissionAsync(actor, permissions, cancellationToken);
        if (denied is not null)
        {
            return denied;
        }

        var response = new
        {
            contractVersion = 1,
            enabled = true,
            environment = options.EnvironmentName,
            database = options.DatabaseName,
            release = new
            {
                commit = options.ReleaseCommit,
                version = options.ReleaseVersion,
                builtAt = options.ReleaseBuiltAt
            },
            actor = new
            {
                tenantId = actor.TenantId,
                userId = actor.UserId,
                authentication = "qa-key-real-actor"
            },
            seed = new
            {
                tenantId = PmcsTestDataSet.TenantId,
                projectId = PmcsTestDataSet.ProjectId,
                rootLocationId = PmcsTestDataSet.RootLocationId,
                actorCount = PmcsTestDataSet.Actors.Count
            },
            utc = clock.UtcNow
        };

        await WriteAuditAsync(
            httpContext,
            actor,
            audit,
            clock,
            "QaGateway.StatusRead",
            "status",
            new Dictionary<string, object?>
            {
                ["environment"] = options.EnvironmentName,
                ["database"] = options.DatabaseName
            },
            cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> GetDiagnosticsAsync(
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissions,
        IQaDiagnosticsService diagnostics,
        IAuditTrail audit,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var denied = await RequireGatewayPermissionAsync(actor, permissions, cancellationToken);
        if (denied is not null)
        {
            return denied;
        }

        var response = await diagnostics.ReadAsync(cancellationToken);
        await WriteAuditAsync(
            httpContext,
            actor,
            audit,
            clock,
            "QaGateway.DiagnosticsRead",
            "diagnostics",
            new Dictionary<string, object?>
            {
                ["overallHealth"] = response.OverallHealth,
                ["probeCount"] = response.Probes.Count
            },
            cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> PreviewPermissionsAsync(
        Guid projectId,
        Guid userId,
        string[]? operation,
        string? proposedProjectRoleCode,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissions,
        IAuditTrail audit,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var denied = await RequireGatewayPermissionAsync(actor, permissions, cancellationToken);
        if (denied is not null)
        {
            return denied;
        }

        var requestedOperations = operation?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var response = await permissions.PreviewProjectPermissionsAsync(
            actor.TenantId,
            userId,
            projectId,
            proposedProjectRoleCode,
            requestedOperations,
            cancellationToken);

        await WriteAuditAsync(
            httpContext,
            actor,
            audit,
            clock,
            "QaGateway.PermissionPreviewRead",
            projectId.ToString(),
            new Dictionary<string, object?>
            {
                ["targetUserId"] = userId,
                ["projectId"] = projectId,
                ["operationCount"] = requestedOperations?.Length ?? 0,
                ["proposedProjectRoleCode"] = proposedProjectRoleCode
            },
            cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult?> RequireGatewayPermissionAsync(
        ICurrentActor actor,
        IProjectPermissionService permissions,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        return await permissions.HasTenantPermissionAsync(
            actor.TenantId,
            actor.UserId,
            GatewayPermission,
            cancellationToken)
            ? null
            : Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    private static Task WriteAuditAsync(
        HttpContext httpContext,
        ICurrentActor actor,
        IAuditTrail audit,
        IClock clock,
        string eventType,
        string resourceId,
        IReadOnlyDictionary<string, object?> data,
        CancellationToken cancellationToken) =>
        audit.WriteAsync(
            new AuditEntry(
                actor.TenantId,
                null,
                actor.UserId,
                eventType,
                "QaGateway",
                resourceId,
                clock.UtcNow,
                data,
                httpContext.TraceIdentifier),
            cancellationToken);
}
