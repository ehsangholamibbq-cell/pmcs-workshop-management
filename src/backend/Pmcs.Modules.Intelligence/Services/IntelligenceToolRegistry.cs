using System.Text.Json;
using Pmcs.BuildingBlocks.Application;
using Pmcs.BuildingBlocks.Modules;
using Pmcs.Modules.Collaboration.Contracts;
using Pmcs.Modules.Reporting.Contracts;

namespace Pmcs.Modules.Intelligence.Services;

internal sealed record IntelligenceToolResult(bool Allowed, string Code, object? Data)
{
    internal static IntelligenceToolResult Denied(string code) => new(false, code, null);
}

/// <summary>Only owner-module read contracts may be called; the model never supplies authority.</summary>
internal sealed class IntelligenceToolRegistry(
    IModuleCatalog modules, IProjectPermissionService permissions,
    IReportingReadService reporting, IProjectChatContextReadService collaboration)
{
    private static readonly HashSet<string> ImplementedTools = new(StringComparer.Ordinal)
    {
        "reporting.catalog.list", "reporting.runs.get", "reporting.outputs.describe",
        "collaboration.messages.list"
    };

    internal IReadOnlyCollection<ToolManifest> RegisteredTools() =>
        modules.Modules.SelectMany(module => module.Tools)
            .Where(tool => tool.AccessMode == ToolAccessMode.ReadOnly &&
                ImplementedTools.Contains(tool.Id))
            .OrderBy(tool => tool.Id, StringComparer.Ordinal).ToArray();

    internal async Task<IntelligenceToolResult> InvokeAsync(
        Guid tenantId, Guid actorUserId, Guid projectId,
        string toolId, JsonElement arguments, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || actorUserId == Guid.Empty || projectId == Guid.Empty)
            return IntelligenceToolResult.Denied("ai.tool.scope_denied");

        var matches = RegisteredTools().Where(tool => tool.Id == toolId).Take(2).ToArray();
        if (matches.Length != 1)
            return IntelligenceToolResult.Denied("ai.tool.unknown");
        var tool = matches[0];
        var itemProperty = toolId switch
        {
            "reporting.runs.get" => "runId",
            "reporting.outputs.describe" => "outputId",
            _ => null
        };
        if (!TryReadArguments(arguments, projectId, itemProperty, out var itemId))
            return IntelligenceToolResult.Denied("ai.tool.arguments_invalid");
        if (!await permissions.HasProjectPermissionAsync(tenantId, actorUserId,
                projectId, "insights.generate", cancellationToken) ||
            !await permissions.HasProjectPermissionAsync(tenantId, actorUserId,
                projectId, tool.Permission, cancellationToken))
            return IntelligenceToolResult.Denied("ai.tool.permission_denied");

        object? data = toolId switch
        {
            "reporting.catalog.list" => await CatalogAsync(),
            "reporting.runs.get" => await RunAsync(),
            "reporting.outputs.describe" => await OutputAsync(),
            "collaboration.messages.list" => await MessagesAsync(),
            _ => null
        };
        return data is null
            ? IntelligenceToolResult.Denied("ai.tool.source_unavailable")
            : new IntelligenceToolResult(true, "ai.tool.completed", data);

        async Task<object> CatalogAsync()
        {
            var definitions = await reporting.ListCatalogAsync(tenantId, actorUserId,
                projectId, cancellationToken);
            return new { definitions = definitions.Take(50).Select(item => new
            {
                item.Code, item.Title, item.Description, classification = item.Classification.ToString()
            }).ToArray() };
        }

        async Task<object?> RunAsync()
        {
            var run = await reporting.FindRunAsync(tenantId, actorUserId,
                projectId, itemId, cancellationToken);
            return run is null ? null : new
            {
                run.Id, run.ProjectId, run.DefinitionCode,
                status = run.Status.ToString(), run.AsOfUtc,
                run.CompletedAt, run.SnapshotHash
            };
        }

        async Task<object?> OutputAsync()
        {
            var output = await reporting.FindOutputAsync(tenantId, actorUserId,
                projectId, itemId, cancellationToken);
            return output is null ? null : new
            {
                output.Id, output.RunId, output.ProjectId,
                format = output.Format.ToString(), output.Sha256,
                classification = output.Classification.ToString(),
                archiveState = output.ArchiveState.ToString()
            };
        }

        async Task<object?> MessagesAsync()
        {
            var messages = await collaboration.ListRecentAsync(tenantId, actorUserId,
                projectId, cancellationToken);
            return messages is null ? null : new { messages = messages.Select(item => new
            {
                item.Id, item.Sequence, item.AuthorUserId, item.Body, item.CreatedAt
            }).ToArray() };
        }
    }

    internal static bool TryReadArguments(JsonElement arguments, Guid projectId,
        string? itemProperty, out Guid itemId)
    {
        itemId = Guid.Empty;
        if (arguments.ValueKind != JsonValueKind.Object) return false;
        var count = 0;
        var projectFound = false;
        var itemFound = false;
        foreach (var property in arguments.EnumerateObject())
        {
            count++;
            if (count > (itemProperty is null ? 1 : 2) ||
                property.Value.ValueKind != JsonValueKind.String)
                return false;
            if (property.NameEquals("projectId") && !projectFound)
            {
                projectFound = property.Value.TryGetGuid(out var supplied) && supplied == projectId;
                if (!projectFound) return false;
            }
            else if (itemProperty is not null && !itemFound &&
                property.NameEquals(itemProperty))
            {
                itemFound = property.Value.TryGetGuid(out itemId) && itemId != Guid.Empty;
                if (!itemFound) return false;
            }
            else return false;
        }
        return projectFound && (itemProperty is null || itemFound);
    }
}
