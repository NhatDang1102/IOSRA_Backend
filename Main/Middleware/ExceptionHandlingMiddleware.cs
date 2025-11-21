using System.Text.Json;
using Main.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Service.Exceptions;

namespace Main.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }
    //bat loi
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (AppException ex)
        {
            await WriteErrorAsync(context, ex.StatusCode, ErrorResponse.From(ex.ErrorCode, ex.Message, ex.Details));
        }
        catch (UnauthorizedAccessException ex)
        {
            await WriteErrorAsync(context, StatusCodes.Status401Unauthorized, ErrorResponse.From("UNAUTHORIZED", ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await WriteErrorAsync(context, StatusCodes.Status500InternalServerError, ErrorResponse.From("INTERNAL_ERROR", "An unexpected error occurred. Please try again later."));
        }
    }

    private async Task WriteErrorAsync(HttpContext context, int statusCode, ErrorResponse payload)
    {
        if (context.Response.HasStarted)
        {
            _logger.LogWarning("Cannot write error response, response has already started.");
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload, _jsonOptions));
    }
}
