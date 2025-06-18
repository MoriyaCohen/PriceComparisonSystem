using Microsoft.EntityFrameworkCore;
using PriceComparison.Application.Services;
using PriceComparison.Infrastructure.Models;
using PriceComparison.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

// ===== CORE SERVICES =====
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ===== DATABASE =====
builder.Services.AddDbContext<PriceComparisonDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));


// ===== CORS - פתוח לחלוטין לפיתוח (תיקון לבעיות CORS) =====
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// ===== HTTP CLIENT =====
builder.Services.AddHttpClient();

// ===== APPLICATION SERVICES =====

// Auth Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();

// XML Processing Services
builder.Services.AddScoped<IXmlProcessingService, XmlProcessingService>();

builder.Services.AddScoped<IBarcodeValidationService, BarcodeValidationService>();
builder.Services.AddScoped<IPriceComparisonService, PriceComparisonService>();
// Business Repositories  
builder.Services.AddScoped<IChainRepository, ChainRepository>();
builder.Services.AddScoped<IStoreRepository, StoreRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IStorePriceRepository, StorePriceRepository>();

// ===== IIS CONFIGURATION =====
builder.Services.Configure<IISServerOptions>(options =>
{
  options.MaxRequestBodySize = 50 * 1024 * 1024; // 50MB for XML files
});

// ===== BUILD APPLICATION =====
var app = builder.Build();

// ===== MIDDLEWARE PIPELINE =====

// Development tools
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// CORS ראשון - לפני הכל! (תיקון סדר middleware)
app.UseCors("AllowAll");

// API Controllers
app.MapControllers();

// ===== STARTUP LOGGING =====
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("?? Price Comparison API starting...");
logger.LogInformation("?? Listening on: http://localhost:5161 (HTTP only)");
logger.LogInformation("?? Authentication: Enabled");
logger.LogInformation("?? CORS: Allow all origins (development mode)");
logger.LogInformation("? HTTPS redirect: Disabled to prevent CORS issues");

// ===== RUN APPLICATION =====
app.Run();