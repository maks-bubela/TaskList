using System.ComponentModel.DataAnnotations;
using System.Net.Mime;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Options;
using TaskList.Api.Constants;

namespace TaskList.Api.Middleware;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(
        HttpContext context,
        ProblemDetailsFactory problemDetailsFactory,
        IOptions<JsonOptions> jsonOptions)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            var isValidationError = exception is ValidationException;
            if (!isValidationError)
            {
                logger.LogError(exception, "Unhandled exception while processing the request.");
            }

            if (context.Response.HasStarted)
            {
                throw;
            }

            var statusCode = isValidationError
                ? StatusCodes.Status400BadRequest
                : StatusCodes.Status500InternalServerError;
            var detail = isValidationError ? exception.Message : ExceptionMessageConstants.UnexpectedError;

            context.Response.Clear();
            var problem = problemDetailsFactory.CreateProblemDetails(context, statusCode: statusCode, detail: detail);
            context.Response.StatusCode = statusCode;
            await context.Response.WriteAsJsonAsync(
                problem,
                jsonOptions.Value.JsonSerializerOptions,
                contentType: MediaTypeNames.Application.ProblemJson + "; charset=utf-8",
                cancellationToken: context.RequestAborted);
        }
    }
}
