using System.Text;
using BackendApi.Data;
using BackendApi.Data.Entities;
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

// AIS-03/04/07: self-hosted AI Services container (services/ai-services, FastAPI).
// Falls back to the docker-compose service name for the default so a local
// (non-Docker) `dotnet run` still points somewhere sane via the published port mapping.
builder.Services.AddHttpClient<IAiServicesClient, AiServicesClient>(client =>
{
    var baseUrl = builder.Configuration["AiServices:BaseUrl"] ?? "http://localhost:8000";
    client.BaseAddress = new Uri(baseUrl);
});

// AIS-02: Copyleaks — external, credentialed (Copyleaks:Email/ApiKey/WebhookSecret).
// No default base URL fallback: an empty ApiKey/Email already fails closed inside
// CopyleaksClient via ExternalServiceNotConfiguredException, so there's no safe
// "local dev" default the way AiServices:BaseUrl has one.
builder.Services.AddHttpClient<ICopyleaksClient, CopyleaksClient>(client =>
{
    var baseUrl = builder.Configuration["Copyleaks:BaseUrl"] ?? "https://api.copyleaks.com";
    client.BaseAddress = new Uri(baseUrl);
});

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

app.Run();
