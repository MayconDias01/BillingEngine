using System.Text.Json;
using BillingEngine.Domain.Exceptions;
using FluentValidation;

namespace BillingEngine.API.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var (statusCode, response) = exception switch
        {
            ValidationException validationEx => (
                StatusCodes.Status422UnprocessableEntity,
                new
                {
                    type = "ValidationError",
                    errors = validationEx.Errors.Select(e => new { e.PropertyName, e.ErrorMessage })
                } as object),

            SubscriptionNotFoundException => (
                StatusCodes.Status404NotFound,
                new { type = "NotFound", message = exception.Message } as object),

            DomainException => (
                StatusCodes.Status400BadRequest,
                new { type = "DomainError", message = exception.Message } as object),

            _ => (
                StatusCodes.Status500InternalServerError,
                new { type = "InternalError", message = "An unexpected error occurred." } as object)
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
            _logger.LogError(exception, "Unhandled exception");

        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}
