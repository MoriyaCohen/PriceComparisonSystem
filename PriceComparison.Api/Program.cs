using Microsoft.EntityFrameworkCore;
using PriceComparison.Application.Services;
using PriceComparison.Infrastructure.Models;
using PriceComparison.Infrastructure.Repositories;
using PriceComparison.Api.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// הגדרת Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/price-comparison-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// הוספת שירותים
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularApp",
        builder => builder
            .WithOrigins("http://localhost:4200")
            .AllowAnyMethod()
            .AllowAnyHeader());
});

// 🔧 Entity Framework - חיבור למסד הנתונים
builder.Services.AddDbContext<PriceComparisonDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 🔧 רישום Repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IChainRepository, ChainRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IStoreRepository, StoreRepository>();
builder.Services.AddScoped<IStorePriceRepository, StorePriceRepository>();

// רישום שירותים - Application Layer
builder.Services.AddScoped<IBarcodeValidationService, BarcodeValidationService>();
builder.Services.AddScoped<IPriceComparisonService, PriceComparisonService>();
builder.Services.AddScoped<ILocalXmlSearchService, LocalXmlSearchService>();

// 🔧 AuthService האמיתי שלך (עם מסד נתונים)
builder.Services.AddScoped<IAuthService, AuthService>();

// רישום XmlFileManager ו-XmlDataPreloaderHostedService
builder.Services.AddSingleton<XmlFileManager>();
builder.Services.AddHostedService<XmlDataPreloaderHostedService>();

var app = builder.Build();

// הגדרת pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAngularApp");

app.UseRouting();
app.MapControllers();

// הודעת התחלה
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("יישום PriceComparison הופעל בהצלחה");

// בדיקת חיבור למסד נתונים
try
{
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<PriceComparisonDbContext>();
        await context.Database.CanConnectAsync();
        logger.LogInformation("✅ חיבור למסד הנתונים בוצע בהצלחה");
    }
}
catch (Exception ex)
{
    logger.LogError(ex, "❌ שגיאה בחיבור למסד הנתונים");
}

// יצירת תיקיית XML מקומית
var xmlDataPath = Path.Combine(Directory.GetCurrentDirectory(), "LocalXmlData");
if (!Directory.Exists(xmlDataPath))
{
    Directory.CreateDirectory(xmlDataPath);
    logger.LogInformation("נוצרה תיקיית XML מקומית: {Path}", xmlDataPath);
}
else
{
    logger.LogInformation("תיקיית XML מקומית: {Path}", xmlDataPath);
}

app.Run();