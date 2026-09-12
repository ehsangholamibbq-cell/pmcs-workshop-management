using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pmcs.BuildingBlocks.Application;
using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Api.Infrastructure;

internal sealed partial class ApiExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title, code) = exception switch
        {
            DomainRuleException domain => (StatusCodes.Status400BadRequest, domain.Message, domain.Code),
            IdempotencyKeyReusedException => (StatusCodes.Status409Conflict, exception.Message, "idempotency.key.reused"),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "The record changed before this operation was applied.", "record.revision.conflict"),
            BadHttpRequestException badRequest when badRequest.StatusCode == StatusCodes.Status413PayloadTooLarge =>
                (StatusCodes.Status413PayloadTooLarge, "The request body is too large.", "request.body.too_large"),
            BadHttpRequestException badRequest =>
                (badRequest.StatusCode, "The request is not valid.", "request.invalid"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.", "server.unexpected")
        };

        if (status >= 500)
        {
            LogUnhandledException(logger, exception, httpContext.TraceIdentifier);
        }
        else
        {
            LogRejectedOperation(logger, exception, code);
        }

        httpContext.Response.StatusCode = status;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Extensions =
                {
                    ["code"] = code,
                    ["correlationId"] = httpContext.TraceIdentifier
                }
            },
            Exception = exception
        });
    }

    [LoggerMessage(
        EventId = 10,
        Level = LogLevel.Error,
        Message = "Unhandled API exception with correlation id {CorrelationId}.")]
    private static partial void LogUnhandledException(ILogger logger, Exception exception, string correlationId);

    [LoggerMessage(
        EventId = 11,
        Level = LogLevel.Warning,
        Message = "Rejected API operation with code {Code}.")]
    private static partial void LogRejectedOperation(ILogger logger, Exception exception, string code);
}
