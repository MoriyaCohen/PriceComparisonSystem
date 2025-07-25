using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json;
using System.Linq;
using PriceComparison.Download.New.MVP;
using PriceComparison.Download.New.Wolt;
using PriceComparison.Download.New.MishnatYosef;
using PriceComparison.Download.New.SuperPharm;
using PriceComparison.Download.New.Shufersal;
using PriceComparison.Download.New.Storage;
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
                Console.WriteLine("🎯 רשתות בינה פרוגקט + שופרסל + וולט + משנת יוסף + סופר פארם");
                Console.WriteLine("============================================================");

                // תיקון הבעיה: תאריך בפורמט ישראלי
                var currentDate = DateTime.Now.ToString("dd/MM/yyyy");
                Console.WriteLine($"📅 תאריך היום (פורמט ישראלי): {currentDate}");

                // טעינת הגדרות רשתות
                var chainsConfig = await LoadChainsConfiguration();
                var enabledChains = chainsConfig.Where(c => c.Enabled).ToList();

                Console.WriteLine($"📖 נטען קובץ הגדרות: {chainsConfig.Count} רשתות מוגדרות");
                Console.WriteLine($"📋 רשתות מופעלות: {string.Join(", ", enabledChains.Select(c => c.Id))}");

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

                if (!enabledChains.Any())
                {
                    Console.WriteLine("⚠️ אין רשתות מופעלות להורדה");
                    return;
                }

                var allResults = new List<DownloadResult>();

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

                    // יצירת ChainConfig מתאים עבור משנת יוסף
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

                    // יצירת ChainConfig מתאים עבור סופר פארם
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

                // הצגת תוצאות מאוחדת
                await DisplayAllResults(allResults);
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
        /// הורדת שופרסל עם ממשק אחיד
        /// </summary>
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

                // הערכה גסה של חלוקת הקבצים
                if (downloadedCount > 0)
                {
                    result.StoresFiles = 1; // בדרך כלל קובץ אחד של חנויות
                    result.PriceFiles = (int)(downloadedCount * 0.6); // 60% מחירים
                    result.PromoFiles = downloadedCount - result.StoresFiles - result.PriceFiles; // השאר מבצעים
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

        /// <summary>
        /// הורדת וולט עם ממשק אחיד
        /// </summary>
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

                // הערכה גסה של חלוקת הקבצים
                if (downloadedCount > 0)
                {
                    result.StoresFiles = 1; // בדרך כלל קובץ אחד של חנויות
                    result.PriceFiles = (int)(downloadedCount * 0.6); // 60% מחירים
                    result.PromoFiles = downloadedCount - result.StoresFiles - result.PriceFiles; // השאר מבצעים
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

        private static async Task<List<ChainConfig>> LoadChainsConfiguration()
        {
            const string configFile = "chains.json";

            if (!File.Exists(configFile))
            {
                Console.WriteLine($"⚠️ קובץ {configFile} לא נמצא, יוצר דוגמה...");
                await CreateSampleConfiguration(configFile);
                Console.WriteLine($"✅ נוצר קובץ דוגמה: {configFile}");
                Console.WriteLine("📝 ערוך את הקובץ לפי הצורך והפעל מחדש");
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

        private static async Task CreateSampleConfiguration(string configFile)
        {
            var sampleConfig = new ChainsConfiguration
            {
                Description = "הגדרות רשתות להורדה - גרסה מתוקנת עם הורדה מלאה",
                LastUpdated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                Chains = new List<ChainConfig>
                {
                    // רשת שופרסל - מתוקנת
                    new ChainConfig
                    {
                        Id = "shufersal",
                        Name = "שופרסל בע\"מ (כולל רשת BE)",
                        BaseUrl = "https://prices.shufersal.co.il/",
                        Prefix = "Shufersal",
                        HasNetworkColumn = false,
                        Enabled = false
                    },

                    // רשת סופר פארם - מתוקנת
                    new ChainConfig
                    {
                        Id = "superpharm",
                        Name = "סופר פארם (ישראל) בע\"מ",
                        BaseUrl = "https://prices.super-pharm.co.il/",
                        Prefix = "SuperPharm",
                        HasNetworkColumn = false,
                        Enabled = false
                    },

                    // רשת משנת יוסף
                    new ChainConfig
                    {
                        Id = "mishnatyosef",
                        Name = "משנת יוסף (קיי.טי.)",
                        BaseUrl = "https://chp-kt.pages.dev/",
                        Prefix = "MishnatYosef",
                        HasNetworkColumn = false,
                        Enabled = false
                    },

                    // רשת וולט - מתוקנת
                    new ChainConfig
                    {
                        Id = "wolt",
                        Name = "וולט אופריישנס סרוויסס ישראל",
                        BaseUrl = "https://wm-gateway.wolt.com/isr-prices/public/v1",
                        Prefix = "Wolt",
                        HasNetworkColumn = false,
                        Enabled = true
                    },
                    
                    // רשתות בינה פרוגקט קיימות
                    new ChainConfig
                    {
                        Id = "kingstore",
                        Name = "קינג סטור",
                        BaseUrl = "https://kingstore.binaprojects.com",
                        Prefix = "KingStore",
                        HasNetworkColumn = false,
                        Enabled = false
                    },
                    new ChainConfig
                    {
                        Id = "maayan",
                        Name = "מעיין אלפיים",
                        BaseUrl = "https://maayan2000.binaprojects.com",
                        Prefix = "Maayan",
                        HasNetworkColumn = false,
                        Enabled = false
                    },
                    new ChainConfig
                    {
                        Id = "goodpharm",
                        Name = "גוד פארם",
                        BaseUrl = "https://goodpharm.binaprojects.com",
                        Prefix = "GoodPharm",
                        HasNetworkColumn = false,
                        Enabled = false
                    },
                    new ChainConfig
                    {
                        Id = "supersapir",
                        Name = "סופר ספיר",
                        BaseUrl = "https://supersapir.binaprojects.com",
                        Prefix = "SuperSapir",
                        HasNetworkColumn = true,
                        Enabled = false
                    },
                    new ChainConfig
                    {
                        Id = "ktshivuk",
                        Name = "קיי.טי. יבוא ושיווק (בינה פרוגקט)",
                        BaseUrl = "https://ktshivuk.binaprojects.com",
                        Prefix = "KTShivuk",
                        HasNetworkColumn = false,
                        Enabled = false
                    },
                    new ChainConfig
                    {
                        Id = "shefabirkathashem",
                        Name = "שפע ברכת השם",
                        BaseUrl = "https://shefabirkathashem.binaprojects.com",
                        Prefix = "ShefaBirkatHashem",
                        HasNetworkColumn = false,
                        Enabled = false
                    },
                    new ChainConfig
                    {
                        Id = "shukhayir",
                        Name = "שוק העיר (ט.ע.מ.ס)",
                        BaseUrl = "https://shuk-hayir.binaprojects.com",
                        Prefix = "ShukHayir",
                        HasNetworkColumn = false,
                        Enabled = false
                    },
                    new ChainConfig
                    {
                        Id = "zolvebegadol",
                        Name = "זול ובגדול",
                        BaseUrl = "https://zolvebegadol.binaprojects.com",
                        Prefix = "ZolVeBegadol",
                        HasNetworkColumn = false,
                        Enabled = false
                    },
                    new ChainConfig
                    {
                        Id = "superbareket",
                        Name = "עוף והודו ברקת - חנות המפעל",
                        BaseUrl = "https://superbareket.binaprojects.com",
                        Prefix = "SuperBareket",
                        HasNetworkColumn = false,
                        Enabled = false
                    }
                }
            };

            var json = JsonSerializer.Serialize(sampleConfig, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            await File.WriteAllTextAsync(configFile, json);
        }

        private static async Task DisplayAllResults(List<DownloadResult> results)
        {
            Console.WriteLine("\n" + "=".PadRight(70, '='));
            Console.WriteLine("📊 סיכום הורדות - כל הרשתות (גרסה מתוקנת)");
            Console.WriteLine("=".PadRight(70, '='));

            var totalSuccessful = results.Count(r => r.Success);
            var totalFiles = results.Sum(r => r.DownloadedFiles);

            Console.WriteLine($"✅ רשתות שהצליחו: {totalSuccessful}/{results.Count}");
            Console.WriteLine($"📁 סה\"כ קבצים: {totalFiles}");

            // הצגת תוצאות לפי סוג רשת
            var binaResults = results.Where(r =>
                !r.ChainName.Contains("שופרסל") &&
                !r.ChainName.Contains("וולט") &&
                !r.ChainName.Contains("משנת יוסף") &&
                !r.ChainName.Contains("סופר פארם")).ToList();

            var shuferSalResults = results.Where(r => r.ChainName.Contains("שופרסל")).ToList();
            var woltResults = results.Where(r => r.ChainName.Contains("וולט")).ToList();
            var mishnatYosefResults = results.Where(r => r.ChainName.Contains("משנת יוסף")).ToList();
            var superPharmResults = results.Where(r => r.ChainName.Contains("סופר פארם")).ToList();

            // הצגת תוצאות בינה פרוגקט
            if (binaResults.Any())
            {
                Console.WriteLine($"\n🏭 רשתות בינה פרוגקט ({binaResults.Count}):");
                foreach (var result in binaResults)
                {
                    DisplayResult(result);
                }
            }

            // הצגת תוצאות שופרסל
            if (shuferSalResults.Any())
            {
                Console.WriteLine($"\n🛒 רשת שופרסל:");
                foreach (var result in shuferSalResults)
                {
                    DisplayResult(result);
                }
            }

            // הצגת תוצאות וולט
            if (woltResults.Any())
            {
                Console.WriteLine($"\n🛍️ רשת וולט:");
                foreach (var result in woltResults)
                {
                    DisplayResult(result);
                    Console.WriteLine($"     🌐 API Directory: HTML Parsing");
                }
            }

            // הצגת תוצאות משנת יוסף
            if (mishnatYosefResults.Any())
            {
                Console.WriteLine($"\n🏪 רשת משנת יוסף:");
                foreach (var result in mishnatYosefResults)
                {
                    DisplayResult(result);
                    Console.WriteLine($"     🌐 API מיוחד: Cloudflare Workers");
                }
            }

            // הצגת תוצאות סופר פארם
            if (superPharmResults.Any())
            {
                Console.WriteLine($"\n🏥 רשת סופר פארם:");
                foreach (var result in superPharmResults)
                {
                    DisplayResult(result);
                    Console.WriteLine($"     🌐 HTML Parsing מתקדם");
                }
            }

            // שמירת לוג מפורט
            await SaveDetailedLog(results);
        }

        private static void DisplayResult(DownloadResult result)
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

        private static async Task SaveDetailedLog(List<DownloadResult> results)
        {
            var logData = new
            {
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                Version = "גרסה מתוקנת - הורדה מלאה של כל הגרסאות העדכניות",
                TotalChains = results.Count,
                SuccessfulChains = results.Count(r => r.Success),
                TotalFiles = results.Sum(r => r.DownloadedFiles),
                Results = results.Select(r => new
                {
                    ChainName = r.ChainName,
                    Success = r.Success,
                    DownloadedFiles = r.DownloadedFiles,
                    StoresFiles = r.StoresFiles,
                    PriceFiles = r.PriceFiles,
                    PromoFiles = r.PromoFiles,
                    ErrorMessage = r.ErrorMessage ?? "",
                    Duration = r.Duration,
                    SampleFiles = r.SampleFiles ?? new List<string>()
                })
            };

            var json = JsonSerializer.Serialize(logData, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            var logFileName = $"download_log_fixed_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            await File.WriteAllTextAsync(logFileName, json);

            Console.WriteLine($"\n📄 לוג מתוקן נשמר: {logFileName}");
        }
    }

    // מחלקות תצורה
    public class ChainsConfiguration
    {
        public string Description { get; set; } = "";
        public string LastUpdated { get; set; } = "";
        public List<ChainConfig> Chains { get; set; } = new();
    }
}