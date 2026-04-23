using System.Text;
using EMS.API.Hubs;
using EMS.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

// ─── MongoDB ──────────────────────────────────────────────────────────────────
var mongoConnectionString = builder.Configuration["MongoDB:ConnectionString"]
    ?? throw new InvalidOperationException("MongoDB:ConnectionString is required.");
var mongoDatabaseName = builder.Configuration["MongoDB:DatabaseName"] ?? "ems_db";

var mongoClient = new MongoClient(mongoConnectionString);
var mongoDatabase = mongoClient.GetDatabase(mongoDatabaseName);

builder.Services.AddSingleton<IMongoClient>(mongoClient);
builder.Services.AddSingleton(mongoDatabase);

// ─── JWT Authentication ───────────────────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is required.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        };

        // Allow SignalR to receive the JWT from the query string (required by browser WebSocket)
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ─── CORS ─────────────────────────────────────────────────────────────────────
// Flutter web runs on a random high port, so we allow ALL localhost origins.
// In production replace SetIsOriginAllowed with a strict WithOrigins() list.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy
            .SetIsOriginAllowed(_ => true)   // allow any origin in dev
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();             // required for SignalR WebSocket
    });
});

// ─── SignalR ──────────────────────────────────────────────────────────────────
builder.Services.AddSignalR();

// ─── ML Service HTTP client ───────────────────────────────────────────────────
var mlBaseUrl = builder.Configuration["MlService:BaseUrl"] ?? "http://localhost:8000";
builder.Services.AddHttpClient<IMlService, MlService>(client =>
{
    client.BaseAddress = new Uri(mlBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(60);
});

// ─── Application services ─────────────────────────────────────────────────────
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<ISessionService, SessionService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IDispatchService, DispatchService>();
builder.Services.AddScoped<IDriverService, DriverService>();

// ─── Controllers + Swagger ───────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "EMS API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new()
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter your JWT token.",
    });
    c.AddSecurityRequirement(new()
    {
        {
            new() { Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

// ─── Build ────────────────────────────────────────────────────────────────────
var app = builder.Build();

// Swagger available in all environments (gate behind auth or restrict to non-prod as needed)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "EMS API v1");
    c.RoutePrefix = "swagger";
});

// Middleware order matters for CORS:
// UseRouting → UseCors → UseAuthentication → UseAuthorization → endpoints
// UseCors must be after UseRouting so it can inspect the matched endpoint,
// and before UseAuthentication so OPTIONS preflight requests are not rejected
// with a 401 before the CORS headers are written.
app.UseRouting();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

// Minimal liveness probe — used by Docker health check
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "EMS.API" }));

app.MapControllers();
app.MapHub<DispatchHub>("/hubs/dispatch");

// Bind on all interfaces so Android devices on the same Wi-Fi can reach the
// backend. By default Kestrel only listens on localhost, which is invisible
// to a real phone. This makes the API reachable at http://<LAN-IP>:5000.
// In production, replace with a proper reverse proxy (nginx/IIS) + HTTPS.
app.Run("http://0.0.0.0:5000");
