using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PriceComparison.Application.Services; 
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PriceComparison.Api.Services
{
    public class DailySyncBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DailySyncBackgroundService> _logger;
        
        private readonly TimeSpan _period = TimeSpan.FromHours(24);

        
        private const string BaseFolder = @"C:\Users\ADMIN\projects\LocalXmlData";
        private string PriceDir => Path.Combine(BaseFolder, "PRICEFULL");
        private string PromoDir => Path.Combine(BaseFolder, "PROMOFULL");
        private string SyncedDir => Path.Combine(BaseFolder, "SYNCED_DATA");

        public DailySyncBackgroundService(IServiceProvider serviceProvider, ILogger<DailySyncBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Daily Sync Background Service is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("⏰ Auto-Sync Started (Daily Process)...");

                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var syncService = scope.ServiceProvider.GetRequiredService<PriceSyncService>();

                        if (!Directory.Exists(SyncedDir)) Directory.CreateDirectory(SyncedDir);

                        if (Directory.Exists(PriceDir))
                        {
                           
                            var allPriceFiles = Directory.GetFiles(PriceDir, "PriceFull*.xml", SearchOption.AllDirectories);
                            var allPromoFiles = Directory.GetFiles(PromoDir, "PromoFull*.xml", SearchOption.AllDirectories);

                            int successCount = 0;

                            foreach (var pricePath in allPriceFiles)
                            {
                                try
                                {
                                    string pFileName = Path.GetFileName(pricePath);
                                    string cleanName = pFileName.Replace("PriceFull", "");
                                    var parts = cleanName.Split('-'); 

                                    if (parts.Length >= 2)
                                    {
                                     
                                        var matchingPromoPath = allPromoFiles.FirstOrDefault(f => Path.GetFileName(f).Contains(parts[0]));

                                       
                                        syncService.GenerateSyncedFile(pricePath, matchingPromoPath, SyncedDir);
                                        successCount++;
                                    }
                                }
                                catch (Exception innerEx)
                                {
                                    _logger.LogError($"Error syncing specific file: {innerEx.Message}");
                                }
                            }
                            _logger.LogInformation($"✅ Auto-Sync Finished. Processed {successCount} files.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in Auto-Sync Fatal Loop");
                }

                
                await Task.Delay(_period, stoppingToken);
            }
        }
    }
}