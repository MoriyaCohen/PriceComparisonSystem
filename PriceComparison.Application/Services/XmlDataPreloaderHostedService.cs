// צור קובץ חדש: PriceComparison.Api/Services/XmlDataPreloaderHostedService.cs

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PriceComparison.Application.Services;

namespace PriceComparison.Api.Services
{
    /// <summary>
    /// שירות רקע לטעינת נתוני XML בעת עליית השרת
    /// מבטיח שהנתונים נטענים לפני שהAPIים זמינים
    /// </summary>
    public class XmlDataPreloaderHostedService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<XmlDataPreloaderHostedService> _logger;

        public XmlDataPreloaderHostedService(
            IServiceProvider serviceProvider,
            ILogger<XmlDataPreloaderHostedService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        /// <summary>
        /// מתחיל בעת עליית השרת - טוען נתוני XML
        /// </summary>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("📦 התחלת טעינת קבצי XML בעת עליית השרת...");

            try
            {
                // יצירת scope חדש כדי לקבל את השירותים
                using var scope = _serviceProvider.CreateScope();
                var localXmlSearchService = scope.ServiceProvider.GetRequiredService<ILocalXmlSearchService>();

                // טעינת הנתונים
                var success = await localXmlSearchService.RefreshDataAsync();

                if (success)
                {
                    _logger.LogInformation("✅ טעינת נתוני XML הושלמה בהצלחה");

                    // קבלת סטטיסטיקות
                    var status = await localXmlSearchService.GetDataStatusAsync();
                    _logger.LogInformation("📊 נטענו: {ProductCount} מוצרים מ-{ChainCount} רשתות ו-{StoreCount} סניפים",
                        status.TotalProducts, status.LoadedChains, status.LoadedStores);
                }
                else
                {
                    _logger.LogWarning("⚠️ טעינת נתוני XML נכשלה");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ שגיאה בטעינת נתוני XML בעת עליית השרת");
                // לא עוצרים את השרת אם הטעינה נכשלה
            }
        }

        /// <summary>
        /// מתבצע בעת כיבוי השרת
        /// </summary>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("🛑 XmlDataPreloaderHostedService נעצר");
            return Task.CompletedTask;
        }
    }
}