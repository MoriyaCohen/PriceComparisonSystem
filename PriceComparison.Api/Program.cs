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

// ===== CORS - פשוט ופתוח =====
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
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IXmlProcessingService, XmlProcessingService>();
builder.Services.AddScoped<IChainRepository, ChainRepository>();
builder.Services.AddScoped<IStoreRepository, StoreRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IStorePriceRepository, StorePriceRepository>();

// ===== IIS CONFIGURATION =====
builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = 50 * 1024 * 1024;
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

// CORS ראשון - לפני הכל!
app.UseCors("AllowAll");


// API Controllers
app.MapControllers();

// ===== STARTUP LOGGING =====
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("API starting on HTTP ONLY - no HTTPS redirect");
logger.LogInformation("CORS: Allow all origins");

// ===== RUN APPLICATION =====
app.Run();