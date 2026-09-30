using System.Text.Json;
using Pmcs.BuildingBlocks.Application;
using Pmcs.BuildingBlocks.Modules;
using Pmcs.Modules.Collaboration;
using Pmcs.Modules.Collaboration.Contracts;
using Pmcs.Modules.Intelligence.Services;
using Pmcs.Modules.Reporting;
using Pmcs.Modules.Reporting.Contracts;
using Pmcs.Modules.Reporting.Domain;

namespace Pmcs.Domain.Tests;

public sealed class IntelligenceToolRegistryTests
{
    private readonly Guid tenant = Guid.NewGuid();
    private readonly Guid actor = Guid.NewGuid();
    private readonly Guid project = Guid.NewGuid();

    [Fact]
    public async Task RegistryRejectsUnknownCrossScopeAndUnexpectedArgumentsBeforeOwnerCall()
    {
        var registry = CreateRegistry(true);
        Assert.Equal("ai.tool.unknown", (await registry.InvokeAsync(tenant, actor, project,
            "reports.sql.execute", Args(project))).Code);
        Assert.Equal("ai.tool.arguments_invalid", (await registry.InvokeAsync(tenant, actor,
            project, "reporting.catalog.list", Args(Guid.NewGuid()))).Code);
        Assert.Equal("ai.tool.arguments_invalid", (await registry.InvokeAsync(tenant, actor,
            project, "reporting.catalog.list", JsonSerializer.SerializeToElement(new
            { projectId = project, sql = "select *" }))).Code);
        Assert.Equal("ai.tool.arguments_invalid", (await registry.InvokeAsync(tenant, actor,
            project, "reporting.runs.get", JsonSerializer.SerializeToElement(new
            { projectId = project, outputId = Guid.NewGuid() }))).Code);
    }

    [Fact]
    public async Task RegistryRequiresLiveSourcePermissionAndDelegatesToOwner()
    {
        var denied = await CreateRegistry(false).InvokeAsync(tenant, actor, project,
            "reporting.catalog.list", Args(project));
        Assert.Equal("ai.tool.permission_denied", denied.Code);
        Assert.Null(denied.Data);

        var permitted = await CreateRegistry(true).InvokeAsync(tenant, actor, project,
            "reporting.catalog.list", Args(project));
        Assert.True(permitted.Allowed);
        Assert.Equal("ai.tool.completed", permitted.Code);
        Assert.Equal(4, CreateRegistry(true).RegisteredTools().Count);
    }

    private IntelligenceToolRegistry CreateRegistry(bool allowed) => new(
        new Catalog(), new Permission(allowed), new Reporting(), new Collaboration());

    private static JsonElement Args(Guid projectId) =>
        JsonSerializer.SerializeToElement(new { projectId });

    private sealed class Catalog : IModuleCatalog
    {
        public IReadOnlyList<ModuleDescriptor> Modules { get; } =
            [new ReportingModule().Descriptor, new CollaborationModule().Descriptor];
        public bool TryGet(string moduleId, out ModuleDescriptor? descriptor)
        {
            descriptor = Modules.SingleOrDefault(item => item.ModuleId == moduleId);
            return descriptor is not null;
        }
        public ModuleDescriptor GetRequired(string moduleId) =>
            Modules.Single(item => item.ModuleId == moduleId);
    }

    private sealed class Permission(bool allowed) : IProjectPermissionService
    {
        public Task<bool> HasTenantPermissionAsync(Guid tenantId, Guid userId, string permission,
            CancellationToken cancellationToken = default) => Task.FromResult(allowed);
        public Task<bool> HasProjectPermissionAsync(Guid tenantId, Guid userId, Guid projectId,
            string permission, CancellationToken cancellationToken = default) => Task.FromResult(allowed);
        public Task<ProjectPermissionScope> GetProjectScopeAsync(Guid tenantId, Guid userId,
            string permission, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<ProjectAccessReadiness> GetProjectAccessReadinessAsync(Guid tenantId,
            Guid projectId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<EffectivePermissionPreview> PreviewProjectPermissionsAsync(Guid tenantId,
            Guid userId, Guid projectId, string? proposedProjectRoleCode = null,
            IReadOnlyCollection<string>? operations = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class Reporting : IReportingReadService
    {
        public Task<IReadOnlyCollection<ReportingCatalogEntry>> ListCatalogAsync(Guid tenantId,
            Guid actorUserId, Guid projectId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<ReportingCatalogEntry>>([]);
        public Task<IReadOnlyCollection<ReportingRunStatusRecord>> ListRunsAsync(Guid tenantId,
            Guid actorUserId, Guid projectId, ReportRunStatus? status = null,
            string? definitionCode = null, int limit = 50,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<ReportingRunStatusRecord>>([]);
        public Task<ReportingRunStatusRecord?> FindRunAsync(Guid tenantId, Guid actorUserId,
            Guid projectId, Guid runId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ReportingRunStatusRecord?>(null);
        public Task<ReportingOutputMetadataRecord?> FindOutputAsync(Guid tenantId,
            Guid actorUserId, Guid projectId, Guid outputId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ReportingOutputMetadataRecord?>(null);
    }

    private sealed class Collaboration : IProjectChatContextReadService
    {
        public Task<IReadOnlyCollection<ProjectChatContextMessage>?> ListRecentAsync(
            Guid tenantId, Guid actorUserId, Guid projectId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<ProjectChatContextMessage>?>(null);
    }
}
