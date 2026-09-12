using Pmcs.BuildingBlocks.Application;

namespace Pmcs.Api.Infrastructure;

internal sealed class ActorAccessMiddleware(RequestDelegate next)
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
