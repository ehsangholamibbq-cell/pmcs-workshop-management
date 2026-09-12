namespace Pmcs.Api.Infrastructure;

internal sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(static state =>
        {
            var httpContext = (HttpContext)state;
            var headers = httpContext.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            if (httpContext.Request.Path.StartsWithSegments("/api"))
            {
                headers["Cache-Control"] = "no-store";
            }
            if (httpContext.Response.StatusCode == StatusCodes.Status401Unauthorized &&
                !headers.ContainsKey("WWW-Authenticate"))
            {
                headers["WWW-Authenticate"] = "Bearer";
            }

            return Task.CompletedTask;
        }, context);

        await next(context);
    }
}
