using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json;
using System.Linq;
using PriceComparison.Download.New.BinaProject;
using PriceComparison.Download.New.Wolt;
using PriceComparison.Download.New.MishnatYosef;
using PriceComparison.Download.New.SuperPharm;
using PriceComparison.Download.New.Shufersal;
using PriceComparison.Download.New.Storage;
using PriceComparison.Download.New.PublishedPrices;
using System.Net.Http;

namespace PriceComparison.Download.New
{
    class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                Console.WriteLine("🚀 מערכת הורדות מתוקנת - כל הרשתות (גרסאות עדכניות)");
                Console.WriteLine("🎯 רשתות בינה פרוגקט + שופרסל + וולט + משנת יוסף + סופר פארם + PublishedPrices");
                Console.WriteLine("============================================================");

                // תיקון הבעיה: תאריך בפורמט ישראלי
                var currentDate = DateTime.Now.ToString("dd/MM/yyyy");
                Console.WriteLine($"📅 תאריך היום (פורמט ישראלי): {currentDate}");

                // בדיקה איזה סוג הורדה להריץ
                if (args.Length > 0 && args[0] == "--publishedprices-only")
                {
                    Console.WriteLine("🔄 מריץ רק PublishedPrices...");
                    await RunPublishedPricesOnly(currentDate);
                }
                else if (args.Length > 0 && args[0] == "--binaproject-only")
                {
                    Console.WriteLine("🔄 מריץ רק BinaProject...");
                    await RunBinaProjectOnly(currentDate);
                }
                else
                {
                    Console.WriteLine("🔄 מריץ את כל המערכות...");
                    await RunAllSystems(currentDate);
                }

                Console.WriteLine("\n🎉 כל ההורדות הושלמו בהצלחה!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"💥 שגיאה כללית: {ex.Message}");
                Console.WriteLine($"📋 פרטים: {ex}");
            }

            Console.WriteLine("\n🔍 לחץ מקש כלשהו לסיום...");
            Console.ReadKey();
        }

        /// <summary>
        /// הרצת PublishedPrices בלבד
        /// </summary>
        static async Task RunPublishedPricesOnly(string currentDate)
        {
            Console.WriteLine("\n" + "=".PadRight(70, '='));
            Console.WriteLine("📊 מתחיל הורדות PublishedPrices - כל הרשתות");
            Console.WriteLine("=".PadRight(70, '='));

            // טעינת הגדרות רשתות PublishedPrices
            var publishedChainsConfig = await LoadPublishedPricesConfiguration();
            var enabledPublishedChains = publishedChainsConfig.Where(c => c.Enabled).ToList();

            Console.WriteLine($"📖 נטען קובץ הגדרות PublishedPrices: {publishedChainsConfig.Count} רשתות מוגדרות");
            Console.WriteLine($"📋 רשתות PublishedPrices מופעלות: {string.Join(", ", enabledPublishedChains.Select(c => c.Name))}");

            if (!enabledPublishedChains.Any())
            {
                Console.WriteLine("⚠️ אין רשתות PublishedPrices מופעלות להורדה");
                return;
            }

            var factory = new PublishedPricesDownloaderFactory();
            var allResults = new List<PublishedPricesDownloadResult>();

            // הפעלת הורדות PublishedPrices במקביל
            var downloadTasks = enabledPublishedChains.Select(async chain =>
            {
                Console.WriteLine($"\n🔍 מתחיל הורדה PublishedPrices: {chain.Name}");

                var downloader = factory.GetDownloader(chain.Type, chain.Name, chain.Id);
                if (downloader != null)
                {
                    try
                    {
                        return await downloader.DownloadChain(chain, currentDate);
                    }
                    finally
                    {
                        downloader.Dispose();
                    }
                }
                else
                {
                    Console.WriteLine($"❌ לא נמצא מטפל עבור: {chain.Name}");
                    return new PublishedPricesDownloadResult
                    {
                        ChainName = chain.Name,
                        Success = false,
                        ErrorMessage = "לא נמצא מטפל מתאים"
                    };
                }
            });

            var results = await Task.WhenAll(downloadTasks);
            allResults.AddRange(results);

            // הצגת תוצאות PublishedPrices
            await DisplayPublishedPricesResults(allResults);
        }

        /// <summary>
        /// הרצת BinaProject בלבד
        /// </summary>
        static async Task RunBinaProjectOnly(string currentDate)
        {
            Console.WriteLine("\n" + "=".PadRight(70, '='));
            Console.WriteLine("📊 מתחיל הורדות BinaProject - כל הרשתות");
            Console.WriteLine("=".PadRight(70, '='));

            // טעינת הגדרות רשתות BinaProject
            var chainsConfig = await LoadChainsConfiguration();
            var enabledChains = chainsConfig.Where(c => c.Enabled).ToList();

            Console.WriteLine($"📖 נטען קובץ הגדרות BinaProject: {chainsConfig.Count} רשתות מוגדרות");
            Console.WriteLine($"📋 רשתות BinaProject מופעלות: {string.Join(", ", enabledChains.Select(c => c.Id))}");

            if (!enabledChains.Any())
            {
                Console.WriteLine("⚠️ אין רשתות BinaProject מופעלות להורדה");
                return;
            }

            var allResults = new List<DownloadResult>();

            // הרצת הלוגיקה המקורית של BinaProject
            await RunOriginalBinaProjectLogic(enabledChains, currentDate, allResults);

            // הצגת תוצאות BinaProject
            await DisplayAllResults(allResults);
        }

        /// <summary>
        /// הרצת כל המערכות יחד
        /// </summary>
        static async Task RunAllSystems(string currentDate)
        {
            // שלב 1: הרצת BinaProject
            Console.WriteLine("\n" + "=".PadRight(70, '='));
            Console.WriteLine("🔄 שלב 1: הורדות BinaProject");
            Console.WriteLine("=".PadRight(70, '='));
            await RunBinaProjectOnly(currentDate);

            // המתנה קצרה בין המערכות
            Console.WriteLine("\n⏳ המתנה של 3 שניות לפני PublishedPrices...");
            await Task.Delay(3000);

            // שלב 2: הרצת PublishedPrices
            Console.WriteLine("\n" + "=".PadRight(70, '='));
            Console.WriteLine("🔄 שלב 2: הורדות PublishedPrices");
            Console.WriteLine("=".PadRight(70, '='));
            await RunPublishedPricesOnly(currentDate);
        }

        // ========== פונקציות עזר מקוריות מBinaProject ==========

        static async Task RunOriginalBinaProjectLogic(List<ChainConfig> enabledChains, string currentDate, List<DownloadResult> allResults)
        {
            // בדיקה איזה רשתות מופעלות
            var shuferSalEnabled = enabledChains.Any(c => c.Id.Equals("shufersal", StringComparison.OrdinalIgnoreCase));
            var woltEnabled = enabledChains.Any(c => c.Id.Equals("wolt", StringComparison.OrdinalIgnoreCase));
            var mishnatYosefEnabled = enabledChains.Any(c => c.Id.Equals("mishnatyosef", StringComparison.OrdinalIgnoreCase));
            var superPharmEnabled = enabledChains.Any(c => c.Id.Equals("superpharm", StringComparison.OrdinalIgnoreCase));
            var binaChains = enabledChains.Where(c =>
                !c.Id.Equals("shufersal", StringComparison.OrdinalIgnoreCase) &&
                !c.Id.Equals("wolt", StringComparison.OrdinalIgnoreCase) &&
                !c.Id.Equals("mishnatyosef", StringComparison.OrdinalIgnoreCase) &&
                !c.Id.Equals("superpharm", StringComparison.OrdinalIgnoreCase)).ToList();

            // הפעלת הורדות רשתות בינה פרוגקט במקביל
            if (binaChains.Any())
            {
                Console.WriteLine($"\n⚡ מתחיל {binaChains.Count} הורדות בינה פרוגקט במקביל...");

                var factory = new ChainDownloaderFactory();
                var downloadTasks = binaChains.Select(async chain =>
                {
                    Console.WriteLine($"\n🔍 זוהה {chain.Id} → {chain.Name}");

                    var downloader = factory.GetDownloader(chain.Id);
                    if (downloader != null)
                    {
                        return await downloader.DownloadChain(chain, currentDate);
                    }
                    else
                    {
                        Console.WriteLine($"❌ לא נמצא מטפל עבור: {chain.Id}");
                        return new DownloadResult
                        {
                            ChainName = chain.Name,
                            Success = false,
                            ErrorMessage = "לא נמצא מטפל מתאים"
                        };
                    }
                });

                var binaResults = await Task.WhenAll(downloadTasks);
                allResults.AddRange(binaResults);
            }

            // יצירת HttpClient ו-FileManager משותפים
            using var httpClient = new HttpClient();
            var fileManager = new FileManager();

            // הפעלת הורדת שופרסל בנפרד
            if (shuferSalEnabled)
            {
                Console.WriteLine($"\n🛒 מתחיל הורדת רשת שופרסל...");
                var shuferSalDownloader = new ShuferSalDownloader(httpClient, fileManager);
                var shuferSalResult = await DownloadShuferSal(shuferSalDownloader);
                allResults.Add(shuferSalResult);
            }

            // הפעלת הורדת וולט בנפרד
            if (woltEnabled)
            {
                Console.WriteLine($"\n🛍️ מתחיל הורדת רשת וולט...");
                var woltDownloader = new WoltDownloader(httpClient);
                var woltResult = await DownloadWolt(woltDownloader);
                allResults.Add(woltResult);
            }

            // הפעלת הורדת משנת יוסף בנפרד
            if (mishnatYosefEnabled)
            {
                Console.WriteLine($"\n🏪 מתחיל הורדת רשת משנת יוסף...");
                var mishnatYosefDownloader = new MishnatYosefDownloader();
                var mishnatYosefConfig = new ChainConfig
                {
                    Id = "mishnatyosef",
                    Name = "משנת יוסף (קיי.טי.)",
                    BaseUrl = "https://chp-kt.pages.dev/",
                    Prefix = "MishnatYosef",
                    HasNetworkColumn = false,
                    Enabled = true
                };
                var mishnatYosefResult = await mishnatYosefDownloader.DownloadChain(mishnatYosefConfig, currentDate);
                allResults.Add(mishnatYosefResult);
            }

            // הפעלת הורדת סופר פארם בנפרד
            if (superPharmEnabled)
            {
                Console.WriteLine($"\n🏥 מתחיל הורדת רשת סופר פארם...");
                var superPharmDownloader = new SuperPharmDownloader();
                var superPharmConfig = new ChainConfig
                {
                    Id = "superpharm",
                    Name = "סופר פארם (ישראל) בע\"מ",
                    BaseUrl = "https://prices.super-pharm.co.il/",
                    Prefix = "SuperPharm",
                    HasNetworkColumn = false,
                    Enabled = true
                };
                var superPharmResult = await superPharmDownloader.DownloadChain(superPharmConfig, currentDate);
                allResults.Add(superPharmResult);
            }
        }

        // ========== טעינת קובצי הגדרות ==========

        private static async Task<List<ChainConfig>> LoadChainsConfiguration()
        {
            const string configFile = "chains.json";

            if (!File.Exists(configFile))
            {
                Console.WriteLine($"⚠️ קובץ {configFile} לא נמצא, יוצר דוגמה...");
                await CreateSampleConfiguration(configFile);
                Console.WriteLine($"✅ נוצר קובץ דוגמה: {configFile}");
                return new List<ChainConfig>();
            }

            try
            {
                var json = await File.ReadAllTextAsync(configFile);
                var config = JsonSerializer.Deserialize<ChainsConfiguration>(json);
                return config?.Chains ?? new List<ChainConfig>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ שגיאה בטעינת קובץ הגדרות: {ex.Message}");
                return new List<ChainConfig>();
            }
        }

        private static async Task<List<PublishedPricesChain>> LoadPublishedPricesConfiguration()
        {
            const string configFile = "publishedprices_chains.json";

            if (!File.Exists(configFile))
            {
                Console.WriteLine($"⚠️ קובץ {configFile} לא נמצא, יוצר דוגמה...");
                await CreatePublishedPricesConfiguration(configFile);
                Console.WriteLine($"✅ נוצר קובץ דוגמה: {configFile}");
                return new List<PublishedPricesChain>();
            }

            try
            {
                var json = await File.ReadAllTextAsync(configFile);
                var config = JsonSerializer.Deserialize<PublishedPricesConfig>(json);
                return config?.Chains ?? new List<PublishedPricesChain>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ שגיאה בטעינת קובץ הגדרות PublishedPrices: {ex.Message}");
                return new List<PublishedPricesChain>();
            }
        }

        // ========== יצירת קובצי הגדרות ==========

        private static async Task CreateSampleConfiguration(string configFile)
        {
            var sampleConfig = new ChainsConfiguration
            {
                Description = "הגדרות רשתות להורדה - גרסה מתוקנת עם הורדה מלאה",
                LastUpdated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                Chains = new List<ChainConfig>
                {
                    new ChainConfig
                    {
                        Id = "shufersal",
                        Name = "שופרסל בע\"מ (כולל רשת BE)",
                        BaseUrl = "https://prices.shufersal.co.il/",
                        Prefix = "Shufersal",
                        HasNetworkColumn = false,
                        Enabled = false
                    },
                    new ChainConfig
                    {
                        Id = "wolt",
                        Name = "וולט אופריישנס סרוויסס ישראל",
                        BaseUrl = "https://wm-gateway.wolt.com/isr-prices/public/v1",
                        Prefix = "Wolt",
                        HasNetworkColumn = false,
                        Enabled = true
                    },
                    new ChainConfig
                    {
                        Id = "kingstore",
                        Name = "קינג סטור",
                        BaseUrl = "https://kingstore.binaprojects.com",
                        Prefix = "KingStore",
                        HasNetworkColumn = false,
                        Enabled = false
                    }
                    // הוסף רשתות נוספות לפי הצורך
                }
            };

            var json = JsonSerializer.Serialize(sampleConfig, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            await File.WriteAllTextAsync(configFile, json);
        }

        private static async Task CreatePublishedPricesConfiguration(string configFile)
        {
            var sampleConfig = new PublishedPricesConfig
            {
                Description = "הגדרות רשתות PublishedPrices להורדה - כל הרשתות ממסמך החקירה",
                LastUpdated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                Chains = new List<PublishedPricesChain>
                {
                    new PublishedPricesChain
                    {
                        Id = "rami_levi",
                        Name = "חנויות רמי לוי שיווק השקמה 2006 בע\"מ",
                        LoginUrl = "https://url.publishedprices.co.il/login",
                        FileUrl = "https://url.publishedprices.co.il/file",
                        Username = "RamiLevi",
                        Password = "",
                        Type = PublishedPricesType.CerberusStandard,
                        Enabled = true,
                        Notes = "ללא סיסמה"
                    },
                    new PublishedPricesChain
                    {
                        Id = "tivtaam",
                        Name = "טיב טעם רשתות בע\"מ",
                        LoginUrl = "https://url.publishedprices.co.il/login",
                        FileUrl = "https://url.publishedprices.co.il/file",
                        Username = "TivTaam",
                        Password = "",
                        Type = PublishedPricesType.CerberusStandard,
                        Enabled = true,
                        Notes = "ללא סיסמה"
                    }
                    // תוספות נוספות יתווספו בהרצה הראשונה
                }
            };

            var json = JsonSerializer.Serialize(sampleConfig, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            await File.WriteAllTextAsync(configFile, json);
        }

        // ========== פונקציות הצגת תוצאות ==========

        private static async Task DisplayAllResults(List<DownloadResult> results)
        {
            Console.WriteLine("\n" + "=".PadRight(70, '='));
            Console.WriteLine("📊 סיכום הורדות BinaProject - כל הרשתות");
            Console.WriteLine("=".PadRight(70, '='));

            var totalSuccessful = results.Count(r => r.Success);
            var totalFiles = results.Sum(r => r.DownloadedFiles);

            Console.WriteLine($"✅ רשתות שהצליחו: {totalSuccessful}/{results.Count}");
            Console.WriteLine($"📁 סה\"כ קבצים: {totalFiles}");

            foreach (var result in results)
            {
                var status = result.Success ? "✅" : "❌";
                Console.WriteLine($"  {status} {result.ChainName}: {result.DownloadedFiles} קבצים");

                if (!result.Success && !string.IsNullOrEmpty(result.ErrorMessage))
                {
                    Console.WriteLine($"     💬 {result.ErrorMessage}");
                }
                else if (result.Success)
                {
                    Console.WriteLine($"     📋 {result.StoresFiles} Stores + {result.PriceFiles} Prices + {result.PromoFiles} Promos");
                    Console.WriteLine($"     ⏱️ זמן ביצוע: {result.Duration:F1} שניות");
                }
            }

            // שמירת לוג
            var logData = new
            {
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                Type = "BinaProject",
                TotalChains = results.Count,
                SuccessfulChains = results.Count(r => r.Success),
                TotalFiles = results.Sum(r => r.DownloadedFiles),
                Results = results
            };

            var json = JsonSerializer.Serialize(logData, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            var logFileName = $"binaproject_download_log_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            await File.WriteAllTextAsync(logFileName, json);
            Console.WriteLine($"\n📄 לוג BinaProject נשמר: {logFileName}");
        }

        private static async Task DisplayPublishedPricesResults(List<PublishedPricesDownloadResult> results)
        {
            Console.WriteLine("\n" + "=".PadRight(70, '='));
            Console.WriteLine("📊 סיכום הורדות PublishedPrices - כל הרשתות");
            Console.WriteLine("=".PadRight(70, '='));

            var totalSuccessful = results.Count(r => r.Success);
            var totalFiles = results.Sum(r => r.DownloadedFiles);

            Console.WriteLine($"✅ רשתות שהצליחו: {totalSuccessful}/{results.Count}");
            Console.WriteLine($"📁 סה\"כ קבצים: {totalFiles}");

            foreach (var result in results)
            {
                var status = result.Success ? "✅" : "❌";
                Console.WriteLine($"  {status} {result.ChainName}: {result.DownloadedFiles} קבצים");

                if (!result.Success && !string.IsNullOrEmpty(result.ErrorMessage))
                {
                    Console.WriteLine($"     💬 {result.ErrorMessage}");
                }
                else if (result.Success)
                {
                    Console.WriteLine($"     📋 {result.StoresFiles} Stores + {result.PriceFiles} Prices + {result.PromoFiles} Promos");
                    Console.WriteLine($"     ⏱️ זמן ביצוע: {result.Duration:F1} שניות");
                }
            }

            // שמירת לוג
            var logData = new
            {
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                Type = "PublishedPrices",
                TotalChains = results.Count,
                SuccessfulChains = results.Count(r => r.Success),
                TotalFiles = results.Sum(r => r.DownloadedFiles),
                Results = results
            };

            var json = JsonSerializer.Serialize(logData, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            var logFileName = $"publishedprices_download_log_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            await File.WriteAllTextAsync(logFileName, json);
            Console.WriteLine($"\n📄 לוג PublishedPrices נשמר: {logFileName}");
        }

        // ========== פונקציות שופרסל ווולט (מהקוד המקורי) ==========

        private static async Task<DownloadResult> DownloadShuferSal(ShuferSalDownloader downloader)
        {
            var startTime = DateTime.Now;
            var result = new DownloadResult
            {
                ChainName = "שופרסל בע\"מ (כולל רשת BE)",
                Success = false
            };

            try
            {
                var downloadedCount = await downloader.DownloadLatestFiles();
                result.DownloadedFiles = downloadedCount;
                result.Success = downloadedCount > 0;
                result.Duration = (DateTime.Now - startTime).TotalSeconds;

                if (downloadedCount > 0)
                {
                    result.StoresFiles = 1;
                    result.PriceFiles = (int)(downloadedCount * 0.6);
                    result.PromoFiles = downloadedCount - result.StoresFiles - result.PriceFiles;
                }

                if (!result.Success)
                {
                    result.ErrorMessage = "לא הצליח להוריד קבצים מהשרת";
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                result.Duration = (DateTime.Now - startTime).TotalSeconds;
            }

            return result;
        }

        private static async Task<DownloadResult> DownloadWolt(WoltDownloader downloader)
        {
            var startTime = DateTime.Now;
            var result = new DownloadResult
            {
                ChainName = "וולט אופריישנס סרוויסס ישראל",
                Success = false
            };

            try
            {
                var downloadedCount = await downloader.DownloadLatestFiles();
                result.DownloadedFiles = downloadedCount;
                result.Success = downloadedCount > 0;
                result.Duration = (DateTime.Now - startTime).TotalSeconds;

                if (downloadedCount > 0)
                {
                    result.StoresFiles = 1;
                    result.PriceFiles = (int)(downloadedCount * 0.6);
                    result.PromoFiles = downloadedCount - result.StoresFiles - result.PriceFiles;
                }

                if (!result.Success)
                {
                    result.ErrorMessage = "לא הצליח להוריד קבצים מהשרת";
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                result.Duration = (DateTime.Now - startTime).TotalSeconds;
            }

            return result;
        }
    }

    // ========== מחלקות תצורה ==========
    public class ChainsConfiguration
    {
        public string Description { get; set; } = "";
        public string LastUpdated { get; set; } = "";
        public List<ChainConfig> Chains { get; set; } = new();
    }
}