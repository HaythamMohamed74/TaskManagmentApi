using System.Net;
using System.Text.Json;
using TaskManagement.Domain.Exceptions;

namespace TaskManagement.Api.Middleware;

public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var (statusCode, title) = MapException(ex);

            if (statusCode == HttpStatusCode.InternalServerError)
                logger.LogError(ex, "Unhandled exception processing {Method} {Path}", context.Request.Method, context.Request.Path);
            else
                logger.LogWarning(ex, "{Title} on {Method} {Path}", title, context.Request.Method, context.Request.Path);

            context.Response.ContentType = "application/problem+json";
            context.Response.StatusCode = (int)statusCode;

            var problem = new
            {
                title,
                status = (int)statusCode,
                detail = ex.Message,
                traceId = context.TraceIdentifier
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(problem));
        }
    }

    private static (HttpStatusCode StatusCode, string Title) MapException(Exception ex) => ex switch
    {
        NotFoundException => (HttpStatusCode.NotFound, "Not Found"),
        ConflictException => (HttpStatusCode.Conflict, "Conflict"),
        ForbiddenException => (HttpStatusCode.Forbidden, "Forbidden"),
        UnauthorizedException => (HttpStatusCode.Unauthorized, "Unauthorized"),
        ValidationException => (HttpStatusCode.BadRequest, "Validation Error"),
        ArgumentException => (HttpStatusCode.BadRequest, "Bad Request"),
        _ => (HttpStatusCode.InternalServerError, "Internal Server Error")
    };
}
