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
            var approvedImmutableAsset =
                HttpMethods.IsGet(httpContext.Request.Method) &&
                httpContext.Request.Path.StartsWithSegments(
                    "/api/v1/public/login-experience/assets") &&
                httpContext.Response.StatusCode == StatusCodes.Status200OK &&
                string.Equals(
                    headers.CacheControl.ToString(),
                    "public, max-age=31536000, immutable",
                    StringComparison.Ordinal);
            if (httpContext.Request.Path.StartsWithSegments("/api") && !approvedImmutableAsset)
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
