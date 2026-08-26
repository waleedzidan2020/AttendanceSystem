using AttendanceSystem.DTOs;
using System.Net;
using System.Text.Json;
namespace AttendanceSystem.Middleware;
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;private readonly ILogger<GlobalExceptionMiddleware> _logger;
    public GlobalExceptionMiddleware(RequestDelegate next,ILogger<GlobalExceptionMiddleware> logger){_next=next;_logger=logger;}
    public async Task InvokeAsync(HttpContext context){try{await _next(context);}catch(Exception ex){_logger.LogError(ex,"Unhandled error for {Path}",context.Request.Path);if(context.Response.HasStarted)throw;context.Response.StatusCode=(int)HttpStatusCode.InternalServerError;context.Response.ContentType="application/json";await context.Response.WriteAsync(JsonSerializer.Serialize(new ApiResponse<object>(false,null,"An unexpected error occurred.","SERVER_ERROR"),new JsonSerializerOptions(JsonSerializerDefaults.Web)));}}
}
