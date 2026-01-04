using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PriceComparison.Download.New.BinaProject
{
    // ========== ממשק אחיד לכל הרשתות ==========
    public interface IChainDownloader
    {
        Task<List<FileMetadata>> GetAvailableFiles(string date);
        Task<DownloadResult> DownloadChain(ChainConfig config, string date);
        bool CanHandle(string chainId);
        string ChainName { get; }
        string ChainId { get; }
    }

    // ========== מודלים ==========
    public class ChainConfig
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string BaseUrl { get; set; } = "";
        public string Prefix { get; set; } = "";
        public bool HasNetworkColumn { get; set; }
        public bool Enabled { get; set; }
    }

    public class DownloadResult
    {
        public string ChainName { get; set; } = "";
        public bool Success { get; set; }
        public int DownloadedFiles { get; set; }
        public string ErrorMessage { get; set; } = "";
        public double Duration { get; set; }
        public List<string> SampleFiles { get; set; } = new();
        public int StoresFiles { get; set; }
        public int PriceFiles { get; set; }
        public int PromoFiles { get; set; }
    }

    public class FileMetadata
    {
        public string FileNm { get; set; } = "";
        public string DateFile { get; set; } = "";
        public string WStore { get; set; } = "";
        public string WFileType { get; set; } = "";
        public string Company { get; set; } = "";
        public string LastUpdateDate { get; set; } = "";
        public string LastUpdateTime { get; set; } = "";
        public string FileType { get; set; } = "";
    }

    public class DownloadResponse
    {
        public string SPath { get; set; } = "";
    }

    // ========== מחלקת בסיס מתקדמת נגד חסימות ==========
    public abstract class BinaProjectsDownloaderBase : IChainDownloader
    {
        protected readonly HttpClient _httpClient;
        protected const string BaseDownloadPath = "Downloads";

        // ✅ משתנים לניהול אנטי-בוט מתקדם
        private static readonly SemaphoreSlim _downloadSemaphore = new(2, 2); // מקסימום 2 הורדות במקביל
        private static int _requestCounter = 0;
        private static DateTime _lastRequestTime = DateTime.MinValue;
        private readonly Random _random = new();

        // ✅ רשימת User-Agents מתקדמת ומעודכנת
        private static readonly List<string> UserAgents = new()
        {
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.0.0 Safari/537.36 Edg/119.0.0.0",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:121.0) Gecko/20100101 Firefox/121.0",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10.15; rv:121.0) Gecko/20100101 Firefox/121.0",
            "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.1 Safari/605.1.15"
        };

        // ✅ רשימת Accept-Language מגוונת
        private static readonly List<string> AcceptLanguages = new()
        {
            "he-IL,he;q=0.9,en-US;q=0.8,en;q=0.7",
            "he,he-IL;q=0.9,en;q=0.8,en-US;q=0.7",
            "he-IL,he;q=0.9,en;q=0.8",
            "en-US,en;q=0.9,he;q=0.8",
            "en-US,en;q=0.9,he-IL;q=0.8,he;q=0.7"
        };

        public abstract string ChainName { get; }
        public abstract string ChainId { get; }
        protected abstract string BaseUrl { get; }
        protected abstract string ChainPrefix { get; }

        protected BinaProjectsDownloaderBase()
        {
            _httpClient = new HttpClient();
            SetupAdvancedHttpClient();
        }

        // ✅ הגדרת HttpClient מתקדמת נגד זיהוי בוט
        private void SetupAdvancedHttpClient()
        {
            _httpClient.DefaultRequestHeaders.Clear();

            // User-Agent אקראי
            var userAgent = UserAgents[_random.Next(UserAgents.Count)];
            _httpClient.DefaultRequestHeaders.Add("User-Agent", userAgent);

            // Accept headers מתקדמים
            _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");

            // Accept-Language אקראי
            var acceptLang = AcceptLanguages[_random.Next(AcceptLanguages.Count)];
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", acceptLang);

            _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
            _httpClient.DefaultRequestHeaders.Add("DNT", "1");
            _httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
            _httpClient.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");

            // Headers נוספים שגורמים לנו להיראות כמו דפדפן אמיתי
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Site", "none");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-User", "?1");
            _httpClient.DefaultRequestHeaders.Add("Cache-Control", "max-age=0");

            _httpClient.Timeout = TimeSpan.FromMinutes(15);

            Console.WriteLine($"      🎭 נבחר User-Agent: {userAgent.Substring(0, Math.Min(50, userAgent.Length))}...");

        }

        // ✅ עיכוב מתקדם נגד זיהוי בוט
        private async Task AdvancedAntiDetectionDelay(string context = "", int baseMinMs = 1200, int baseMaxMs = 2500)
        {
            //await _downloadSemaphore.WaitAsync();

            //try
            //{
            //    var requestCount = Interlocked.Increment(ref _requestCounter);
            //    var timeSinceLastRequest = DateTime.Now - _lastRequestTime;

            //    double multiplier = 1.0;

            //    // אם הבקשות צפופות מדי — עכב קצת
            //    if (timeSinceLastRequest.TotalSeconds < 1)
            //        multiplier = 1.5;

            //    // כל 10 בקשות תעכב קצת יותר
            //    if (requestCount % 10 == 0)
            //        multiplier = 2.0;

            //    var minMs = (int)(baseMinMs * multiplier);
            //    var maxMs = (int)(baseMaxMs * multiplier);

            //    var delayMs = _random.Next(minMs, maxMs);

            //    // רעש קל
            //    delayMs += _random.Next(-100, 150);
            //    delayMs = Math.Max(700, delayMs);

            //    Console.WriteLine($"      ⏳ {context} - ממתין {delayMs}ms (בקשה #{requestCount})...");

            //    await Task.Delay(delayMs);
            //    _lastRequestTime = DateTime.Now;
            //}
            //finally
            //{
            //    _downloadSemaphore.Release();
            //}
        }


        // ✅ עיכוב קצר עם הגנה
        private async Task ShortProtectedDelay(string context = "")
        {
            //var delayMs = _random.Next(100, 600); // במקום 800–2000
            //Console.WriteLine($"      ⏱️ {context} - המתנה מוגנת {delayMs}ms...");
            //await Task.Delay(delayMs);
        }
        public abstract bool CanHandle(string chainId);

        // ========== קבלת קבצים זמינים עם הגנה ==========
        public virtual async Task<List<FileMetadata>> GetAvailableFiles(string date)
        {
            const int maxRetries = 3;

            // וידוא שהתאריך תקין, אחרת נשתמש בתאריך היום
            if (string.IsNullOrEmpty(date))
                date = DateTime.Now.ToString("dd/MM/yyyy");
            else
            {
                // ניסיון להמיר תאריך קיים לפורמט dd/MM/yyyy
                if (DateTime.TryParse(date, out var parsedDate))
                    date = parsedDate.ToString("dd/MM/yyyy");
                else
                    date = DateTime.Now.ToString("dd/MM/yyyy");
            }

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var fullUrl = $"{BaseUrl}/MainIO_Hok.aspx";

                    Console.WriteLine($"      🌐 מתחבר ל: {BaseUrl} (ניסיון {attempt}/{maxRetries})");

                    await AdvancedAntiDetectionDelay($"לפני קבלת קבצים זמינים - ניסיון {attempt}");

                    var content = new FormUrlEncodedContent(new[]
                    {
                new KeyValuePair<string, string>("WStore", "0"),
                new KeyValuePair<string, string>("WDate", date),   // שולחים תאריך נכון
                new KeyValuePair<string, string>("WFileType", "0")
            });

                    using var request = new HttpRequestMessage(HttpMethod.Post, fullUrl)
                    {
                        Content = content
                    };

                    request.Headers.Add("X-Requested-With", "XMLHttpRequest");
                    request.Headers.Add("Referer", BaseUrl + "/Main.aspx");
                    request.Headers.Add("Origin", BaseUrl);

                    var response = await _httpClient.SendAsync(request);

                    if (response.StatusCode == (HttpStatusCode)418)
                    {
                        Console.WriteLine($"      🫖 זוהינו כבוט (418) - ניסיון {attempt}");
                        if (attempt < maxRetries)
                        {
                            await AdvancedAntiDetectionDelay($"אחרי שגיאת 418", 8000, 15000);
                            continue;
                        }
                        return new List<FileMetadata>();
                    }

                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();

                        if (json.Length < 10 || json.TrimStart().StartsWith("<"))
                        {
                            Console.WriteLine($"      ⚠️ תגובה לא תקינה מהשרת");
                            if (attempt < maxRetries)
                            {
                                await AdvancedAntiDetectionDelay($"אחרי תגובה לא תקינה", 5000, 10000);
                                continue;
                            }
                            return new List<FileMetadata>();
                        }

                        try
                        {
                            var files = JsonSerializer.Deserialize<List<FileMetadata>>(json) ?? new List<FileMetadata>();
                            Console.WriteLine($"      ✅ נמצאו {files.Count} קבצים זמינים");
                            return files;
                        }
                        catch (JsonException ex)
                        {
                            Console.WriteLine($"      ❌ שגיאת JSON: {ex.Message}");
                            if (attempt < maxRetries)
                            {
                                await AdvancedAntiDetectionDelay($"אחרי שגיאת JSON", 4000, 8000);
                                continue;
                            }
                            return new List<FileMetadata>();
                        }
                    }

                    Console.WriteLine($"      ❌ שגיאת HTTP: {response.StatusCode}");
                    if (attempt < maxRetries)
                    {
                        await AdvancedAntiDetectionDelay($"אחרי שגיאת HTTP {response.StatusCode}", 6000, 12000);
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"      💥 שגיאה בחיבור (ניסיון {attempt}): {ex.Message}");
                    if (attempt < maxRetries)
                    {
                        await AdvancedAntiDetectionDelay($"אחרי שגיאת חיבור", 7000, 14000);
                        continue;
                    }
                }
            }

            return new List<FileMetadata>();
        }


        private string ConvertToIsraeliDateFormat(string date)
        {
            try
            {
                DateTime parsedDate;
                string[] formats = { "MM/dd/yyyy", "dd/MM/yyyy" };
                if (DateTime.TryParseExact(date, formats, null, DateTimeStyles.None, out parsedDate))
                {
                    return parsedDate.ToString("dd/MM/yyyy");
                }
                // ניסיון כללי לפורמט כלשהו
                if (DateTime.TryParse(date, out parsedDate))
                    return parsedDate.ToString("dd/MM/yyyy");
                return date; // אם לא ניתן להמיר
            }
            catch
            {
                return date;
            }
        }


        // ========== הורדה ראשית - מתקדמת ==========
        public virtual async Task<DownloadResult> DownloadChain(ChainConfig config, string date)
        {
            var startTime = DateTime.Now;
            var result = new DownloadResult
            {
                ChainName = ChainName,
                Success = false,
                DownloadedFiles = 0,
                SampleFiles = new List<string>(),
                StoresFiles = 0,
                PriceFiles = 0,
                PromoFiles = 0
            };

            try
            {
                Console.WriteLine($"\n🏪 מתחיל הורדה מתקדמת: {ChainName}");
                Console.WriteLine($"🎯 מטרה: הקבצים העדכניים ביותר להיום לכל סניף");
                Console.WriteLine($"🛡️ מערכת הגנה: אנטי-בוט מתקדם פעיל");

                // יצירת תיקיית רשת
                var chainDir = Path.Combine(BaseDownloadPath, ChainId);
                Directory.CreateDirectory(chainDir);

                // קבלת כל הקבצים הזמינים
                var availableFiles = await GetAvailableFiles(date);

                if (!availableFiles.Any())
                {
                    result.ErrorMessage = "לא נמצאו קבצים זמינים למרות מספר ניסיונות";
                    Console.WriteLine($"      ❌ לא נמצאו קבצים זמינים להיום אחרי מספר ניסיונות");
                    result.Duration = (DateTime.Now - startTime).TotalSeconds;
                    return result;
                }

                // ניתוח מה יש בפועל
                AnalyzeAvailableFiles(availableFiles);

                // שלב 1: הורדת קבצי חנויות (StoresFull/Stores)
                result.StoresFiles = await DownloadStoresFiles(availableFiles, chainDir);
                // שלב 2: זיהוי סניפים
                var stores = GetUniqueStores(availableFiles);
                Console.WriteLine($"      📍 זוהו {stores.Count} סניפים");

                if (!stores.Any())
                {
                    Console.WriteLine($"      ⚠️ לא נמצאו סניפים - סיום הורדה");
                    result.Success = true;
                    result.Duration = (DateTime.Now - startTime).TotalSeconds;
                    return result;
                }

                // שלב 3: הורדת קבצי מחירים (העדכני ביותר לכל סניף)
                result.PriceFiles = await DownloadPriceFiles(availableFiles, stores, chainDir);

                // שלב 4: הורדת קבצי מבצעים (העדכני ביותר לכל סניף)
                result.PromoFiles = await DownloadPromoFiles(availableFiles, stores, chainDir);

                // סיכום
                result.DownloadedFiles = result.StoresFiles + result.PriceFiles + result.PromoFiles;
                result.Success = true;
                result.Duration = (DateTime.Now - startTime).TotalSeconds;

                Console.WriteLine($"      📊 סיכום: {result.StoresFiles} Stores + {result.PriceFiles} Prices + {result.PromoFiles} Promos = {result.DownloadedFiles} סה\"כ");
                Console.WriteLine($"      ✅ {ChainName}: הורדה הושלמה בהצלחה עם הגנה מתקדמת");

                return result;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                result.Duration = (DateTime.Now - startTime).TotalSeconds;
                Console.WriteLine($"      ❌ שגיאה כללית ב{ChainName}: {ex.Message}");
                return result;
            }
        }

        private void AnalyzeAvailableFiles(List<FileMetadata> files)
        {
            var types = files.GroupBy(f => GetFileType(f.FileNm)).ToDictionary(g => g.Key, g => g.Count());
            Console.WriteLine($"      🔍 ניתוח קבצים:");
            foreach (var type in types)
                Console.WriteLine($"         📄 {type.Key}: {type.Value}");
        }

        // ✅ הורדת קבצי Stores עם הגנה מתקדמת
        private async Task<int> DownloadStoresFiles(List<FileMetadata> availableFiles, string chainDir)
        {
            Console.WriteLine($"      📋 מחפש קבצי Stores...");

            var storesFiles = availableFiles
                .Where(f => f.FileNm.Contains("StoresFull") || (f.FileNm.Contains("Stores") && !f.FileNm.Contains("Full")))
                .OrderByDescending(f => f.FileNm.Contains("StoresFull") ? 1 : 0)
                .ThenByDescending(f => ExtractTimeFromFileName(f.FileNm))
                .ToList();

            if (!storesFiles.Any())
            {
                Console.WriteLine($"      ⚠️ לא נמצאו קבצי Stores");
                return 0;
            }

            var latestStores = storesFiles.First();
            Console.WriteLine($"      🎯 מוריד: {latestStores.FileNm}");

            await AdvancedAntiDetectionDelay("לפני הורדת קובץ Stores");

            var success = await DownloadAndSaveXmlWithRetry(latestStores, chainDir, "Stores");
            if (success)
            {
                Console.WriteLine($"      ✅ הורד קובץ Stores בהצלחה");
                return 1;
            }

            return 0;
        }

        // ✅ הורדת קבצי Price עם הגנה מתקדמת
        private async Task<int> DownloadPriceFiles(List<FileMetadata> availableFiles, List<string> stores, string chainDir)
        {
            Console.WriteLine($"      💰 מוריד קבצי Price עם הגנה מתקדמת...");

            int downloaded = 0;
            int failedDueToBot = 0;
            var limitedStores = stores.ToList();

            foreach (var store in limitedStores)
            {
                var priceFullFiles = availableFiles
                    .Where(f => (f.FileNm.Contains("PriceFull") || (f.FileNm.Contains("Price") && !f.FileNm.Contains("Full") && !f.FileNm.Contains("Promo"))) &&
                               ExtractStoreFromBranch(f.FileNm) == store)
                    .OrderByDescending(f => f.FileNm.Contains("PriceFull") ? 1 : 0)
                    .ThenByDescending(f => ExtractTimeFromFileName(f.FileNm))
                    .ToList();

                //חיפוש קבצי Price רגיל
                var priceFiles = availableFiles
              .Where(f => (f.FileNm.ToLower().Contains("price")) &&
                          !f.FileNm.ToLower().Contains("pricefull") &&
                          ExtractStoreFromBranch(f.FileNm) == store)
                           .OrderByDescending(f => f.FileNm.Contains("Price") ? 1 : 0)
                    .ThenByDescending(f => ExtractTimeFromFileName(f.FileNm))
                    .ToList();
                if (priceFullFiles.Any())
                {
                    var latestPrice = priceFullFiles.First();
                    var fileType = latestPrice.FileNm.Contains("PriceFull") ? "PriceFull" : "Price";

                    Console.WriteLine($"         🎯 סניף {store}: {latestPrice.FileNm}");

                    await AdvancedAntiDetectionDelay($"לפני הורדת Price לסניף {store}");

                    var success = await DownloadAndSaveXmlWithRetry(latestPrice, chainDir, fileType);
                    if (success)
                    {
                        downloaded++;
                    }
                    else
                    {
                        failedDueToBot++;
                        // אם יש יותר מדי כישלונות, תפסיק
                        if (failedDueToBot > 5)
                        {
                            Console.WriteLine($"         ⚠️ יותר מדי כישלונות (418) - מדלג על שאר הסניפים");
                            break;
                        }
                    }

                    if (store != stores.Last())
                    {
                        await ShortProtectedDelay($"אחרי הורדת Price לסניף {store}");
                    }

                }
                if (priceFiles.Any())
                {

                    var latestPrice = priceFiles.First();
                    var fileType = "Price";

                    Console.WriteLine($"         🎯 סניף {store}: {latestPrice.FileNm}");

                    await AdvancedAntiDetectionDelay($"לפני הורדת Price לסניף {store}");

                    var success = await DownloadAndSaveXmlWithRetry(latestPrice, chainDir, fileType);
                    if (success)
                    {
                        downloaded++;
                    }
                    else
                    {
                        failedDueToBot++;
                        // אם יש יותר מדי כישלונות, תפסיק
                        if (failedDueToBot > 5)
                        {
                            Console.WriteLine($"         ⚠️ יותר מדי כישלונות (418) - מדלג על שאר הסניפים");
                            break;
                        }
                    }

                    if (store != stores.Last())
                    {
                        await ShortProtectedDelay($"אחרי הורדת Price לסניף {store}");
                    }
                }
            }

            Console.WriteLine($"      💰 הורדו {downloaded} קבצי Price (נכשלו: {failedDueToBot})");
            return downloaded;
        }

        // ✅ הורדת קבצי Promo עם הגנה מתקדמת
        private async Task<int> DownloadPromoFiles(List<FileMetadata> availableFiles, List<string> stores, string chainDir)
        {
            Console.WriteLine($"      🎁 מחפש קבצי Promo עם הגנה מתקדמת...");

            int downloaded = 0;
            int failedDueToBot = 0;
            var limitedStores = stores.ToList();

            foreach (var store in limitedStores)
            {

                var promoFullFiles = availableFiles
                    .Where(f => (f.FileNm.Contains("PromoFull") || (f.FileNm.Contains("Promo") && !f.FileNm.Contains("Full"))) &&
                               ExtractStoreFromBranch( f.FileNm) == store)
                    .OrderByDescending(f => f.FileNm.Contains("PromoFull") ? 1 : 0)
                    .ThenByDescending(f => ExtractTimeFromFileName(f.FileNm))
                    .ToList();

                var promoFiles = availableFiles
                  .Where(f => (f.FileNm.ToLower().Contains("promo") && 
                  !f.FileNm.ToLower().Contains("promofull")) &&
                             ExtractStoreFromBranch( f.FileNm) == store)
                  .OrderByDescending(f => f.FileNm.Contains("PromoFull") ? 1 : 0)
                  .ThenByDescending(f => ExtractTimeFromFileName(f.FileNm))
                  .ToList();

                if (promoFullFiles.Any())
                {
                    var latestPromo = promoFullFiles.First();
                    var fileType = latestPromo.FileNm.Contains("PromoFull") ? "PromoFull" : "Promo";

                    Console.WriteLine($"         🎯 סניף {store}: {latestPromo.FileNm}");

                    await AdvancedAntiDetectionDelay($"לפני הורדת Promo לסניף {store}");

                    var success = await DownloadAndSaveXmlWithRetry(latestPromo, chainDir, fileType);
                    if (success)
                    {
                        downloaded++;
                    }
                    else
                    {
                        failedDueToBot++;
                        // אם יש יותר מדי כישלונות, תפסיק
                        if (failedDueToBot > 5)
                        {
                            Console.WriteLine($"         ⚠️ יותר מדי כישלונות (418) - מדלג על שאר קבצי Promo");
                            break;
                        }
                    }

                    if (store != stores.Last())
                    {
                        await ShortProtectedDelay($"אחרי הורדת Promo לסניף {store}");
                    }

                }
                if (promoFiles.Any())
                {
                    var latestPromo = promoFiles.First();
                    var fileType =  "Promo";

                    Console.WriteLine($"         🎯 סניף {store}: {latestPromo.FileNm}");

                    await AdvancedAntiDetectionDelay($"לפני הורדת Promo לסניף {store}");

                    var success = await DownloadAndSaveXmlWithRetry(latestPromo, chainDir, fileType);
                    if (success)
                    {
                        downloaded++;
                    }
                    else
                    {
                        failedDueToBot++;
                        // אם יש יותר מדי כישלונות, תפסיק
                        if (failedDueToBot > 5)
                        {
                            Console.WriteLine($"         ⚠️ יותר מדי כישלונות (418) - מדלג על שאר קבצי Promo");
                            break;
                        }
                    }

                    if (store != stores.Last())
                    {
                        await ShortProtectedDelay($"אחרי הורדת Promo לסניף {store}");
                    }

                }
            }

            if (downloaded > 0)
            {
                Console.WriteLine($"      🎁 הורדו {downloaded} קבצי Promo (נכשלו: {failedDueToBot})");
            }
            else
            {
                Console.WriteLine($"      🎁 לא הצליח להוריד קבצי Promo (כישלונות: {failedDueToBot})");
            }

            return downloaded;
        }

        // ========== הורדה ושמירה עם Retry ==========
        private async Task<bool> DownloadAndSaveXmlWithRetry(FileMetadata fileInfo, string chainDir, string fileType)
        {
            const int maxRetries = 2;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var typeDir = Path.Combine(chainDir, fileType);
                    Directory.CreateDirectory(typeDir);

                    // ★ חדש — בדיקה אם כבר יש קובץ מעודכן יותר
                    var fileName = fileInfo.FileNm;
                    var storeId = ExtractStoreFromBranch( fileName);   // משתמש בפונקציה הקיימת שלך
                    var newDate = ExtractDateFromFileName(fileInfo.FileNm);

                    string searchPattern = $"*-{storeId}-*.*";
                    var existingFiles = Directory.GetFiles(typeDir, searchPattern);
                    if (existingFiles.Any())
                    {
                        var existing = existingFiles
                            .Select(f => new { File = f, Date = ExtractDateFromFileName(f) })
                            .Where(x => x.Date != DateTime.MinValue)
                            .OrderByDescending(x => x.Date)
                            .FirstOrDefault();

                        if (existing != null)
                        {
                            Console.WriteLine($"         🔍 השוואת תאריכים: חדש = {newDate}, ישן = {existing.Date}");

                            if (existing.Date >= newDate)
                            {
                                Console.WriteLine($"         📁 סניף {storeId} כבר מעודכן — מדלג על הורדה.");
                                return true;
                            }
                        }
                    }

                    var downloadUrl = await GetDownloadUrlWithRetry(fileInfo.FileNm);
                    if (string.IsNullOrEmpty(downloadUrl))
                    {
                        Console.WriteLine($"         ❌ לא נמצא קישור הורדה (ניסיון {attempt})");
                        if (attempt < maxRetries)
                        {
                            await AdvancedAntiDetectionDelay($"אחרי כישלון קישור הורדה", 4000, 8000);
                            continue;
                        }
                        return false;
                    }
                    Console.WriteLine(downloadUrl);
                    var response = await _httpClient.GetAsync(downloadUrl);
                    if (response.StatusCode == (HttpStatusCode)418) // 418
                    {
                        Console.WriteLine($"         🫖 זוהינו כבוט בהורדה (418) - ניסיון {attempt}");
                        if (attempt < maxRetries)
                        {
                            await AdvancedAntiDetectionDelay($"אחרי 418 בהורדה", 10000, 20000);
                            continue;
                        }
                        return false;
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"         ❌ שגיאה בהורדה: {response.StatusCode} (ניסיון {attempt})");
                        if (attempt < maxRetries)
                        {
                            await AdvancedAntiDetectionDelay($"אחרי שגיאת הורדה", 5000, 10000);
                            continue;
                        }
                        return false;
                    }

                    var fileBytes = await response.Content.ReadAsByteArrayAsync();
                    var savedFiles = await ExtractAndSaveXml(fileBytes, fileInfo, typeDir);

                    if (savedFiles > 0)
                    {
                        Console.WriteLine($"         ✅ נשמרו {savedFiles} קבצי XML");

                        // ★ חדש — מחיקת קובץ ישן לאחר שמירה מוצלחת
                        if (existingFiles?.Any() == true)
                        {
                            foreach (var old in existingFiles)
                            {
                                try
                                {
                                    File.Delete(old);
                                    Console.WriteLine($"         🧹 נמחק קובץ ישן: {Path.GetFileName(old)}");
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"         ⚠️ שגיאה במחיקת {old}: {ex.Message}");
                                }
                            }
                        }

                        return true;
                    }

                    return false;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"         ❌ שגיאה (ניסיון {attempt}): {ex.Message}");
                    if (attempt < maxRetries)
                    {
                        await AdvancedAntiDetectionDelay($"אחרי שגיאה כללית", 6000, 12000);
                        continue;
                    }
                }
            }

            return false;
        }

        private DateTime ExtractDateFromFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return DateTime.MinValue;

            // Regex שמחפש רצף של 12 ספרות שמתחיל ב-20, לא מחובר למספרים אחרים
            var match = System.Text.RegularExpressions.Regex.Match(fileName, @"(?<!\d)20\d{10}(?!\d)");
            if (match.Success)
            {
                var candidate = match.Value;

                // בדיקה בסיסית שהחודש והיום הגיוניים
                int year = int.Parse(candidate.Substring(0, 4));
                int month = int.Parse(candidate.Substring(4, 2));
                int day = int.Parse(candidate.Substring(6, 2));

                if (month >= 1 && month <= 12 && day >= 1 && day <= 31)
                {
                    if (DateTime.TryParseExact(candidate, "yyyyMMddHHmm", null,
                                               System.Globalization.DateTimeStyles.None, out var dt))
                    {
                        return dt;
                    }
                }
            }

            return DateTime.MinValue;
        }
        private string ExtractStoreFromBranch( string fileName)
        {

                var match = Regex.Match(fileName, @"-(\d+)-");

                if (match.Success)
                {
                    return match.Groups[1].Value; // זה יהיה "250"
                }
            return "";
        }
        // ========== חילוץ ושמירת XML ==========
        private async Task<int> ExtractAndSaveXml(byte[] fileBytes, FileMetadata fileInfo, string typeDir)
        {
            try
            {
                int savedCount = 0;

                if (IsZipFile(fileBytes))
                {
                    using var zipStream = new MemoryStream(fileBytes);
                    using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

                    foreach (var entry in archive.Entries)
                    {
                        if (!string.IsNullOrEmpty(entry.Name) && entry.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                        {
                            var xmlPath = Path.Combine(typeDir, entry.Name);
                            entry.ExtractToFile(xmlPath, true);
                            savedCount++;
                        }
                    }
                }
                else if (IsGzFile(fileBytes))
                {
                    using var gzStream = new MemoryStream(fileBytes);
                    using var decompressionStream = new GZipStream(gzStream, CompressionMode.Decompress);
                    using var reader = new StreamReader(decompressionStream);

                    var xmlContent = await reader.ReadToEndAsync();
                    var xmlFileName = fileInfo.FileNm.Replace(".gz", ".xml");
                    var xmlPath = Path.Combine(typeDir, xmlFileName);

                    await File.WriteAllTextAsync(xmlPath, xmlContent);
                    savedCount = 1;
                }
                else
                {
                    var xmlFileName = fileInfo.FileNm.EndsWith(".xml") ? fileInfo.FileNm : fileInfo.FileNm + ".xml";
                    var xmlPath = Path.Combine(typeDir, xmlFileName);

                    await File.WriteAllBytesAsync(xmlPath, fileBytes);
                    savedCount = 1;
                }

                return savedCount;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"         ❌ שגיאה בחילוץ: {ex.Message}");
                return 0;
            }
        }


        // ========== פונקציות עזר עם Retry ==========
        private async Task<string> GetDownloadUrlWithRetry(string fileName)
        {
            const int maxRetries = 2;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    var apiUrl = $"{BaseUrl}/Download.aspx?FileNm={fileName}&_={timestamp}";

                    var response = await _httpClient.PostAsync(apiUrl, new StringContent(""));

                    if (response.StatusCode == (HttpStatusCode)418)
                    {
                        if (attempt < maxRetries)
                        {
                            await Task.Delay(4000);
                            continue;
                        }
                        return "";
                    }

                    if (!response.IsSuccessStatusCode)
                        return "";

                    var json = await response.Content.ReadAsStringAsync();

                    // הפורמט תמיד [{"SPath":"url"}]
                    var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (root.ValueKind == JsonValueKind.Array &&
                        root.GetArrayLength() > 0 &&
                        root[0].TryGetProperty("SPath", out var spathProp))
                    {
                        var spath = spathProp.GetString();

                        if (!string.IsNullOrEmpty(spath))
                            return spath;  // ← זה הקישור האמיתי לקובץ!
                    }

                    return "";
                }
                catch
                {
                    if (attempt < maxRetries)
                    {
                        await Task.Delay(4000);
                        continue;
                    }
                }
            }

            return "";
        }



        private List<string> GetUniqueStores(List<FileMetadata> files)
        {
            return files
                .Where(f => f.FileNm.Contains("Price") || f.FileNm.Contains("Promo"))
                .Select(f => ExtractStoreFromBranch(f.FileNm))
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct()
                .OrderBy(s => s)
                .ToList();
        }

        private string GetFileType(string fileName)
        {
            if (fileName.Contains("StoresFull")) return "StoresFull";
            if (fileName.Contains("PriceFull")) return "PriceFull";
            if (fileName.Contains("PromoFull")) return "PromoFull";
            if (fileName.Contains("Stores")) return "Stores";
            if (fileName.Contains("Price")) return "Price";
            if (fileName.Contains("Promo")) return "Promo";
            return "Unknown";
        }

       

        private string ExtractTimeFromFileName(string fileName)
        {
            try
            {
                var parts = fileName.Split('-');
                return parts.Length >= 3 ? parts[2] : "000000000000";
            }
            catch
            {
                return "000000000000";
            }
        }

        private bool IsZipFile(byte[] fileBytes)
        {
            return fileBytes.Length >= 4 &&
                   fileBytes[0] == 0x50 && fileBytes[1] == 0x4B &&
                   (fileBytes[2] == 0x03 || fileBytes[2] == 0x05 || fileBytes[2] == 0x07) &&
                   (fileBytes[3] == 0x04 || fileBytes[3] == 0x06 || fileBytes[3] == 0x08);
        }

        private bool IsGzFile(byte[] fileBytes)
        {
            return fileBytes.Length >= 2 && fileBytes[0] == 0x1F && fileBytes[1] == 0x8B;
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    // ========== רשתות קונקרטיות - כל 9 רשתות בינה פרוגקטס ==========

    public class KingStoreDownloader : BinaProjectsDownloaderBase
    {
        public override string ChainName => "קינג סטור";
        public override string ChainId => "KingStore";
        protected override string BaseUrl => "https://kingstore.binaprojects.com";
        protected override string ChainPrefix => "KingStore";

        public override bool CanHandle(string chainId)
        {
            return chainId.Equals("kingstore", StringComparison.OrdinalIgnoreCase);
        }
    }

    public class MaayanDownloader : BinaProjectsDownloaderBase
    {
        public override string ChainName => "מעיין אלפיים";
        public override string ChainId => "Maayan";
        protected override string BaseUrl => "https://maayan2000.binaprojects.com";
        protected override string ChainPrefix => "Maayan";

        public override bool CanHandle(string chainId)
        {
            return chainId.Equals("maayan", StringComparison.OrdinalIgnoreCase);
        }
    }

    public class SuperSapirDownloader : BinaProjectsDownloaderBase
    {
        public override string ChainName => "סופר ספיר";
        public override string ChainId => "SuperSapir";
        protected override string BaseUrl => "https://supersapir.binaprojects.com";
        protected override string ChainPrefix => "SuperSapir";

        public override bool CanHandle(string chainId)
        {
            return chainId.Equals("supersapir", StringComparison.OrdinalIgnoreCase);
        }
    }

    public class GoodPharmDownloader : BinaProjectsDownloaderBase
    {
        public override string ChainName => "גוד פארם";
        public override string ChainId => "GoodPharm";
        protected override string BaseUrl => "https://goodpharm.binaprojects.com";
        protected override string ChainPrefix => "GoodPharm";

        public override bool CanHandle(string chainId)
        {
            return chainId.Equals("goodpharm", StringComparison.OrdinalIgnoreCase);
        }
    }

    public class KTShivukDownloader : BinaProjectsDownloaderBase
    {
        public override string ChainName => "קיי.טי. יבוא ושיווק (משנת יוסף)";
        public override string ChainId => "KTShivuk";
        protected override string BaseUrl => "https://ktshivuk.binaprojects.com";
        protected override string ChainPrefix => "KTShivuk";

        public override bool CanHandle(string chainId)
        {
            return chainId.Equals("ktshivuk", StringComparison.OrdinalIgnoreCase) ||
                   chainId.Equals("kt", StringComparison.OrdinalIgnoreCase) ||
                   chainId.Equals("katie", StringComparison.OrdinalIgnoreCase) ||
                   chainId.Equals("mishnatyosef", StringComparison.OrdinalIgnoreCase);
        }
    }

    public class ShefaBirkatHashemDownloader : BinaProjectsDownloaderBase
    {
        public override string ChainName => "שפע ברכת השם";
        public override string ChainId => "ShefaBirkatHashem";
        protected override string BaseUrl => "https://shefabirkathashem.binaprojects.com";
        protected override string ChainPrefix => "ShefaBirkatHashem";

        public override bool CanHandle(string chainId)
        {
            return chainId.Equals("shefabirkathashem", StringComparison.OrdinalIgnoreCase) ||
                   chainId.Equals("shefa", StringComparison.OrdinalIgnoreCase) ||
                   chainId.Equals("birkathashem", StringComparison.OrdinalIgnoreCase) ||
                   chainId.Equals("ברכת-השם", StringComparison.OrdinalIgnoreCase);
        }
    }

    public class ShukHayirDownloader : BinaProjectsDownloaderBase
    {
        public override string ChainName => "שוק העיר (ט.ע.מ.ס)";
        public override string ChainId => "ShukHayir";
        protected override string BaseUrl => "https://shuk-hayir.binaprojects.com";
        protected override string ChainPrefix => "ShukHayir";

        public override bool CanHandle(string chainId)
        {
            return chainId.Equals("shukhayir", StringComparison.OrdinalIgnoreCase) ||
                   chainId.Equals("shuk-hayir", StringComparison.OrdinalIgnoreCase) ||
                   chainId.Equals("shukheir", StringComparison.OrdinalIgnoreCase) ||
                   chainId.Equals("שוק-העיר", StringComparison.OrdinalIgnoreCase) ||
                   chainId.Equals("tams", StringComparison.OrdinalIgnoreCase);
        }
    }

    public class ZolVeBegadolDownloader : BinaProjectsDownloaderBase
    {
        public override string ChainName => "זול ובגדול";
        public override string ChainId => "ZolVeBegadol";
        protected override string BaseUrl => "https://zolvebegadol.binaprojects.com";
        protected override string ChainPrefix => "ZolVeBegadol";

        public override bool CanHandle(string chainId)
        {
            return chainId.Equals("zolvebegadol", StringComparison.OrdinalIgnoreCase) ||
                   chainId.Equals("zol", StringComparison.OrdinalIgnoreCase) ||
                   chainId.Equals("begadol", StringComparison.OrdinalIgnoreCase) ||
                   chainId.Equals("זול-ובגדול", StringComparison.OrdinalIgnoreCase);
        }
    }
    public class citymarketkiryatgatDownloader : BinaProjectsDownloaderBase
    {
        public override string ChainName => "סיטי מרקטגט";
        public override string ChainId => "citymarketkiryatgat";
        protected override string BaseUrl => "https://citymarketkiryatgat.binaprojects.com";
        protected override string ChainPrefix => "citymarketkiryatgat";

        public override bool CanHandle(string chainId)
        {
            return chainId.Equals("citymarketkiryatgat", StringComparison.OrdinalIgnoreCase) 
                //||
                //   chainId.Equals("zol", StringComparison.OrdinalIgnoreCase) ||
                //   chainId.Equals("begadol", StringComparison.OrdinalIgnoreCase) ||
                //   chainId.Equals("זול-ובגדול", StringComparison.OrdinalIgnoreCase)
                   ;
        }
    }
    public class SuperBareketDownloader : BinaProjectsDownloaderBase
    {
        public override string ChainName => "עוף והודו ברקת - חנות המפעל";
        public override string ChainId => "SuperBareket";
        protected override string BaseUrl => "https://superbareket.binaprojects.com";
        protected override string ChainPrefix => "SuperBareket";

        public override bool CanHandle(string chainId)
        {
            return chainId.Equals("superbareket", StringComparison.OrdinalIgnoreCase) ||
                   chainId.Equals("bareket", StringComparison.OrdinalIgnoreCase) ||
                   chainId.Equals("ברקת", StringComparison.OrdinalIgnoreCase) ||
                   chainId.Equals("עוף-והודו", StringComparison.OrdinalIgnoreCase) ||
                   chainId.Equals("חנות-המפעל", StringComparison.OrdinalIgnoreCase);
        }
    }

    // ========== Factory מנהל את כל הרשתות ==========
    public class ChainDownloaderFactory
    {
        private readonly List<IChainDownloader> _downloaders;

        public ChainDownloaderFactory()
        {
            _downloaders = new List<IChainDownloader>
            {
                new KingStoreDownloader(),
                new MaayanDownloader(),
                new SuperSapirDownloader(),
                new GoodPharmDownloader(),
                new KTShivukDownloader(),
                new ShefaBirkatHashemDownloader(),
                new ShukHayirDownloader(),
                new ZolVeBegadolDownloader(),
                new citymarketkiryatgatDownloader(),
                new SuperBareketDownloader()
            };

            Console.WriteLine($"🏭 Factory הוקם עם {_downloaders.Count} רשתות בינה פרוגקטס");
            Console.WriteLine($"🛡️ מערכת הגנה: אנטי-בוט מתקדם עם retry ו-backoff");
            Console.WriteLine($"📋 רשתות זמינות: {string.Join(", ", _downloaders.Select(d => d.ChainName))}");
        }

        public IChainDownloader? GetDownloader(string chainId)
        {
            var downloader = _downloaders.FirstOrDefault(d => d.CanHandle(chainId));
            if (downloader != null)
            {
                Console.WriteLine($"✅ נמצא Downloader מתקדם עבור '{chainId}': {downloader.ChainName}");
            }
            else
            {
                Console.WriteLine($"❌ לא נמצא Downloader עבור '{chainId}'");
                Console.WriteLine($"💡 Chain IDs זמינים: kingstore, maayan, supersapir, goodpharm, ktshivuk, shefa, shukhayir, zol, superbareket");
            }
            return downloader;
        }

        public List<string> GetSupportedChains()
        {
            return _downloaders.Select(d => d.ChainName).ToList();
        }

        public List<IChainDownloader> GetAllDownloaders()
        {
            return _downloaders.ToList();
        }
    }
}