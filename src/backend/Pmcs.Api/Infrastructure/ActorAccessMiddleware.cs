using Pmcs.BuildingBlocks.Application;

namespace Pmcs.Api.Infrastructure;

internal sealed partial class ActorAccessMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        ICurrentActor actor,
        IActorAccessValidator accessValidator)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        if (!actor.IsAuthenticated || !actor.TokenIssuedAt.HasValue)
        {
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger<ActorAccessMiddleware>();
            LogInvalidActorClaims(logger, actor.TenantId != Guid.Empty, actor.UserId != Guid.Empty, actor.TokenIssuedAt.HasValue);
            await WriteProblemAsync(context, StatusCodes.Status401Unauthorized, "authentication.claims.invalid");
            return;
        }

        if (!await accessValidator.HasAccessAsync(
                actor.TenantId,
                actor.UserId,
                actor.TokenIssuedAt.Value,
                context.RequestAborted))
        {
            await WriteProblemAsync(context, StatusCodes.Status403Forbidden, "authentication.access.denied");
            return;
        }

        await next(context);
    }

    [LoggerMessage(4011, LogLevel.Warning, "Authenticated OIDC principal has invalid actor claims. Tenant={HasTenantId}; User={HasUserId}; IssuedAt={HasIssuedAt}.")]
    private static partial void LogInvalidActorClaims(ILogger logger, bool hasTenantId, bool hasUserId, bool hasIssuedAt);

    private static Task WriteProblemAsync(HttpContext context, int statusCode, string code)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        return context.Response.WriteAsJsonAsync(new
        {
            status = statusCode,
            title = statusCode == StatusCodes.Status401Unauthorized
                ? "Authentication token is invalid."
                : "Account access is denied.",
            code,
            correlationId = context.TraceIdentifier
        }, context.RequestAborted);
    }
}
