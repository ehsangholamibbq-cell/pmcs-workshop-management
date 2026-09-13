using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.Projects.Domain;

namespace Pmcs.Api.Infrastructure;

internal sealed class ProjectLifecycleMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        ICurrentActor actor,
        IProjectDirectory projects)
    {
        if (!IsOperationalMutation(context, out var projectId) || !actor.IsAuthenticated)
        {
            await next(context);
            return;
        }

        var profile = await projects.FindProfileAsync(actor.TenantId, projectId, context.RequestAborted);
        if (profile is null)
        {
            await WriteProblemAsync(context, StatusCodes.Status404NotFound, "project.not_found", null);
            return;
        }

        if (profile.Status != ProjectStatus.Active)
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status409Conflict,
                "project.not_operational",
                profile.Status.ToString());
            return;
        }

        await next(context);
    }

    internal static bool IsOperationalMutation(HttpContext context, out Guid projectId)
    {
        projectId = Guid.Empty;
        if (HttpMethods.IsGet(context.Request.Method) ||
            HttpMethods.IsHead(context.Request.Method) ||
            HttpMethods.IsOptions(context.Request.Method) ||
            !context.Request.RouteValues.TryGetValue("projectId", out var rawProjectId) ||
            !Guid.TryParse(Convert.ToString(rawProjectId, System.Globalization.CultureInfo.InvariantCulture), out projectId))
        {
            return false;
        }

        var segments = context.Request.Path.Value?
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
        if (segments.Length < 4 ||
            !string.Equals(segments[0], "api", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(segments[1], "v1", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(segments[2], "projects", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (segments.Length == 4)
        {
            return false;
        }

        return !string.Equals(segments[4], "activate", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(segments[4], "setup", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(segments[4], "calendar", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(segments[4], "planning-mode", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(segments[4], "locations", StringComparison.OrdinalIgnoreCase);
    }

    private static Task WriteProblemAsync(HttpContext context, int statusCode, string code, string? projectStatus)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        return context.Response.WriteAsJsonAsync(new
        {
            status = statusCode,
            title = statusCode == StatusCodes.Status404NotFound
                ? "Project was not found."
                : "Project is not active for operational changes.",
            code,
            projectStatus,
            correlationId = context.TraceIdentifier
        }, context.RequestAborted);
    }
}
