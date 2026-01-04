
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
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

                var chainDir = Path.Combine(BaseDownloadPath, "Shufersal");
                Directory.CreateDirectory(chainDir);

                int totalDownloaded = 0;
                int page = 1;

                while (true)
                {
                    Console.WriteLine($"      📄 קורא עמוד {page}...");
                    var pageUrl = page == 1 ? SHUFERSAL_BASE_URL : $"{SHUFERSAL_BASE_URL}?page={page}";
                    var response = await _httpClient.GetAsync(pageUrl);

                    if (!response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"      ❌ שגיאת HTTP בעמוד {page}: {response.StatusCode}");
                        break;
                    }

                    var htmlContent = await response.Content.ReadAsStringAsync();

                    if (string.IsNullOrWhiteSpace(htmlContent))
                    {
                        Console.WriteLine($"      ⚠️ תגובה ריקה מהשרת בעמוד {page}");
                        break;
                    }

                    var pageFiles = ParseShuferSalHtml(htmlContent);

                    if (!pageFiles.Any())
                    {
                        Console.WriteLine($"      ✅ עמוד {page} ריק — אין יותר קבצים.");
                        break;
                    }

                    Console.WriteLine($"      📄 עמוד {page}: נמצאו {pageFiles.Count} קבצים");

                    // ניתוח הקבצים מהעמוד הנוכחי
                    AnalyzeAvailableFiles(pageFiles);

                    // הורדת Stores
                    totalDownloaded += await DownloadStoresFiles(pageFiles, chainDir);

                    // זיהוי סניפים
                    var stores = GetUniqueStores(pageFiles);

                    if (stores.Any())
                    {
                        // הורדת Price
                        totalDownloaded += await DownloadPriceFiles(pageFiles, stores, chainDir);

                        // הורדת Promo
                        totalDownloaded += await DownloadPromoFiles(pageFiles, stores, chainDir);
                    }

                    // בדיקה אם יש עמוד הבא
                    if (!HasNextPage(htmlContent))
                    {
                        Console.WriteLine($"      ✅ הגענו לעמוד האחרון ({page})");
                        break;
                    }

                    page++;
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
        //private async Task<List<ShuferSalFileInfo>> GetAllAvailableFiles()
        //{
        //    try
        //    {
        //        Console.WriteLine($"      🌐 מתחבר לשופרסל עם תמיכה ב-pagination: {SHUFERSAL_BASE_URL}");

        //        var allFiles = new List<ShuferSalFileInfo>();
        //        int page = 1;

        //        while (true)
        //        {
        //            Console.WriteLine($"      📄 קורא עמוד {page}...");

        //            // עיכוב קל למניעת זיהוי בוט
        //            //await Task.Delay(_random.Next(200, 500));

        //            var pageUrl = page == 1 ? SHUFERSAL_BASE_URL : $"{SHUFERSAL_BASE_URL}?page={page}";
        //            var response = await _httpClient.GetAsync(pageUrl);

        //            if (!response.IsSuccessStatusCode)
        //            {
        //                Console.WriteLine($"      ❌ שגיאת HTTP בעמוד {page}: {response.StatusCode}");
        //                break; // אין טעם להמשיך אם השרת לא מחזיר תקין
        //            }

        //            var htmlContent = await response.Content.ReadAsStringAsync();

        //            if (string.IsNullOrWhiteSpace(htmlContent))
        //            {
        //                Console.WriteLine($"      ⚠️ תגובה ריקה מהשרת בעמוד {page}");
        //                break;
        //            }

        //            var pageFiles = ParseShuferSalHtml(htmlContent);

        //            if (pageFiles.Count == 0)
        //            {
        //                Console.WriteLine($"      ✅ עמוד {page} ריק — אין יותר קבצים.");
        //                break;
        //            }

        //            Console.WriteLine($"      📄 עמוד {page}: נמצאו {pageFiles.Count} קבצים");
        //            allFiles.AddRange(pageFiles);

        //            // בדיקה אם יש עמוד הבא
        //            if (!HasNextPage(htmlContent))
        //            {
        //                Console.WriteLine($"      ✅ הגענו לעמוד האחרון ({page})");
        //                break;
        //            }

        //            page++;
        //        }

        //        Console.WriteLine($"      ✅ סה\"כ נמצאו {allFiles.Count} קבצים זמינים מכל העמודים");

        //        var convertedFiles = ConvertToStandardFormat(allFiles);

        //        Console.WriteLine($"      🔍 לאחר המרה וסינון: {convertedFiles.Count} קבצים רלוונטיים");

        //        return convertedFiles;
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"      💥 שגיאה בקבלת קבצים: {ex.Message}");
        //        Console.WriteLine($"      💥 פרטי שגיאה: {ex.StackTrace}");
        //        return new List<ShuferSalFileInfo>();
        //    }
        //}


        /// <summary>
        /// בדיקה אם יש עמוד הבא
        /// </summary>
        private bool HasNextPage(string htmlContent)
        {
            try
            {
                // ננקה רווחים מיותרים ונמיר לאותיות קטנות
                var clean = htmlContent.Replace(" ", "").ToLowerInvariant();

                // נחפש אם יש קישור אמיתי לעמוד הבא (כלומר: href עם page=)
                // אבל נוודא שזה לא "כפתור מושבת" (ללא href או עם class=disabled)
                var hasActiveNextLink = Regex.IsMatch(
                    clean,
                    @"<a[^>]+href=.*?page=\d+[^>]*>(?:&gt;|>)<\/a>",
                    RegexOptions.IgnoreCase
                );

                return hasActiveNextLink;
            }
            catch
            {
                return false;
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
                //files = files.GroupBy(f => f.FileName)
                //            .Select(g => g.OrderByDescending(x => ParseUpdateTimeForSorting(x.UpdateTime)).First())
                //            .ToList();
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
                    //var cellPattern = @"<td[^>]*>(.*?)</td>";
                    //var cells = Regex.Matches(rowHtml, cellPattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);

                    //if (cells.Count >= 7) // לפחות 7 עמודות כפי שראינו ב-HTML
                    //{
                    //    // מיפוי העמודות לפי הטבלה שראינו:
                    //    // הורדה, זמן עידכון, גודל, סוג קובץ, קטגוריה, סניף, שם
                    //    var downloadCell = cells[0].Groups[1].Value;
                    //    var updateTimeCell = cells[1].Groups[1].Value;
                    //    var sizeCell = cells[2].Groups[1].Value;
                    //    var fileTypeCell = cells[3].Groups[1].Value;
                    //    var categoryCell = cells[4].Groups[1].Value;
                    //    var branchCell = cells[5].Groups[1].Value;
                    //    var nameCell = cells[6].Groups[1].Value;

                    var cellPattern = @"<td[^>]*>\s*(.*?)\s*</td>";
                    var cells = Regex.Matches(rowHtml, cellPattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);

                    if (cells.Count >= 7)
                    {
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
            return result
.GroupBy(f => new { f.Category, f.BranchName })           // קיבוץ לפי קטגוריה + סניף
.Select(g => g.OrderByDescending(f => ParseUpdateTimeForSorting(f.UpdateTime)).First())
.ToList();
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
                if (DateTime.TryParse(updateTimeStr, new System.Globalization.CultureInfo("en-US"),
                      System.Globalization.DateTimeStyles.None, out var updateTime))

                {
                    var nowDate = DateTime.Now.Date;
                    bool isToday = updateTime.ToLocalTime().Date == nowDate;
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

        private async Task<int> DownloadStoresFiles(List<ShuferSalFileInfo> availableFiles, string chainDir)
        {
            Console.WriteLine($"      📋 מחפש קבצי Stores...");

            // חיפוש קבצי Stores לפי קטגוריה וגם לפי שם
            var storesFiles = availableFiles
                .Where(f => f.Category.ToLower() == "stores" ||
                            f.FileName.ToLower().Contains("stores"))
                .OrderByDescending(f => f.FileName.ToLower().Contains("storesfull") ? 1 : 0)
                .ThenByDescending(f => ParseUpdateTimeForSorting(f.UpdateTime))
                .ToList();

            //Console.WriteLine($"      🔍 Debug - מחפש Stores בקטגוריות:");
            //foreach (var sample in availableFiles.Take(10))
            //    Console.WriteLine($"         {sample.FileName}: {sample.Category}");

            // אם לא נמצאו קבצי Stores רגילים
            if (!storesFiles.Any())
            {
                Console.WriteLine($"      ⚠️ לא נמצאו קבצי Stores בחיפוש ראשוני");

                // חיפוש חלופי - קבצים כלליים שלא משויכים לסניף ספציפי
                var generalFiles = availableFiles
                    .Where(f => !Regex.IsMatch(f.FileName, @"-\d{3}-")) // לא מכיל -XXX-
                    .ToList();

                Console.WriteLine($"      🔍 חיפוש חלופי - קבצים כלליים: {generalFiles.Count}");

                if (generalFiles.Any())
                {
                    var latestGeneral = generalFiles
                        .OrderByDescending(f => ParseUpdateTimeForSorting(f.UpdateTime))
                        .First();

                    Console.WriteLine($"      🎯 מוריד קובץ כללי כ-Stores: {latestGeneral.FileName}");

                    var success = await DownloadAndSaveFileWithRetry(latestGeneral, chainDir, "Stores");
                    return success ? 1 : 0;
                }

                Console.WriteLine($"      ❌ לא נמצאו קבצי Stores גם בחיפוש חלופי");
                return 0;
            }

            // אם נמצאו קבצי Stores — נוריד את העדכני ביותר
            var latestStores = storesFiles.First();
            Console.WriteLine($"      🎯 מוריד: {latestStores.FileName}");

            var storesSuccess = await DownloadAndSaveFileWithRetry(latestStores, chainDir, "Stores");
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
            var limitedStores = stores.ToList();
            Console.WriteLine($"      🔍 מגביל ל-{limitedStores.Count} סניפים לבדיקה: {string.Join(", ", limitedStores)}");

            foreach (var store in limitedStores)
            {
                // חיפוש קבצי PriceFull
                var priceFullFiles = availableFiles
                    .Where(f => (f.Category.ToLower() == "pricesfull" || f.FileName.ToLower().Contains("pricefull")) &&
                               ExtractStoreFromBranch(f.BranchName) == store)
                    .OrderByDescending(f => ParseUpdateTimeForSorting(f.UpdateTime))
                    .ToList();

                //חיפוש קבצי Price רגיל
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

                    //await Task.Delay(_random.Next(100, 600));
                    var success = await DownloadAndSaveFileWithRetry(latestPriceFull, chainDir, "PriceFull");
                    if (success) downloaded++;
                }

                //הורדת Price רגיל אם קיים
                if (priceFiles.Any())
                {
                    var latestPrice = priceFiles.First();
                    Console.WriteLine($"         🎯 סניף {store} Price: {latestPrice.FileName}");

                    //await Task.Delay(_random.Next(100, 600));
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
            var limitedStores = stores.ToList();

            foreach (var store in limitedStores)
            {
                // חיפוש קבצי PromoFull
                var promoFullFiles = availableFiles
                    .Where(f => (f.Category.ToLower() == "promosfull" || f.FileName.ToLower().Contains("promofull")) &&
                               ExtractStoreFromBranch(f.BranchName) == store)
                    .OrderByDescending(f => ParseUpdateTimeForSorting(f.UpdateTime))
                    .ToList();

                //חיפוש קבצי Promo רגיל
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

                    //await Task.Delay(_random.Next(100, 600));
                    var success = await DownloadAndSaveFileWithRetry(latestPromoFull, chainDir, "PromoFull");
                    if (success) downloaded++;
                }

                //הורדת Promo רגיל אם קיים
                if (promoFiles.Any())
                {
                    var latestPromo = promoFiles.First();
                    Console.WriteLine($"         🎯 סניף {store} Promo: {latestPromo.FileName}");

                    //await Task.Delay(_random.Next(100, 600));
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

        private async Task<bool> DownloadAndSaveFileWithRetry(ShuferSalFileInfo fileInfo, string chainDir, string fileType, int maxRetries = 3)
        {
            try
            {
                // חילוץ storeId (מ־BranchName אם קיים, אחרת מחלוץ מה־FileName)
                var storeId = ExtractStoreFromBranch(fileInfo.BranchName);
                if (string.IsNullOrEmpty(storeId))
                {
                    var m = Regex.Match(fileInfo.FileName ?? "", @"-(\d{1,})-"); // תומך גם ביותר מ-3 ספרות
                    if (m.Success)
                        storeId = m.Groups[1].Value;
                }

                // 🧩 נרמול storeId – אם קצר מ־3 ספרות מוסיף אפסים משמאל
                if (!string.IsNullOrEmpty(storeId))
                {
                    if (storeId.Length < 3)
                        storeId = storeId.PadLeft(3, '0');

                    Console.WriteLine($"         🏪 storeId לאחר נרמול: {storeId}");
                }

                var typeDir = Path.Combine(chainDir, fileType);
                Directory.CreateDirectory(typeDir);

                bool isGeneralFile = (fileInfo.FileName ?? "").IndexOf("Stores", StringComparison.OrdinalIgnoreCase) >= 0;

                // pattern גמיש: יתפוס xml/gz/כל סיומת
                string searchPattern = isGeneralFile
                    ? $"{fileType}*.*"
                    : $"*-{storeId}-*.*";

                Console.WriteLine($"         🔎 מחפש קבצים ב: {typeDir}, pattern: {searchPattern}");

                var existingFiles = Directory.GetFiles(typeDir, searchPattern).ToList();
                Console.WriteLine($"         🔎 נמצאו {existingFiles.Count} קבצים תואמים");

                var newDate = ParseUpdateTimeForSorting("", fileInfo.FileName ?? "");
                Console.WriteLine($"         🔎 newDate = {(newDate == DateTime.MinValue ? "MinValue" : newDate.ToString("yyyy-MM-dd HH:mm"))}");

                if (existingFiles.Any())
                {
                    if (newDate == DateTime.MinValue)
                    {
                        Console.WriteLine($"         🌐 תאריך הקובץ מהאתר (newDate): {(newDate == DateTime.MinValue ? "לא זוהה" : newDate.ToString("yyyy-MM-dd HH:mm"))}");
                        return false;
                    }

                    bool shouldDownload = false;
                    foreach (var ef in existingFiles)
                    {
                        var existingDate = ExtractDateFromFileName(ef);
                        Console.WriteLine($"             מצא קיים: {Path.GetFileName(ef)} -> existingDate = {(existingDate == DateTime.MinValue ? "MinValue" : existingDate.ToString("yyyy-MM-dd HH:mm"))}");

                        if (existingDate == DateTime.MinValue)
                        {
                            Console.WriteLine("             ⚠️ existingDate לא זוהה — נמשיך לבדוק את הקבצים האחרים.");
                            continue;
                        }

                        if (newDate > existingDate)
                        {
                            Console.WriteLine($"             ✅ newDate ({newDate:yyyy-MM-dd HH:mm}) חדש יותר מ-existingDate ({existingDate:yyyy-MM-dd HH:mm})");
                            shouldDownload = true;
                            break;
                        }
                        else
                        {
                            Console.WriteLine($"             ⏭️ newDate ({newDate:yyyy-MM-dd HH:mm}) אינו חדש יותר מ-existingDate ({existingDate:yyyy-MM-dd HH:mm}) — דילג.");
                        }
                    }

                    if (!existingFiles.Any() || !existingFiles.Any(f => ExtractDateFromFileName(f) != DateTime.MinValue))
                    {
                        Console.WriteLine("             ⚠️ לא נמצאו תאריכים תקינים בקבצים — נוריד מחדש.");
                        shouldDownload = true;
                    }

                    if (!shouldDownload)
                        return false;
                }

                // הורדה עם retry
                for (int attempt = 1; attempt <= maxRetries; attempt++)
                {
                    try
                    {
                        if (attempt > 1)
                        {
                            Console.WriteLine($"         🔄 ניסיון {attempt}/{maxRetries}: {fileInfo.FileName}");
                            await Task.Delay(_random.Next(100, 600));
                        }

                        var success = await DownloadAndSaveFile(fileInfo, chainDir, fileType);
                        if (success)
                        {
                            Console.WriteLine($"         ✅ הורדה הושלמה ונשמרה: {fileInfo.FileName}");
                            foreach (var ef in existingFiles)
                            {
                                try
                                {
                                    Console.WriteLine($"             🔁 מוחק ישן: {Path.GetFileName(ef)}");
                                    File.Delete(ef);
                                }
                                catch (Exception exDel)
                                {
                                    Console.WriteLine($"             ⚠️ שגיאה במחיקה של {Path.GetFileName(ef)}: {exDel.Message}");
                                }
                            }
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"         ⚠️ ניסיון {attempt} נכשל: {ex.Message}");
                        if (attempt == maxRetries)
                            Console.WriteLine($"         ❌ נכשל לאחר {maxRetries} ניסיונות: {fileInfo.FileName}");
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"         ❌ שגיאה ב־DownloadAndSaveFileWithRetry: {ex.Message}");
                return false;
            }
        }


        private DateTime ParseUpdateTimeForSorting(string updateTime, string fileName)
        {
            // ננסה קודם כל לקרוא מה־updateTime (אם הוא במבנה תקין)
            if (!string.IsNullOrEmpty(updateTime) &&
                DateTime.TryParseExact(updateTime, "yyyyMMddHHmm", null,
                    System.Globalization.DateTimeStyles.None, out var parsed))
            {
                return parsed;
            }

            // אם לא הצלחנו – ננסה להוציא את התאריך מתוך שם הקובץ
            var dateFromFileName = ExtractDateFromFileName(fileName);
            if (dateFromFileName != DateTime.MinValue)
                return dateFromFileName;

            // אם גם זה לא הצליח – נחזיר תאריך ישן כדי לא לפספס עדכונים
            return DateTime.MinValue;
        }

        private DateTime ExtractDateFromFileName(string fileName)
        {
            try
            {
                // חילוץ החלק האחרון אחרי '-' האחרון
                var parts = fileName.Split('-');
                if (parts.Length >= 3)
                {
                    var dateStr = parts[parts.Length - 1].Replace(".gz", "").Replace(".xml", "");

                    if (DateTime.TryParseExact(dateStr, "yyyyMMddHHmm", null,
                        System.Globalization.DateTimeStyles.None, out var dt))
                    {
                        return dt;
                    }
                }
            }
            catch
            {
                // במקרה של שגיאה נחזיר MinValue
            }

            return DateTime.MinValue;
        }

        //private DateTime ExtractDateFromFileName(string fileName)
        //{
        //    // מחפש את ה־12 ספרות האחרונות (yyyyMMddHHmm)
        //    var match = Regex.Match(fileName, @"(\d{12})(?:\.\w+)?$");
        //    if (match.Success && DateTime.TryParseExact(
        //        match.Value,
        //        "yyyyMMddHHmm",
        //        null,
        //        System.Globalization.DateTimeStyles.None,
        //        out var dt))
        //    {
        //        Console.WriteLine("נכנס");
        //        return dt;
        //    }

        //    try
        //    {
        //        if (File.Exists(fileName))
        //            return File.GetLastWriteTime(fileName);
        //    }
        //    catch
        //    {
        //        // אם יש בעיה בגישה לקובץ, נחזיר ערך ישן
        //    }

        //    return DateTime.MinValue;
        //}





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
                string baseName = Path.GetFileNameWithoutExtension(fileInfo.FileName);  // שם בסיסי לכל הקבצים

                // --- טיפול בקובץ ZIP ---
                if (IsZipFile(fileBytes))
                {
                    using var zipStream = new MemoryStream(fileBytes);
                    using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

                    foreach (var entry in archive.Entries)
                    {
                        if (!string.IsNullOrEmpty(entry.Name) &&
                            entry.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                        {
                            string xmlName = baseName + ".xml";   // תמיד שמור בשם XML
                            string xmlPath = Path.Combine(typeDir, xmlName);

                            entry.ExtractToFile(xmlPath, true);
                            savedCount++;
                        }
                    }

                    return savedCount;
                }

                // --- טיפול בקובץ GZ ---
                if (IsGzFile(fileBytes))
                {
                    using var gzStream = new MemoryStream(fileBytes);
                    using var decompressionStream = new GZipStream(gzStream, CompressionMode.Decompress);
                    using var reader = new StreamReader(decompressionStream);

                    string xmlContent = await reader.ReadToEndAsync();

                    string xmlName = baseName + ".xml";
                    string xmlPath = Path.Combine(typeDir, xmlName);

                    await File.WriteAllTextAsync(xmlPath, xmlContent);
                    return 1;
                }

                // --- טיפול בקובץ רגיל (לא ZIP ולא GZ) ---
                {
                    string xmlName = baseName + ".xml";
                    string xmlPath = Path.Combine(typeDir, xmlName);

                    await File.WriteAllBytesAsync(xmlPath, fileBytes);
                    return 1;
                }
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
                .OrderBy(s => int.Parse(s))
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
