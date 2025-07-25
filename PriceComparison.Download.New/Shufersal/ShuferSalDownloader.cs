using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using PriceComparison.Download.New.MVP;
using PriceComparison.Download.New.Storage;

namespace PriceComparison.Download.New.Shufersal
{
    /// <summary>
    /// מודל לקובץ ברשת שופרסל - מבוסס על HTML parsing
    /// </summary>
    public class ShuferSalFileInfo
    {
        public string FileName { get; set; } = "";
        public string UpdateTime { get; set; } = "";
        public string Size { get; set; } = "";
        public string FileType { get; set; } = "";
        public string Category { get; set; } = "";
        public string BranchName { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
    }

    /// <summary>
    /// מוריד קבצים מרשת שופרסל - HTML parsing עם תמיכה ב-pagination
    /// </summary>
    public class ShuferSalDownloader
    {
        private readonly HttpClient _httpClient;
        private readonly FileManager _fileManager;
        private readonly Random _random = new();
        private const string BaseDownloadPath = "Downloads";

        // הגדרות ייחודיות לרשת שופרסל
        private const string SHUFERSAL_BASE_URL = "https://prices.shufersal.co.il/";

        public string ChainName => "שופרסל בע\"מ (כולל רשת BE)";
        public string ChainId => "Shufersal";

        public ShuferSalDownloader(HttpClient httpClient, FileManager fileManager)
        {
            _httpClient = httpClient;
            _fileManager = fileManager;
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
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Site", "none");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-User", "?1");

            _httpClient.Timeout = TimeSpan.FromMinutes(10);
        }

        /// <summary>
        /// הורדת כל הקבצים העדכניים - נקודת הכניסה הראשית
        /// </summary>
        public async Task<int> DownloadLatestFiles()
        {
            try
            {
                Console.WriteLine($"🛒 מתחיל הורדת רשת שופרסל...");
                Console.WriteLine($"🎯 מטרה: קבצים עדכניים מ-HTML parsing עם pagination");

                // יצירת תיקיית רשת
                var chainDir = Path.Combine(BaseDownloadPath, "Shufersal");
                Directory.CreateDirectory(chainDir);

                // קבלת כל הקבצים הזמינים מכל העמודים
                var availableFiles = await GetAllAvailableFiles();

                if (!availableFiles.Any())
                {
                    Console.WriteLine($"      ❌ לא נמצאו קבצים זמינים למרות מספר ניסיונות");
                    return 0;
                }

                Console.WriteLine($"      ✅ נמצאו {availableFiles.Count} קבצים זמינים מכל העמודים");

                // ניתוח הקבצים
                AnalyzeAvailableFiles(availableFiles);

                int totalDownloaded = 0;

                // שלב 1: הורדת קבצי Stores
                totalDownloaded += await DownloadStoresFiles(availableFiles, chainDir);

                // שלב 2: זיהוי סניפים
                var stores = GetUniqueStores(availableFiles);
                Console.WriteLine($"      📍 זוהו {stores.Count} סניפים");

                if (stores.Any())
                {
                    // שלב 3: הורדת קבצי מחירים
                    totalDownloaded += await DownloadPriceFiles(availableFiles, stores, chainDir);

                    // שלב 4: הורדת קבצי מבצעים
                    totalDownloaded += await DownloadPromoFiles(availableFiles, stores, chainDir);
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
        /// קבלת רשימת קבצים זמינים על ידי parsing HTML עם תמיכה ב-pagination
        /// </summary>
        private async Task<List<ShuferSalFileInfo>> GetAllAvailableFiles()
        {
            try
            {
                Console.WriteLine($"      🌐 מתחבר לשופרסל עם תמיכה ב-pagination: {SHUFERSAL_BASE_URL}");

                var allFiles = new List<ShuferSalFileInfo>();

                // קריאה לכל העמודים
                for (int page = 1; page <= 90; page++) // שופרסל יכול להיות עד 86 עמודים
                {
                    Console.WriteLine($"      📄 קורא עמוד {page}...");

                    // עיכוב קל למניעת זיהוי בוט
                    await Task.Delay(_random.Next(1000, 3000));

                    var pageUrl = page == 1 ? SHUFERSAL_BASE_URL : $"{SHUFERSAL_BASE_URL}?page={page}";
                    var response = await _httpClient.GetAsync(pageUrl);

                    if (!response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"      ❌ שגיאת HTTP בעמוד {page}: {response.StatusCode}");
                        break; // יוצאים מהלולאה אם העמוד לא קיים
                    }

                    var htmlContent = await response.Content.ReadAsStringAsync();

                    if (string.IsNullOrWhiteSpace(htmlContent))
                    {
                        Console.WriteLine($"      ⚠️ תגובה ריקה מהשרת בעמוד {page}");
                        break;
                    }

                    // בדיקה אם יש תוכן בעמוד
                    var pageFiles = ParseShuferSalHtml(htmlContent);

                    if (pageFiles.Count == 0)
                    {
                        Console.WriteLine($"      ✅ הגענו לסוף העמודים (עמוד {page} ריק)");
                        break;
                    }

                    Console.WriteLine($"      📄 עמוד {page}: נמצאו {pageFiles.Count} קבצים");
                    allFiles.AddRange(pageFiles);

                    // בדיקה אם יש עמוד הבא
                    if (!HasNextPage(htmlContent))
                    {
                        Console.WriteLine($"      ✅ זה העמוד האחרון (עמוד {page})");
                        break;
                    }
                }

                Console.WriteLine($"      ✅ סה\"כ נמצאו {allFiles.Count} קבצים זמינים מכל העמודים");

                // המרה לפורמט המוכר של המערכת וסינון לקבצים עדכניים
                var convertedFiles = ConvertToStandardFormat(allFiles);

                Console.WriteLine($"      🔍 לאחר המרה וסינון: {convertedFiles.Count} קבצים רלוונטיים");

                return convertedFiles;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"      💥 שגיאה בקבלת קבצים: {ex.Message}");
                Console.WriteLine($"      💥 פרטי שגיאה: {ex.StackTrace}");
                return new List<ShuferSalFileInfo>();
            }
        }

        /// <summary>
        /// בדיקה אם יש עמוד הבא
        /// </summary>
        private bool HasNextPage(string htmlContent)
        {
            try
            {
                // חיפוש כפתור "הבא" או pagination
                var nextPagePatterns = new[]
                {
                    @"<a[^>]*data-swhglnk=""true""[^>]*href=""/\?page=(\d+)""[^>]*>&gt;</a>",
                    @"<a[^>]*href=""/\?page=\d+""[^>]*>.*?(הבא|Next|>).*?</a>",
                    @"<a[^>]*class=""[^""]*next[^""]*""[^>]*>",
                    @"href=""/\?page=\d+"""
                };

                foreach (var pattern in nextPagePatterns)
                {
                    if (Regex.IsMatch(htmlContent, pattern, RegexOptions.IgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }
            catch
            {
                return false; // במקרה של ספק, נפסיק
            }
        }

        /// <summary>
        /// פרסור HTML של שופרסל לחילוץ נתוני הקבצים - גרסה משופרת
        /// </summary>
        private List<ShuferSalFileInfo> ParseShuferSalHtml(string htmlContent)
        {
            var files = new List<ShuferSalFileInfo>();

            try
            {
                // שיטה 1: חיפוש טבלת WebGrid הראשית
                files.AddRange(ParseWebGridTable(htmlContent));

                // הסרת כפילויות לפי שם קובץ
                files = files.GroupBy(f => f.FileName)
                            .Select(g => g.OrderByDescending(x => ParseUpdateTimeForSorting(x.UpdateTime)).First())
                            .ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"      ⚠️ שגיאה בפרסור HTML: {ex.Message}");
            }

            return files;
        }

        /// <summary>
        /// פרסור טבלת WebGrid הראשית של שופרסל
        /// </summary>
        private List<ShuferSalFileInfo> ParseWebGridTable(string htmlContent)
        {
            var files = new List<ShuferSalFileInfo>();

            try
            {
                // חיפוש שורות בטבלת webgrid
                var rowPattern = @"<tr[^>]*class=""(?:webgrid-row-style|webgrid-alternating-row)""[^>]*>(.*?)</tr>";
                var rows = Regex.Matches(htmlContent, rowPattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);

                foreach (Match row in rows)
                {
                    var rowHtml = row.Groups[1].Value;

                    // חילוץ תאים מהשורה
                    var cellPattern = @"<td[^>]*>(.*?)</td>";
                    var cells = Regex.Matches(rowHtml, cellPattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);

                    if (cells.Count >= 7) // לפחות 7 עמודות כפי שראינו ב-HTML
                    {
                        // מיפוי העמודות לפי הטבלה שראינו:
                        // הורדה, זמן עידכון, גודל, סוג קובץ, קטגוריה, סניף, שם
                        var downloadCell = cells[0].Groups[1].Value;
                        var updateTimeCell = cells[1].Groups[1].Value;
                        var sizeCell = cells[2].Groups[1].Value;
                        var fileTypeCell = cells[3].Groups[1].Value;
                        var categoryCell = cells[4].Groups[1].Value;
                        var branchCell = cells[5].Groups[1].Value;
                        var nameCell = cells[6].Groups[1].Value;

                        // חילוץ נתונים
                        var downloadUrl = ExtractDownloadUrl(downloadCell);
                        var fileName = CleanHtmlText(nameCell);
                        var updateTime = CleanHtmlText(updateTimeCell);
                        var size = CleanHtmlText(sizeCell);
                        var fileType = CleanHtmlText(fileTypeCell);
                        var category = CleanHtmlText(categoryCell);
                        var branchName = CleanHtmlText(branchCell);

                        if (!string.IsNullOrEmpty(fileName) && !string.IsNullOrEmpty(downloadUrl))
                        {
                            files.Add(new ShuferSalFileInfo
                            {
                                FileName = fileName,
                                UpdateTime = updateTime,
                                Size = size,
                                FileType = fileType,
                                Category = category,
                                BranchName = branchName,
                                DownloadUrl = downloadUrl
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"      ⚠️ שגיאה בפרסור טבלת WebGrid: {ex.Message}");
            }

            return files;
        }

        /// <summary>
        /// חילוץ קישור הורדה - גרסה משופרת לשופרסל
        /// </summary>
        private string ExtractDownloadUrl(string cellContent)
        {
            try
            {
                var patterns = new[]
                {
                    @"href=""([^""]+)""",
                    @"href='([^']+)'",
                    @"onclick=""[^""]*window\.open\(['""]([^'""]+)['""]\)",
                    @"data-url=""([^""]+)"""
                };

                foreach (var pattern in patterns)
                {
                    var match = Regex.Match(cellContent, pattern, RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        var url = match.Groups[1].Value;

                        // תיקון HTML encoding - החלפת &amp; ב-&
                        url = HttpUtility.HtmlDecode(url);

                        // בשופרסל הקישורים מגיעים מוכנים - לא צריך תיקון נוסף
                        return url;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"      ⚠️ שגיאה בחילוץ URL: {ex.Message}");
            }

            return "";
        }

        /// <summary>
        /// ניקוי טקסט HTML
        /// </summary>
        private string CleanHtmlText(string html)
        {
            if (string.IsNullOrEmpty(html))
                return "";

            try
            {
                // הסרת תגי HTML
                var text = Regex.Replace(html, @"<[^>]+>", "");

                // decode HTML entities
                text = HttpUtility.HtmlDecode(text);

                // ניקוי רווחים
                text = text.Trim();

                return text;
            }
            catch
            {
                return html.Trim();
            }
        }

        /// <summary>
        /// המרת קבצים מפורמט שופרסל לפורמט אחיד
        /// </summary>
        private List<ShuferSalFileInfo> ConvertToStandardFormat(List<ShuferSalFileInfo> shuferSalFiles)
        {
            var result = new List<ShuferSalFileInfo>();

            // קבלת התאריך של היום
            var today = DateTime.Now;

            Console.WriteLine($"      🔍 בודק {shuferSalFiles.Count} קבצים לתאריך היום: {today:dd/MM/yyyy}");

            // דיבוג - הצגת כמה דוגמאות של תאריכים
            var sampleDates = shuferSalFiles.Take(5).Select(f => f.UpdateTime).ToList();
            Console.WriteLine($"      📅 דוגמאות תאריכים מהשרת: {string.Join(", ", sampleDates)}");

            foreach (var file in shuferSalFiles)
            {
                try
                {
                    // בדיקת תאריך - האם הקובץ מהיום (גרסה מורחבת)
                    if (IsFromTodayExtended(file.UpdateTime, today))
                    {
                        result.Add(file);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"      ⚠️ שגיאה בהמרת קובץ {file.FileName}: {ex.Message}");
                }
            }

            Console.WriteLine($"      ✅ אחרי סינון: {result.Count} קבצים מהיום");

            // מיון לפי זמן עדכון (העדכניים ראשונים)
            return result.OrderByDescending(f => ParseUpdateTimeForSorting(f.UpdateTime)).ToList();
        }

        /// <summary>
        /// בדיקה האם הקובץ מהיום - גרסה מורחבת עם תמיכה בפורמטים שונים
        /// </summary>
        private bool IsFromTodayExtended(string updateTimeStr, DateTime today)
        {
            if (string.IsNullOrEmpty(updateTimeStr))
                return false;

            try
            {
                // ניסיון 1: פרסור ישיר
                if (DateTime.TryParse(updateTimeStr, out var updateTime))
                {
                    bool isToday = updateTime.Date == today.Date;
                    if (isToday)
                    {
                        Console.WriteLine($"         ✅ קובץ מהיום: {updateTimeStr} -> {updateTime:dd/MM/yyyy}");
                    }
                    return isToday;
                }

                // ניסיון 2: פורמטים ספציפיים
                var formats = new[]
                {
                    "M/d/yyyy h:mm:ss tt",          // 7/23/2025 9:00:00 PM
                    "MM/dd/yyyy h:mm:ss tt",        // 07/23/2025 9:00:00 PM
                    "d/M/yyyy h:mm:ss tt",          // 23/7/2025 9:00:00 PM
                    "dd/MM/yyyy h:mm:ss tt",        // 23/07/2025 9:00:00 PM
                    "M/d/yyyy HH:mm:ss",            // 7/23/2025 21:00:00
                    "MM/dd/yyyy HH:mm:ss",          // 07/23/2025 21:00:00
                    "d/M/yyyy HH:mm:ss",            // 23/7/2025 21:00:00
                    "dd/MM/yyyy HH:mm:ss",          // 23/07/2025 21:00:00
                    "yyyy-MM-dd HH:mm:ss",          // 2025-07-23 21:00:00
                    "yyyy-MM-dd h:mm:ss tt",        // 2025-07-23 9:00:00 PM
                    "M/d/yyyy",                     // 7/23/2025
                    "MM/dd/yyyy",                   // 07/23/2025
                    "d/M/yyyy",                     // 23/7/2025
                    "dd/MM/yyyy",                   // 23/07/2025
                    "yyyy-MM-dd"                    // 2025-07-23
                };

                foreach (var format in formats)
                {
                    if (DateTime.TryParseExact(updateTimeStr, format, null, System.Globalization.DateTimeStyles.None, out updateTime))
                    {
                        bool isToday = updateTime.Date == today.Date;
                        if (isToday)
                        {
                            Console.WriteLine($"         ✅ קובץ מהיום (פורמט {format}): {updateTimeStr} -> {updateTime:dd/MM/yyyy}");
                        }
                        return isToday;
                    }
                }

                // ניסיון 3: חיפוש תאריך בתוך המחרוזת
                var todayFormats = new[]
                {
                    today.ToString("M/d/yyyy"),     // 7/23/2025
                    today.ToString("MM/dd/yyyy"),   // 07/23/2025
                    today.ToString("d/M/yyyy"),     // 23/7/2025
                    today.ToString("dd/MM/yyyy"),   // 23/07/2025
                    today.ToString("yyyy-MM-dd")    // 2025-07-23
                };

                foreach (var todayFormat in todayFormats)
                {
                    if (updateTimeStr.Contains(todayFormat))
                    {
                        Console.WriteLine($"         ✅ קובץ מהיום (מחרוזת): {updateTimeStr} מכיל {todayFormat}");
                        return true;
                    }
                }

                // אם הגענו עד כאן, הקובץ לא מהיום או שלא הצלחנו לפרס
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"         ⚠️ שגיאה בפרסור תאריך {updateTimeStr}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// המרת זמן עדכון למיון
        /// </summary>
        private DateTime ParseUpdateTimeForSorting(string updateTimeStr)
        {
            try
            {
                if (DateTime.TryParse(updateTimeStr, out var updateTime))
                {
                    return updateTime;
                }

                return DateTime.MinValue;
            }
            catch
            {
                return DateTime.MinValue;
            }
        }

        /// <summary>
        /// ניתוח קבצים זמינים
        /// </summary>
        private void AnalyzeAvailableFiles(List<ShuferSalFileInfo> files)
        {
            var types = files.GroupBy(f => DetermineFileType(f.FileName, f.Category)).ToDictionary(g => g.Key, g => g.Count());
            Console.WriteLine($"      🔍 ניתוח קבצים משופרסל:");
            foreach (var type in types)
                Console.WriteLine($"         📄 {type.Key}: {type.Value}");
        }

        /// <summary>
        /// הורדת קבצי Stores
        /// </summary>
        private async Task<int> DownloadStoresFiles(List<ShuferSalFileInfo> availableFiles, string chainDir)
        {
            Console.WriteLine($"      📋 מחפש קבצי Stores...");

            // חיפוש קבצי Stores לפי קטגוריה וגם לפי שם
            var storesFiles = availableFiles
                .Where(f => f.Category.ToLower() == "stores" ||
                           f.FileName.ToLower().Contains("stores") ||
                           f.FileName.ToLower().Contains("7290027600007-001"))  // קוד שופרסל הכללי
                .OrderByDescending(f => f.FileName.ToLower().Contains("storesfull") ? 1 : 0)
                .ThenByDescending(f => ParseUpdateTimeForSorting(f.UpdateTime))
                .ToList();

            Console.WriteLine($"      🔍 Debug - מחפש Stores בקטגוריות:");
            var categorySample = availableFiles.Take(10).Select(f => $"{f.FileName}: {f.Category}").ToList();
            foreach (var sample in categorySample)
                Console.WriteLine($"         {sample}");

            if (!storesFiles.Any())
            {
                Console.WriteLine($"      ⚠️ לא נמצאו קבצי Stores בחיפוש ראשוני");

                // חיפוש חלופי - כל קובץ שלא שייך לסניף ספציפי
                var generalFiles = availableFiles
                    .Where(f => !Regex.IsMatch(f.FileName, @"-\d{3}-"))  // לא מכיל -XXX- (מספר סניף)
                    .ToList();

                Console.WriteLine($"      🔍 חיפוש חלופי - קבצים כלליים: {generalFiles.Count}");

                if (generalFiles.Any())
                {
                    var latestGeneral = generalFiles.OrderByDescending(f => ParseUpdateTimeForSorting(f.UpdateTime)).First();
                    Console.WriteLine($"      🎯 מוריד קובץ כללי כ-Stores: {latestGeneral.FileName}");

                    var success = await DownloadAndSaveFile(latestGeneral, chainDir, "Stores");
                    return success ? 1 : 0;
                }

                Console.WriteLine($"      ❌ לא נמצאו קבצי Stores גם בחיפוש חלופי");
                return 0;
            }

            var latestStores = storesFiles.First();
            Console.WriteLine($"      🎯 מוריד: {latestStores.FileName}");

            var storesSuccess = await DownloadAndSaveFile(latestStores, chainDir, "Stores");
            return storesSuccess ? 1 : 0;
        }

        /// <summary>
        /// הורדת קבצי מחירים עם retry
        /// </summary>
        private async Task<int> DownloadPriceFiles(List<ShuferSalFileInfo> availableFiles, List<string> stores, string chainDir)
        {
            Console.WriteLine($"      💰 מוריד קבצי Price...");

            int downloaded = 0;

            // הגבלה ל-5 סניפים לבדיקה
            var limitedStores = stores.Take(5).ToList();
            Console.WriteLine($"      🔍 מגביל ל-{limitedStores.Count} סניפים לבדיקה: {string.Join(", ", limitedStores)}");

            foreach (var store in limitedStores)
            {
                // חיפוש קבצי PriceFull
                var priceFullFiles = availableFiles
                    .Where(f => (f.Category.ToLower() == "pricesfull" || f.FileName.ToLower().Contains("pricefull")) &&
                               ExtractStoreFromBranch(f.BranchName) == store)
                    .OrderByDescending(f => ParseUpdateTimeForSorting(f.UpdateTime))
                    .ToList();

                // חיפוש קבצי Price רגיל
                var priceFiles = availableFiles
                    .Where(f => (f.Category.ToLower() == "prices" || f.Category.ToLower() == "price") &&
                               !f.FileName.ToLower().Contains("pricefull") &&
                               ExtractStoreFromBranch(f.BranchName) == store)
                    .OrderByDescending(f => ParseUpdateTimeForSorting(f.UpdateTime))
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
        /// הורדת קבצי מבצעים עם retry
        /// </summary>
        private async Task<int> DownloadPromoFiles(List<ShuferSalFileInfo> availableFiles, List<string> stores, string chainDir)
        {
            Console.WriteLine($"      🎁 מחפש קבצי Promo...");

            int downloaded = 0;

            // הגבלה ל-5 סניפים לבדיקה
            var limitedStores = stores.Take(5).ToList();

            foreach (var store in limitedStores)
            {
                // חיפוש קבצי PromoFull
                var promoFullFiles = availableFiles
                    .Where(f => (f.Category.ToLower() == "promosfull" || f.FileName.ToLower().Contains("promofull")) &&
                               ExtractStoreFromBranch(f.BranchName) == store)
                    .OrderByDescending(f => ParseUpdateTimeForSorting(f.UpdateTime))
                    .ToList();

                // חיפוש קבצי Promo רגיל
                var promoFiles = availableFiles
                    .Where(f => (f.Category.ToLower() == "promos" || f.Category.ToLower() == "promo") &&
                               !f.FileName.ToLower().Contains("promofull") &&
                               ExtractStoreFromBranch(f.BranchName) == store)
                    .OrderByDescending(f => ParseUpdateTimeForSorting(f.UpdateTime))
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
        private async Task<bool> DownloadAndSaveFileWithRetry(ShuferSalFileInfo fileInfo, string chainDir, string fileType, int maxRetries = 3)
        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    if (attempt > 1)
                    {
                        Console.WriteLine($"         🔄 ניסיון {attempt}/{maxRetries}: {fileInfo.FileName}");
                        await Task.Delay(_random.Next(2000, 5000)); // עיכוב מוגדל בין ניסיונות
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
        /// הורדה ושמירת קובץ - גרסה מיוחדת לשופרסל
        /// </summary>
        private async Task<bool> DownloadAndSaveFile(ShuferSalFileInfo fileInfo, string chainDir, string fileType)
        {
            try
            {
                var typeDir = Path.Combine(chainDir, fileType);
                Directory.CreateDirectory(typeDir);

                Console.WriteLine($"         📥 מוריד מ: {fileInfo.DownloadUrl}");

                var response = await _httpClient.GetAsync(fileInfo.DownloadUrl);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"         ❌ שגיאה בהורדה: {response.StatusCode}");
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
        /// חילוץ ושמירת XML
        /// </summary>
        private async Task<int> ExtractAndSaveXml(byte[] fileBytes, ShuferSalFileInfo fileInfo, string typeDir)
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

        private List<string> GetUniqueStores(List<ShuferSalFileInfo> files)
        {
            return files
                .Where(f => f.Category.ToLower().Contains("price") || f.Category.ToLower().Contains("promo"))
                .Select(f => ExtractStoreFromBranch(f.BranchName))
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct()
                .OrderBy(s => s)
                .ToList();
        }

        private string DetermineFileType(string fileName, string category)
        {
            var lowerName = fileName.ToLower();
            var lowerCategory = category.ToLower();

            if (lowerCategory.Contains("stores") || lowerName.Contains("stores")) return "Stores";
            if (lowerCategory.Contains("pricesfull") || lowerName.Contains("pricefull")) return "PriceFull";
            if (lowerCategory.Contains("promosfull") || lowerName.Contains("promofull")) return "PromoFull";
            if (lowerCategory.Contains("prices") || lowerName.Contains("price")) return "Price";
            if (lowerCategory.Contains("promos") || lowerName.Contains("promo")) return "Promo";
            return "Unknown";
        }

        private string ExtractStoreFromBranch(string branchName)
        {
            try
            {
                if (string.IsNullOrEmpty(branchName))
                    return "";

                // חילוץ מספר הסניף מתחילת השם: "1 - שלי ת"א- בן יהודה"
                var parts = branchName.Split('-');
                if (parts.Length >= 1)
                {
                    var storePart = parts[0].Trim();
                    if (int.TryParse(storePart, out var storeNumber))
                    {
                        return storeNumber.ToString();
                    }
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
}