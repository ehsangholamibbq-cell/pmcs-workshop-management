using System.Globalization;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Pmcs.Api.Infrastructure;
using Pmcs.BuildingBlocks.Application;
using Pmcs.BuildingBlocks.Modules;
using Pmcs.BuildingBlocks.Web;
using Pmcs.Modules.FieldOperations;
using Pmcs.Modules.IdentityAccess;
using Pmcs.Modules.Platform;
using Pmcs.Modules.ProjectIntelligence;
using Pmcs.Modules.Projects;
using Pmcs.Modules.Evidence;
using Pmcs.Modules.ActionControl;
using Pmcs.Modules.Finance;
using Pmcs.Modules.Commercial;
using Pmcs.Modules.Intelligence;
using Pmcs.Modules.Planning;
using Pmcs.Modules.TechnicalOffice;
using Pmcs.Modules.QualitySafety;
using Pmcs.Modules.Sync;

var builder = WebApplication.CreateBuilder(args);
var releaseIdentity = ReleaseIdentity.FromAssembly(typeof(Program).Assembly);
ProductionConfigurationValidator.Validate(builder.Environment, builder.Configuration, releaseIdentity);

const string authenticationScheme = "Pmcs";
const long maximumRequestBodySize = 30L * 1024L * 1024L;
var generalPermitLimit = ReadPositiveInt(builder.Configuration, "RateLimiting:GeneralPermitLimit", 300);
var generalWindowSeconds = ReadPositiveInt(builder.Configuration, "RateLimiting:GeneralWindowSeconds", 60);
var insightPermitLimit = ReadPositiveInt(builder.Configuration, "RateLimiting:InsightPermitLimit", 5);
var insightWindowSeconds = ReadPositiveInt(builder.Configuration, "RateLimiting:InsightWindowSeconds", 3600);
var identityPermitLimit = ReadPositiveInt(builder.Configuration, "RateLimiting:IdentityPermitLimit", 30);
var identityWindowSeconds = ReadPositiveInt(builder.Configuration, "RateLimiting:IdentityWindowSeconds", 60);

IModule[] modules =
[
    new PlatformModule(),
    new IdentityAccessModule(),
    new ProjectsModule(),
    new FieldOperationsModule(),
    new SyncModule(),
    new PlanningModule(),
    new TechnicalOfficeModule(),
    new EvidenceModule(),
    new ActionControlModule(),
    new CommercialModule(),
    new FinanceModule(),
    new QualitySafetyModule(),
    new ProjectIntelligenceModule(),
    new IntelligenceModule()
];

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentActor, HttpCurrentActor>();
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = authenticationScheme;
        options.DefaultChallengeScheme = authenticationScheme;
    })
    .AddPolicyScheme(authenticationScheme, authenticationScheme, options =>
    {
        options.ForwardDefaultSelector = context =>
            !builder.Environment.IsDevelopment() ||
            context.Request.Headers["Authorization"].ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? JwtBearerDefaults.AuthenticationScheme
                : DevelopmentIdentityAuthenticationHandler.SchemeName;
    })
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Authentication:Authority"];
        var metadataAddress = builder.Configuration["Authentication:MetadataAddress"];
        if (!string.IsNullOrWhiteSpace(metadataAddress))
        {
            options.MetadataAddress = metadataAddress;
        }
        options.Audience = builder.Configuration["Authentication:Audience"];
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2),
            NameClaimType = "name",
            RoleClaimType = "role"
        };
    })
    .AddScheme<AuthenticationSchemeOptions, DevelopmentIdentityAuthenticationHandler>(
        DevelopmentIdentityAuthenticationHandler.SchemeName,
        _ => { });
builder.Services.AddAuthorizationBuilder().SetFallbackPolicy(
    new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Services.AddCors(options => options.AddPolicy("PmcsWeb", policy =>
    policy
        .WithOrigins([.. WebOriginConfiguration.Read(builder.Configuration)])
        .AllowAnyHeader()
        .AllowAnyMethod()));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            RateLimitPartitionKey(context),
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = generalPermitLimit,
                QueueLimit = 0,
                Window = TimeSpan.FromSeconds(generalWindowSeconds)
            }));
    options.AddPolicy(ApiRateLimitPolicies.InsightGeneration, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            RateLimitPartitionKey(context),
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = insightPermitLimit,
                QueueLimit = 0,
                Window = TimeSpan.FromSeconds(insightWindowSeconds)
            }));
    options.AddPolicy(ApiRateLimitPolicies.IdentityAdministration, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            RateLimitPartitionKey(context),
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = identityPermitLimit,
                QueueLimit = 0,
                Window = TimeSpan.FromSeconds(identityWindowSeconds)
            }));
    options.OnRejected = async (rejection, cancellationToken) =>
    {
        var retryAfterSeconds = rejection.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
            ? Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
            : (int?)null;
        if (retryAfterSeconds.HasValue)
        {
            rejection.HttpContext.Response.Headers["Retry-After"] =
                retryAfterSeconds.Value.ToString(CultureInfo.InvariantCulture);
        }

        rejection.HttpContext.Response.ContentType = "application/problem+json";
        await rejection.HttpContext.Response.WriteAsJsonAsync(new
        {
            status = StatusCodes.Status429TooManyRequests,
            title = "Too many requests.",
            code = "rate_limit.exceeded",
            correlationId = rejection.HttpContext.TraceIdentifier,
            retryAfterSeconds
        }, cancellationToken);
    };
});
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = maximumRequestBodySize);
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();

foreach (var module in modules)
{
    module.AddServices(builder.Services, builder.Configuration);
}

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestTelemetryMiddleware>();
app.UseExceptionHandler();
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseRouting();
app.UseCors("PmcsWeb");
app.UseAuthentication();
app.UseMiddleware<ActorAccessMiddleware>();
app.UseMiddleware<ProjectLifecycleMiddleware>();
app.UseRateLimiter();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous().DisableRateLimiting();
}

var readinessOptions = new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
    ResponseWriter = HealthResponseWriter.WriteAsync
};
app.MapHealthChecks("/health", readinessOptions).AllowAnonymous().DisableRateLimiting();
app.MapHealthChecks("/health/ready", readinessOptions).AllowAnonymous().DisableRateLimiting();
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = HealthResponseWriter.WriteAsync
}).AllowAnonymous().DisableRateLimiting();
app.MapGet("/api/v1/release", () => Results.Ok(releaseIdentity))
    .AllowAnonymous()
    .DisableRateLimiting();

foreach (var module in modules)
{
    module.MapEndpoints(app);
}

app.Run();

static int ReadPositiveInt(IConfiguration configuration, string key, int fallback)
{
    var configured = configuration[key];
    if (string.IsNullOrWhiteSpace(configured))
    {
        return fallback;
    }

    return int.TryParse(configured, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value > 0
        ? value
        : throw new InvalidOperationException($"{key} must be a positive integer.");
}

static string RateLimitPartitionKey(HttpContext context)
{
    var tenant = context.User.FindFirst("tenant_id")?.Value;
    var user = context.User.FindFirst("sub")?.Value;
    if (Guid.TryParse(tenant, out var tenantId) && Guid.TryParse(user, out var userId))
    {
        return $"actor:{tenantId:N}:{userId:N}";
    }

    return $"ip:{context.Connection.RemoteIpAddress}";
}

public partial class Program;
