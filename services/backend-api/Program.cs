using System.Text;
using BackendApi.Data;
using BackendApi.Data.Entities;
using BackendApi.Hubs;
using BackendApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
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
builder.Services.AddScoped<IPermissionService, PermissionService>();
// Notification Router (shared) — see Services/INotificationRouter.cs.
builder.Services.AddScoped<INotificationRouter, NotificationRouter>();
builder.Services.AddSignalR();

var jwtSection = builder.Configuration.GetSection("Jwt");
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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["Key"]!)),
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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
// Notification Router (shared) — real-time transport, see Hubs/NotificationsHub.cs.
app.MapHub<NotificationsHub>("/hubs/notifications");

app.Run();
