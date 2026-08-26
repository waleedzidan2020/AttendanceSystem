using AttendanceSystem.Data;
using AttendanceSystem.DTOs;
using AttendanceSystem.Middleware;
using AttendanceSystem.Models;
using AttendanceSystem.Services;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ==========================================================
// Database
// ==========================================================

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection is required.");
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(connectionString);
});

// ==========================================================
// Identity
// ==========================================================

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
    {
        options.Password.RequiredLength = 10;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireNonAlphanumeric = false;

        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan =
            TimeSpan.FromMinutes(10);

        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// ==========================================================
// Admin Cookie
// ==========================================================

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "Attendance.Admin";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.IsEssential = true;

    options.LoginPath = "/admin/login";
    options.AccessDeniedPath = "/admin/login";

    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);

    options.Events = new CookieAuthenticationEvents
    {
        OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode =
                    StatusCodes.Status401Unauthorized;

                return context.Response.WriteAsJsonAsync(
                    new ApiResponse<object>(
                        false,
                        null,
                        "Authentication is required.",
                        "UNAUTHORIZED"));
            }

            context.Response.Redirect(context.RedirectUri);

            return Task.CompletedTask;
        },

        OnRedirectToAccessDenied = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode =
                    StatusCodes.Status403Forbidden;

                return context.Response.WriteAsJsonAsync(
                    new ApiResponse<object>(
                        false,
                        null,
                        "Access denied.",
                        "UNAUTHORIZED"));
            }

            context.Response.Redirect(context.RedirectUri);

            return Task.CompletedTask;
        }
    };
});

// ==========================================================
// CORS
// ==========================================================

builder.Services.AddCors(options =>
{
    options.AddPolicy("GitHubPages", policy =>
    {
        policy
            .WithOrigins(
                "https://waleedzidan2020.github.io")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// ==========================================================
// Controllers
// ==========================================================

builder.Services
    .AddControllersWithViews()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .ToDictionary(
                    x => x.Key,
                    x => x.Value!
                        .Errors
                        .Select(e => e.ErrorMessage)
                        .ToArray());

            return new BadRequestObjectResult(
                new ApiResponse<object>(
                    false,
                    null,
                    "One or more validation errors occurred.",
                    "VALIDATION_ERROR",
                    errors));
        };
    });

// ==========================================================
// Swagger / Health
// ==========================================================

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

// ==========================================================
// Reverse Proxy
// ==========================================================

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto;

    options.ForwardLimit = 1;

    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// ==========================================================
// Rate Limiting
// ==========================================================

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode =
        StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (ctx, token) =>
    {
        ctx.HttpContext.Response.ContentType =
            "application/json";

        await ctx.HttpContext.Response.WriteAsJsonAsync(
            new ApiResponse<object>(
                false,
                null,
                "Too many requests. Please try again shortly.",
                "RATE_LIMITED"),
            token);
    };

    options.AddPolicy(
        "worker-write",
        context =>
            RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString()
                ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));

    options.AddPolicy(
        "worker-status",
        context =>
            RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString()
                ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 20,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));
});

// ==========================================================
// Application Services
// ==========================================================

builder.Services.AddScoped<IGeofenceService, GeofenceService>();
builder.Services.AddScoped<IAttendanceService, AttendanceService>();
builder.Services.AddScoped<IAdminDashboardService, AdminDashboardService>();

var app = builder.Build();

// ==========================================================
// Middleware
// ==========================================================

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseExceptionHandler("/error");
}
else
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseRouting();

app.UseCors("GitHubPages");

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// ==========================================================
// Endpoints
// ==========================================================

app.MapControllers()
   .RequireCors("GitHubPages");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// ==========================================================
// Health
// ==========================================================

app.MapGet(
    "/health",
    async (
        ApplicationDbContext db,
        CancellationToken ct) =>
    {
        try
        {
            var canConnect =
                await db.Database.CanConnectAsync(ct);

            if (canConnect)
            {
                return Results.Ok(
                    new { status = "healthy" });
            }

            return Results.Json(
                new { status = "unhealthy" },
                statusCode:
                    StatusCodes.Status503ServiceUnavailable);
        }
        catch
        {
            return Results.Json(
                new { status = "unhealthy" },
                statusCode:
                    StatusCodes.Status503ServiceUnavailable);
        }
    });

// ==========================================================
// Migration
// ==========================================================

if (string.Equals(
        Environment.GetEnvironmentVariable("AUTO_MIGRATE"),
        "true",
        StringComparison.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();

    var db =
        scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

    await db.Database.MigrateAsync();
}

// ==========================================================
// Seed
// ==========================================================

await SeedData.InitializeAsync(
    app.Services,
    app.Environment);

app.Run();