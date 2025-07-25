using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace PriceComparison.Download.New.Wolt
{
    /// <summary>
    /// מודל לקובץ ברשת וולט
    /// </summary>
    public class WoltFileInfo
    {
        public string FileName { get; set; } = "";
        public string FileType { get; set; } = "";
        public string StoreId { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
        public string LastModified { get; set; } = "";
    }

    /// <summary>
    /// תוצאת הורדה מוולט
    /// </summary>
    public class WoltDownloadResult
    {
        public string ChainName { get; set; } = "וולט אופריישנס סרוויסס ישראל";
        public bool Success { get; set; } = false;
        public int DownloadedFiles { get; set; } = 0;
        public int StoresFiles { get; set; } = 0;
        public int PriceFiles { get; set; } = 0;
        public int PromoFiles { get; set; } = 0;
        public string ErrorMessage { get; set; } = "";
        public double Duration { get; set; } = 0;
        public List<string> SampleFiles { get; set; } = new();
    }

    /// <summary>
    /// מוריד קבצים מרשת וולט - API Directory based
    /// </summary>
    public class WoltDownloader
    {
        private readonly HttpClient _httpClient;
        private readonly Random _random = new();
        private const string BaseDownloadPath = "Downloads";

        // הגדרות ייחודיות לרשת וולט
        private const string WOLT_BASE_URL = "https://wm-gateway.wolt.com/isr-prices/public/v1";
        private const string WOLT_INDEX_URL = "https://wm-gateway.wolt.com/isr-prices/public/v1/index.html";

        public string ChainName => "וולט אופריישנס סרוויסס ישראל";
        public string ChainId => "Wolt";

        public WoltDownloader(HttpClient httpClient)
        {
            _httpClient = httpClient;
            SetupHttpClient();
        }

        /// <summary>
        /// הגדרת HttpClient עם headers מתקדמים
        /// </summary>
        private void SetupHttpClient()
        {
            _httpClient.DefaultRequestHeaders.Clear();

            // User-Agent דמוי דפדפן אמיתי
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

            // Headers נוספים לדמיון לדפדפן
            _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "he-IL,he;q=0.9,en-US;q=0.8,en;q=0.7");
            _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
            _httpClient.DefaultRequestHeaders.Add("DNT", "1");
            _httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
            _httpClient.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");

            _httpClient.Timeout = TimeSpan.FromMinutes(10);
        }

        /// <summary>
        /// הורדת כל הקבצים העדכניים - נקודת הכניסה הראשית
        /// </summary>
        public async Task<int> DownloadLatestFiles()
        {
            try
            {
                Console.WriteLine($"🛍️ מתחיל הורדת רשת וולט...");
                Console.WriteLine($"🎯 מטרה: קבצים עדכניים מ-API Directory");

                // יצירת תיקיית רשת
                var chainDir = Path.Combine(BaseDownloadPath, "Wolt");
                Directory.CreateDirectory(chainDir);

                // שלב 1: קבלת התאריך העדכני
                var latestDate = await GetLatestAvailableDate();
                if (string.IsNullOrEmpty(latestDate))
                {
                    Console.WriteLine($"      ❌ לא נמצא תאריך עדכני זמין");
                    return 0;
                }

                Console.WriteLine($"      📅 תאריך עדכני: {latestDate}");

                // שלב 2: קבלת כל הקבצים לתאריך העדכני
                var availableFiles = await GetFilesForDate(latestDate);

                if (!availableFiles.Any())
                {
                    Console.WriteLine($"      ❌ לא נמצאו קבצים עבור תאריך {latestDate}");
                    return 0;
                }

                Console.WriteLine($"      ✅ נמצאו {availableFiles.Count} קבצים עבור {latestDate}");

                // ניתוח הקבצים
                AnalyzeAvailableFiles(availableFiles);

                int totalDownloaded = 0;

                // שלב 3: הורדת קבצי Stores
                totalDownloaded += await DownloadStoresFiles(availableFiles, chainDir);

                // שלב 4: זיהוי סניפים
                var stores = GetUniqueStores(availableFiles);
                Console.WriteLine($"      📍 זוהו {stores.Count} סניפים");

                if (stores.Any())
                {
                    // הגבלה ל-5 סניפים לבדיקה
                    var limitedStores = stores.Take(5).ToList();
                    Console.WriteLine($"      🔍 מגביל ל-{limitedStores.Count} סניפים לבדיקה");

                    // שלב 5: הורדת קבצי מחירים
                    totalDownloaded += await DownloadPriceFiles(availableFiles, limitedStores, chainDir);

                    // שלב 6: הורדת קבצי מבצעים
                    totalDownloaded += await DownloadPromoFiles(availableFiles, limitedStores, chainDir);
                }

                Console.WriteLine($"      ✅ {ChainName}: הורדה הושלמה בהצלחה - {totalDownloaded} קבצים");

                return totalDownloaded;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"      ❌ שגיאה כללית ב{ChainName}: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// קבלת התאריך העדכני הזמין
        /// </summary>
        private async Task<string> GetLatestAvailableDate()
        {
            try
            {
                Console.WriteLine($"      🌐 מתחבר לוולט לקבלת רשימת תאריכים: {WOLT_INDEX_URL}");

                var response = await _httpClient.GetAsync(WOLT_INDEX_URL);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"      ❌ שגיאת HTTP: {response.StatusCode}");
                    return "";
                }

                var htmlContent = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrWhiteSpace(htmlContent))
                {
                    Console.WriteLine($"      ⚠️ תגובה ריקה מהשרת");
                    return "";
                }

                // חילוץ תאריכים מה-HTML
                var dates = ExtractDatesFromHtml(htmlContent);

                if (!dates.Any())
                {
                    Console.WriteLine($"      ❌ לא נמצאו תאריכים זמינים");
                    return "";
                }

                // מיון התאריכים בסדר יורד (העדכני ראשון)
                var sortedDates = dates.OrderByDescending(d => d).ToList();

                Console.WriteLine($"      📅 נמצאו {dates.Count} תאריכים זמינים");
                Console.WriteLine($"      🔍 דוגמאות: {string.Join(", ", sortedDates.Take(3))}");

                return sortedDates.First();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"      💥 שגיאה בקבלת תאריכים: {ex.Message}");
                return "";
            }
        }

        /// <summary>
        /// חילוץ תאריכים מה-HTML
        /// </summary>
        private List<string> ExtractDatesFromHtml(string htmlContent)
        {
            var dates = new List<string>();

            try
            {
                // דפוס לחילוץ תאריכים: <a href="2025-07-25.html">2025-07-25</a>
                var datePattern = @"<a[^>]*href=""(\d{4}-\d{2}-\d{2})\.html""[^>]*>(\d{4}-\d{2}-\d{2})</a>";
                var matches = Regex.Matches(htmlContent, datePattern, RegexOptions.IgnoreCase);

                foreach (Match match in matches)
                {
                    var dateStr = match.Groups[1].Value;
                    if (!string.IsNullOrEmpty(dateStr) && IsValidDate(dateStr))
                    {
                        dates.Add(dateStr);
                    }
                }

                // הסרת כפילויות
                dates = dates.Distinct().ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"      ⚠️ שגיאה בחילוץ תאריכים: {ex.Message}");
            }

            return dates;
        }

        /// <summary>
        /// בדיקה האם התאריך תקין
        /// </summary>
        private bool IsValidDate(string dateStr)
        {
            return DateTime.TryParseExact(dateStr, "yyyy-MM-dd", null,
                System.Globalization.DateTimeStyles.None, out _);
        }

        /// <summary>
        /// קבלת קבצים עבור תאריך ספציפי
        /// </summary>
        private async Task<List<WoltFileInfo>> GetFilesForDate(string date)
        {
            try
            {
                Console.WriteLine($"      📂 מקבל קבצים עבור תאריך: {date}");

                var dateUrl = $"{WOLT_BASE_URL}/{date}.html";
                var response = await _httpClient.GetAsync(dateUrl);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"      ❌ שגיאת HTTP בעמוד התאריך: {response.StatusCode}");
                    return new List<WoltFileInfo>();
                }

                var htmlContent = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrWhiteSpace(htmlContent))
                {
                    Console.WriteLine($"      ⚠️ תגובה ריקה מעמוד התאריך");
                    return new List<WoltFileInfo>();
                }

                // פרסור הקבצים מה-HTML
                var files = ParseFilesFromDatePage(htmlContent, date);

                Console.WriteLine($"      ✅ נמצאו {files.Count} קבצים לתאריך {date}");

                return files;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"      💥 שגיאה בקבלת קבצים לתאריך {date}: {ex.Message}");
                return new List<WoltFileInfo>();
            }
        }

        /// <summary>
        /// פרסור קבצים מעמוד התאריך
        /// </summary>
        private List<WoltFileInfo> ParseFilesFromDatePage(string htmlContent, string date)
        {
            var files = new List<WoltFileInfo>();

            try
            {
                // דפוס לחילוץ קישורי הורדה: <a href="filename.xml">filename.xml</a>
                var filePattern = @"<a[^>]*href=""([^""]+\.(?:xml|zip|gz))""[^>]*>([^<]+)</a>";
                var matches = Regex.Matches(htmlContent, filePattern, RegexOptions.IgnoreCase);

                foreach (Match match in matches)
                {
                    var fileName = match.Groups[2].Value.Trim();
                    var relativeUrl = match.Groups[1].Value.Trim();

                    // יצירת URL מלא
                    var fullUrl = relativeUrl.StartsWith("http") ? relativeUrl : $"{WOLT_BASE_URL}/{relativeUrl}";

                    // זיהוי סוג הקובץ וסניף
                    var fileType = DetermineFileType(fileName);
                    var storeId = ExtractStoreFromFileName(fileName);

                    files.Add(new WoltFileInfo
                    {
                        FileName = fileName,
                        FileType = fileType,
                        StoreId = storeId,
                        DownloadUrl = fullUrl,
                        LastModified = date
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"      ⚠️ שגיאה בפרסור קבצים: {ex.Message}");
            }

            return files;
        }

        /// <summary>
        /// ניתוח קבצים זמינים
        /// </summary>
        private void AnalyzeAvailableFiles(List<WoltFileInfo> files)
        {
            var types = files.GroupBy(f => f.FileType).ToDictionary(g => g.Key, g => g.Count());
            Console.WriteLine($"      🔍 ניתוח קבצים מוולט:");
            foreach (var type in types)
                Console.WriteLine($"         📄 {type.Key}: {type.Value}");
        }

        /// <summary>
        /// הורדת קבצי Stores
        /// </summary>
        private async Task<int> DownloadStoresFiles(List<WoltFileInfo> availableFiles, string chainDir)
        {
            Console.WriteLine($"      📋 מחפש קבצי Stores...");

            var storesFiles = availableFiles
                .Where(f => f.FileType == "StoresFull" || f.FileType == "Stores")
                .OrderByDescending(f => f.FileType == "StoresFull" ? 1 : 0)
                .ToList();

            if (!storesFiles.Any())
            {
                Console.WriteLine($"      ⚠️ לא נמצאו קבצי Stores");
                return 0;
            }

            var latestStores = storesFiles.First();
            Console.WriteLine($"      🎯 מוריד: {latestStores.FileName}");

            var success = await DownloadAndSaveFile(latestStores, chainDir, "Stores");
            return success ? 1 : 0;
        }

        /// <summary>
        /// הורדת קבצי מחירים
        /// </summary>
        private async Task<int> DownloadPriceFiles(List<WoltFileInfo> availableFiles, List<string> stores, string chainDir)
        {
            Console.WriteLine($"      💰 מוריד קבצי Price...");

            int downloaded = 0;

            foreach (var store in stores)
            {
                // חיפוש קבצי PriceFull
                var priceFullFiles = availableFiles
                    .Where(f => f.FileType == "PriceFull" && f.StoreId == store)
                    .ToList();

                // חיפוש קבצי Price רגיל
                var priceFiles = availableFiles
                    .Where(f => f.FileType == "Price" && f.StoreId == store)
                    .ToList();

                Console.WriteLine($"         🔍 סניף {store}: {priceFullFiles.Count} PriceFull, {priceFiles.Count} Price");

                // הורדת PriceFull אם קיים
                if (priceFullFiles.Any())
                {
                    var latestPriceFull = priceFullFiles.First();
                    Console.WriteLine($"         🎯 סניף {store} PriceFull: {latestPriceFull.FileName}");

                    await Task.Delay(_random.Next(500, 1500));
                    var success = await DownloadAndSaveFileWithRetry(latestPriceFull, chainDir, "PriceFull");
                    if (success) downloaded++;
                }

                // הורדת Price רגיל אם קיים
                if (priceFiles.Any())
                {
                    var latestPrice = priceFiles.First();
                    Console.WriteLine($"         🎯 סניף {store} Price: {latestPrice.FileName}");

                    await Task.Delay(_random.Next(500, 1500));
                    var success = await DownloadAndSaveFileWithRetry(latestPrice, chainDir, "Price");
                    if (success) downloaded++;
                }
            }

            Console.WriteLine($"      💰 הורדו {downloaded} קבצי Price");
            return downloaded;
        }

        /// <summary>
        /// הורדת קבצי מבצעים
        /// </summary>
        private async Task<int> DownloadPromoFiles(List<WoltFileInfo> availableFiles, List<string> stores, string chainDir)
        {
            Console.WriteLine($"      🎁 מחפש קבצי Promo...");

            int downloaded = 0;

            foreach (var store in stores)
            {
                // חיפוש קבצי PromoFull
                var promoFullFiles = availableFiles
                    .Where(f => f.FileType == "PromoFull" && f.StoreId == store)
                    .ToList();

                // חיפוש קבצי Promo רגיל
                var promoFiles = availableFiles
                    .Where(f => f.FileType == "Promo" && f.StoreId == store)
                    .ToList();

                Console.WriteLine($"         🔍 סניף {store}: {promoFullFiles.Count} PromoFull, {promoFiles.Count} Promo");

                // הורדת PromoFull אם קיים
                if (promoFullFiles.Any())
                {
                    var latestPromoFull = promoFullFiles.First();
                    Console.WriteLine($"         🎯 סניף {store} PromoFull: {latestPromoFull.FileName}");

                    await Task.Delay(_random.Next(500, 1500));
                    var success = await DownloadAndSaveFileWithRetry(latestPromoFull, chainDir, "PromoFull");
                    if (success) downloaded++;
                }

                // הורדת Promo רגיל אם קיים
                if (promoFiles.Any())
                {
                    var latestPromo = promoFiles.First();
                    Console.WriteLine($"         🎯 סניף {store} Promo: {latestPromo.FileName}");

                    await Task.Delay(_random.Next(500, 1500));
                    var success = await DownloadAndSaveFileWithRetry(latestPromo, chainDir, "Promo");
                    if (success) downloaded++;
                }
            }

            if (downloaded > 0)
            {
                Console.WriteLine($"      🎁 הורדו {downloaded} קבצי Promo");
            }
            else
            {
                Console.WriteLine($"      🎁 לא נמצאו קבצי Promo");
            }

            return downloaded;
        }

        /// <summary>
        /// הורדה ושמירת קובץ עם retry mechanism
        /// </summary>
        private async Task<bool> DownloadAndSaveFileWithRetry(WoltFileInfo fileInfo, string chainDir, string fileType, int maxRetries = 3)
        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    if (attempt > 1)
                    {
                        Console.WriteLine($"         🔄 ניסיון {attempt}/{maxRetries}: {fileInfo.FileName}");
                        await Task.Delay(_random.Next(2000, 5000));
                    }

                    var success = await DownloadAndSaveFile(fileInfo, chainDir, fileType);
                    if (success)
                    {
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"         ⚠️ ניסיון {attempt} נכשל: {ex.Message}");
                    if (attempt == maxRetries)
                    {
                        Console.WriteLine($"         ❌ נכשל לאחר {maxRetries} ניסיונות: {fileInfo.FileName}");
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// הורדה ושמירת קובץ - גרסה משופרת עם anti-bot protection
        /// </summary>
        private async Task<bool> DownloadAndSaveFile(WoltFileInfo fileInfo, string chainDir, string fileType)
        {
            try
            {
                var typeDir = Path.Combine(chainDir, fileType);
                Directory.CreateDirectory(typeDir);

                Console.WriteLine($"         📥 מוריד מ: {fileInfo.DownloadUrl}");

                // הוספת headers ספציפיים לבקשה
                var request = new HttpRequestMessage(HttpMethod.Get, fileInfo.DownloadUrl);
                request.Headers.Add("Referer", $"{WOLT_BASE_URL}/index.html");
                request.Headers.Add("Cache-Control", "no-cache");
                request.Headers.Add("Pragma", "no-cache");

                // עיכוב אקראי ארוך יותר
                await Task.Delay(_random.Next(2000, 5000));

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"         ❌ שגיאה בהורדה: {response.StatusCode}");

                    // אם קיבלנו 418, ננסה דרך חלופית
                    if ((int)response.StatusCode == 418)
                    {
                        Console.WriteLine($"         🔄 מנסה דרך חלופית (418 detected)...");
                        return await TryAlternativeDownload(fileInfo, typeDir);
                    }

                    return false;
                }

                var fileBytes = await response.Content.ReadAsByteArrayAsync();
                var savedFiles = await ExtractAndSaveXml(fileBytes, fileInfo, typeDir);

                if (savedFiles > 0)
                {
                    Console.WriteLine($"         ✅ נשמרו {savedFiles} קבצי XML");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"         ❌ שגיאה בהורדה: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// ניסיון הורדה חלופי במקרה של חסימת בוט
        /// </summary>
        private async Task<bool> TryAlternativeDownload(WoltFileInfo fileInfo, string typeDir)
        {
            try
            {
                // ניסיון עם מבנה URL שונה
                var alternativeUrls = new[]
                {
                    fileInfo.DownloadUrl.Replace("/download/", "/"),
                    fileInfo.DownloadUrl.Replace($"{WOLT_BASE_URL}/", $"{WOLT_BASE_URL}/files/"),
                    fileInfo.DownloadUrl.Replace("https://wm-gateway.wolt.com/isr-prices/public/v1/",
                                                "https://wm-gateway.wolt.com/isr-prices/public/v1/files/")
                };

                foreach (var altUrl in alternativeUrls)
                {
                    try
                    {
                        Console.WriteLine($"         🔄 מנסה: {altUrl}");

                        // יצירת client חדש עם headers שונים
                        using var altClient = new HttpClient();
                        altClient.DefaultRequestHeaders.Add("User-Agent",
                            "curl/7.68.0"); // מנסה כ-curl במקום דפדפן

                        await Task.Delay(_random.Next(3000, 6000)); // עיכוב ארוך יותר

                        var response = await altClient.GetAsync(altUrl);

                        if (response.IsSuccessStatusCode)
                        {
                            var fileBytes = await response.Content.ReadAsByteArrayAsync();
                            var savedFiles = await ExtractAndSaveXml(fileBytes, fileInfo, typeDir);

                            if (savedFiles > 0)
                            {
                                Console.WriteLine($"         ✅ הצליח דרך חלופית! נשמרו {savedFiles} קבצי XML");
                                return true;
                            }
                        }
                        else
                        {
                            Console.WriteLine($"         ❌ דרך חלופית נכשלה: {response.StatusCode}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"         ⚠️ שגיאה בדרך חלופית: {ex.Message}");
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"         💥 שגיאה בניסיון חלופי: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// חילוץ ושמירת XML
        /// </summary>
        private async Task<int> ExtractAndSaveXml(byte[] fileBytes, WoltFileInfo fileInfo, string typeDir)
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
                    var xmlFileName = fileInfo.FileName.Replace(".gz", ".xml");
                    var xmlPath = Path.Combine(typeDir, xmlFileName);

                    await File.WriteAllTextAsync(xmlPath, xmlContent);
                    savedCount = 1;
                }
                else
                {
                    var xmlFileName = fileInfo.FileName.EndsWith(".xml") ? fileInfo.FileName : fileInfo.FileName + ".xml";
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

        // ========== פונקציות עזר ==========

        private List<string> GetUniqueStores(List<WoltFileInfo> files)
        {
            return files
                .Where(f => !string.IsNullOrEmpty(f.StoreId) && f.FileType != "StoresFull" && f.FileType != "Stores")
                .Select(f => f.StoreId)
                .Distinct()
                .OrderBy(s => s)
                .ToList();
        }

        private string DetermineFileType(string fileName)
        {
            var lowerName = fileName.ToLower();

            if (lowerName.Contains("storesfull")) return "StoresFull";
            if (lowerName.Contains("pricefull")) return "PriceFull";
            if (lowerName.Contains("promofull")) return "PromoFull";
            if (lowerName.Contains("stores")) return "Stores";
            if (lowerName.Contains("price")) return "Price";
            if (lowerName.Contains("promo")) return "Promo";
            return "Unknown";
        }

        private string ExtractStoreFromFileName(string fileName)
        {
            try
            {
                // דפוס לחילוץ מספר סניף: PriceFull_12345_20250725.xml
                var storePattern = @"_(\d+)_";
                var match = Regex.Match(fileName, storePattern);

                if (match.Success)
                {
                    return match.Groups[1].Value;
                }

                // דפוס חלופי: Price-12345-20250725.xml
                var altPattern = @"-(\d+)-";
                var altMatch = Regex.Match(fileName, altPattern);

                if (altMatch.Success)
                {
                    return altMatch.Groups[1].Value;
                }

                return "";
            }
            catch
            {
                return "";
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
    }

    /// <summary>
    /// מנהל קבצים פשוט עבור וולט
    /// </summary>
    public class WoltFileManager
    {
        public void CreateDirectory(string path)
        {
            Directory.CreateDirectory(path);
        }

        public async Task WriteAllBytesAsync(string path, byte[] bytes)
        {
            await File.WriteAllBytesAsync(path, bytes);
        }
    }
}