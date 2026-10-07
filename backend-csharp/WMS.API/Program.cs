using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using WMS.Business.Services;
using WMS.Data;

var builder = WebApplication.CreateBuilder(args);

// ---- Secrets: fail fast instead of silently running with a known key -----------------------------
var jwtSecret = builder.Configuration["JwtSettings:SecretKey"];
if (string.IsNullOrWhiteSpace(jwtSecret) || jwtSecret.Length < 32)
{
    throw new InvalidOperationException(
        "JwtSettings:SecretKey is missing or shorter than 32 characters. " +
        "Set the JwtSettings__SecretKey environment variable (see .env.example).");
}

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.CustomSchemaIds(type => type.ToString());
});

// JSON compresses ~10x: matters on slow links and weak machines
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);

// Configure localization
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[] { "fr", "en" };
    options.SetDefaultCulture("fr")
        .AddSupportedCultures(supportedCultures)
        .AddSupportedUICultures(supportedCultures);
});

// Configure CORS (origins from configuration: Cors__Origins__0=https://app.example.com ...)
var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
                  ?? new[] { "http://localhost:3000", "http://localhost:5173", "http://localhost" };
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.WithOrigins(corsOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .WithExposedHeaders("X-Total-Count")
              .AllowCredentials();
    });
});

// Configure Entity Framework with PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<WmsDbContext>(options =>
    options.UseNpgsql(connectionString));
// Read endpoints use AsNoTracking() explicitly (less RAM/CPU); update endpoints rely on tracking.

// Configure JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = false,
            ValidateAudience = false
        };
    });

// Register services
builder.Services.AddScoped<IStockLedger, StockLedger>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseResponseCompression();
if (!app.Environment.IsDevelopment() || app.Configuration.GetValue("UseHttpsRedirection", false))
{
    app.UseHttpsRedirection();
}
app.UseCors("AllowReactApp");

// Use localization
var localizationOptions = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<RequestLocalizationOptions>>().Value;
app.UseRequestLocalization(localizationOptions);

app.UseAuthentication(); // Important: avant Authorization
app.UseAuthorization();
app.MapControllers();

// Liveness + DB readiness, used by docker healthcheck / monitoring
app.MapGet("/health", async (WmsDbContext db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct)
        ? Results.Ok(new { status = "ok" })
        : Results.StatusCode(503)).AllowAnonymous();

// Apply migrations (includes the integrity layer) and seed
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<WmsDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        dbContext.Database.Migrate();

        // Demo catalogue only on request (never silently in production)
        if (app.Configuration.GetValue("SeedDemoData", app.Environment.IsDevelopment()))
        {
            SeedData.SeedDatabase(dbContext);
        }

        // First admin: the password MUST come from configuration. No default password.
        if (!dbContext.Users.Any())
        {
            var adminPassword = app.Configuration["WMS_ADMIN_PASSWORD"];
            if (string.IsNullOrWhiteSpace(adminPassword) || adminPassword.Length < 8)
            {
                logger.LogWarning("No user exists and WMS_ADMIN_PASSWORD is not set (min 8 chars): no admin account was created.");
            }
            else
            {
                dbContext.Users.Add(new WMS.Data.Entities.User
                {
                    Username = "admin",
                    Email = app.Configuration["WMS_ADMIN_EMAIL"] ?? "admin@wms.local",
                    Role = "Admin",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
                    CreatedAt = DateTime.UtcNow
                });
                dbContext.SaveChanges();
                logger.LogInformation("Admin account created.");
            }
        }
    }
    catch (Exception ex)
    {
        // A service that cannot reach/migrate its database must not pretend to be healthy.
        logger.LogCritical(ex, "Database initialisation failed");
        throw;
    }
}

app.Run();

public partial class Program { } // lets integration tests use WebApplicationFactory<Program>
