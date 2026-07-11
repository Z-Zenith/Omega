using System.Text;
using BackendApi.Data;
using BackendApi.Data.Entities;
using BackendApi.Hubs;
using BackendApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers(options => options.Filters.Add<SessionActiveFilter>())
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("Campus")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:Campus configuration.");

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString, npgsqlOptions =>
{
    npgsqlOptions
        .MapEnum<AccountType>()
        .MapEnum<AssignmentType>()
        .MapEnum<AttendanceStatus>()
        .MapEnum<DocType>()
        .MapEnum<FeeStatus>()
        .MapEnum<GroupType>()
        .MapEnum<NotificationType>()
        .MapEnum<ScopeKind>()
        .MapEnum<WhitelistRequestStatus>();
}));

builder.Services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
builder.Services.AddScoped<ITotpService, TotpService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<WardAccessFilter>();
builder.Services.AddScoped<SessionActiveFilter>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
// Notification Router (shared) — see Services/INotificationRouter.cs.
builder.Services.AddScoped<INotificationRouter, NotificationRouter>();
builder.Services.AddSignalR();
// SDA-13
builder.Services.AddHostedService<NoLoginAlertHostedService>();
// AWA-05
builder.Services.AddHostedService<FeeReminderHostedService>();

// SDA-25: AI Services (Track-2-owned) receives usage telemetry for suspicious-behaviour
// analysis. Defaults to the docker-compose service name/port if not overridden.
builder.Services.AddHttpClient("AiServices", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["AiServices:BaseUrl"] ?? "http://ai-services:8000");
    client.Timeout = TimeSpan.FromSeconds(10);
});

var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"];
if (string.IsNullOrWhiteSpace(jwtKey))
{
    throw new InvalidOperationException(
        builder.Environment.IsDevelopment()
            ? "Missing Jwt:Key configuration. Set it in appsettings.Development.json (untracked/local) or the JWT__Key environment variable."
            : "Missing Jwt:Key configuration. Set the JWT__Key environment variable before starting in a non-Development environment.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
        // SignalR's browser/websocket clients can't set an Authorization header on the
        // handshake, so the Notification Router's hub accepts the JWT via query string
        // instead — only for requests actually hitting the hub path.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/notifications"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            },
        };
    });
builder.Services.AddAuthorization();

// PRT-01 / SDA-02 / TWA-03 login endpoints authenticate with weak or guessable credentials
// (roll number + DOB for parents; no lockout otherwise) — rate limit by caller IP so a
// script can't brute-force the DOB/password space. Applied via
// [EnableRateLimiting(RateLimiterPolicies.Auth)] on each login action rather than globally,
// so it doesn't throttle normal authenticated traffic.
builder.Services.AddRateLimiter(RateLimiterPolicies.ConfigureAuth);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
// Notification Router (shared) — real-time transport, see Hubs/NotificationsHub.cs.
app.MapHub<NotificationsHub>("/hubs/notifications");

app.Run();
