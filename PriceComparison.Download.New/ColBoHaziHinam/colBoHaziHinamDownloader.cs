using PriceComparison.Download.New.BinaProject;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace PriceComparison.Download.New.colBoHaziHinam
{
    /// <summary>
    /// מבנה נתונים פנימי לקבצים המוצגים באתר (CityMarket)
    /// </summary>
    public class ColBoHaziHinamFileInfo
    {
        public string Date { get; set; } = "";
        public string StoreId { get; set; } = "";
        public string FileName { get; set; } = "";
        public string Type { get; set; } = "";          
        public string Size { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
    }

    /// <summary>
    /// Downloader מותאם ל- CityMarket (מבוסס על הקוד שהיה ל-ColBoHaziHinam/סופר-פארם)
    /// כולל: פרסור HTML (טבלאות), pagination, זיהוי Type + Partial/Full בעמודות נפרדות,
    /// הורדה עם Retry, חילוץ ZIP/GZ/XML ושמירת XML.
    /// </summary>
    public class ColBoHaziHinamDownloader 
    {
        private readonly HttpClient _httpClient;
        private readonly Random _random = new();
        private const string BaseDownloadPath = "Downloads";
        private  string BASE_URL ="";

       

        public ColBoHaziHinamDownloader()
        {
            _httpClient = new HttpClient();
            SetupHttpClient();
        }

        private void SetupHttpClient()
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "he-IL,he;q=0.9,en-US;q=0.8,en;q=0.7");
            _httpClient.Timeout = TimeSpan.FromMinutes(10);
        }

        public bool CanHandle(string chainId)
        {
            return chainId.Equals("ColBoHaziHinam", StringComparison.OrdinalIgnoreCase);

        }
        // ==================== קבלת רשימת קבצים מהאתר ====================
        public async Task<List<ColBoHaziHinamFileInfo>> GetAvailableFiles(string date)
        {
            try
            {
                var targetDate = DateTime.Parse(date);
                string currentDate = targetDate.ToString("yyyy-MM-dd");  // פורמט שמתאים ל-d
                string baseUrl = $"{BASE_URL}/?d={Uri.EscapeDataString(currentDate)}";


                Console.WriteLine($"      🌐 מתחבר ל-CityMarket עם סינון לפי תאריך {currentDate}");

                var allFiles = new List<ColBoHaziHinamFileInfo>();

                // בקשת העמוד הראשון
                var firstResponse = await _httpClient.GetAsync(baseUrl);
                if (!firstResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine($"      ❌ שגיאת HTTP: {firstResponse.StatusCode}");
                    return new List<ColBoHaziHinamFileInfo>();
                }

                var firstHtml = await firstResponse.Content.ReadAsStringAsync();
                allFiles.AddRange(ParseCityMarketHtml(firstHtml, targetDate));
                int totalPages = GetTotalPages(firstHtml);
                Console.WriteLine($"      🔢 מספר עמודים שזוהו: {totalPages}");

                if (totalPages > 1)
                {
                    Console.WriteLine($"      📚 נמצאו {totalPages} עמודים — מתחיל עיבוד...");
                    for (int page = 2; page <= totalPages; page++)
                    {
                        //await Task.Delay(_random.Next(800, 2200));
                        var pageUrl = $"{BASE_URL}/?p={page}&s=&f=&t=&d={Uri.EscapeDataString(currentDate)}";

                        var resp = await _httpClient.GetAsync(pageUrl);

                        if (!resp.IsSuccessStatusCode)
                        {
                            Console.WriteLine($"      ⚠️ שגיאת HTTP בעמוד {page}: {resp.StatusCode}");
                            continue; // במקום break כדי לבדוק עמודים נוספים
                        }

                        var html = await resp.Content.ReadAsStringAsync();
                        var pageFiles = ParseCityMarketHtml(html, targetDate);

                        // לוג כדי לראות אם הקובץ נמצא
                        Console.WriteLine($"         🌐 עמוד {page}: נמצאו {pageFiles.Count} קבצים");
                        //foreach (var f in pageFiles)
                        //{
                        //    Console.WriteLine($"            📄 {f.FileName} (Type: {f.Type})");
                        //}

                        allFiles.AddRange(pageFiles);
                    }
                }

                // המרה לפורמט סטנדרטי
                Console.WriteLine($"      ✅ סה\"כ לאחר המרת פורמט: {allFiles.Count} פריטים");
                return allFiles;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"      ❌ שגיאה ב-GetAvailableFiles: {ex.Message}");
                return new List<ColBoHaziHinamFileInfo>();
            }
        }
        private int GetTotalPages(string htmlContent)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(htmlContent))
                {
                    Console.WriteLine("HTML ריק או מכיל רק רווחים → מחזיר 1");
                    return 1;
                }

                var clean = Regex.Replace(htmlContent, @"\s+", " ");

                // חיפוש data-page
                var dataPageMatches = Regex.Matches(clean, @"data-page\s*=\s*[""']?(\d+)[""']?", RegexOptions.IgnoreCase);
                if (dataPageMatches.Count > 0)
                {
                    var pages = dataPageMatches.Select(x => int.Parse(x.Groups[1].Value)).ToList();
                    Console.WriteLine("data-page matches: " + string.Join(", ", pages));
                    Console.WriteLine("data-page max: " + pages.Max());
                    return pages.Max();
                }

                // חיפוש href עם פרמטר page או p
                var hrefMatches = Regex.Matches(clean, @"[?&](?:page|p)=(\d+)", RegexOptions.IgnoreCase);
                if (hrefMatches.Count > 0)
                {
                    var pages = hrefMatches.Select(x => int.Parse(x.Groups[1].Value)).ToList();
                    Console.WriteLine("href page/p matches: " + string.Join(", ", pages));
                    Console.WriteLine("href page/p max: " + pages.Max());
                    return pages.Max();
                }

                Console.WriteLine("לא נמצאו מספרי עמודים → מחזיר 1");
            }
            catch (Exception ex)
            {
                Console.WriteLine("שגיאה ב-GetTotalPages: " + ex.Message);
            }

            return 1;
        }
        // ==================== פרסור HTML מותאם ל-CityMarket ====================
        private List<ColBoHaziHinamFileInfo> ParseCityMarketHtml(string htmlContent, DateTime targetDate)
        {
            var files = new List<ColBoHaziHinamFileInfo>();
            try
            {
                // נשתמש בעיקר בפרסור טבלאות (כפי שהדגמת)
                files.AddRange(ParseTableRows(htmlContent, targetDate));

                // במידה ולא נמצאו - ניסיונות fallback
                if (!files.Any())
                    files.AddRange(ParseDirectDownloadLinks(htmlContent, targetDate));
                if (!files.Any())
                    files.AddRange(ParseDivStructure(htmlContent, targetDate));

                //הסרת כפילויות לפי FileName +Store
                //files = files.GroupBy(f => $"{f.Type}||{f.StoreId}").Select(g =>
                //g.OrderByDescending(x=>x.Date)
                //.First()).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"      ⚠️ שגיאה בפרסור עמוד: {ex.Message}");
            }
            return files;
        }
        private List<ColBoHaziHinamFileInfo> ParseTableRows(string htmlContent, DateTime targetDate)
        {
            var files = new List<ColBoHaziHinamFileInfo>();
            try
            {
                var rowPattern = @"<tr[^>]*>(.*?)</tr>";
                var rows = Regex.Matches(htmlContent, rowPattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);

                foreach (Match row in rows)
                {
                    var rowHtml = row.Groups[1].Value;
                    if (rowHtml.Contains("<th") || !rowHtml.Contains("<td")) continue;

                    var cellPattern = @"<td[^>]*>(.*?)</td>";
                    var cells = Regex.Matches(rowHtml, cellPattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);

                    if (cells.Count >= 6)
                    {
                        try
                        {
                            // עמודת תאריך
                            var dateCell = CleanHtmlText(cells[0].Groups[1].Value).Trim();

                            // עמודת סניף
                            var storeId = CleanHtmlText(cells[1].Groups[1].Value).Trim();

                            // עמודת שם קובץ
                            var fileName = CleanHtmlText(cells[2].Groups[1].Value).Trim();

                            // עמודת סוג (Price / Promo / Stores)
                            var type = CleanHtmlText(cells[3].Groups[1].Value).Trim();

                            // גודל
                            var size = CleanHtmlText(cells[4].Groups[1].Value).Trim();

                            // 🎯 קישור הורדה — חילוץ מתוך ה־href
                            var linkHtml = cells[5].Groups[1].Value;
                            var downloadUrlMatch = Regex.Match(linkHtml, @"href\s*=\s*""([^""]+)""", RegexOptions.IgnoreCase);

                            string downloadUrl = "";
                            if (downloadUrlMatch.Success)
                            {
                                downloadUrl = downloadUrlMatch.Groups[1].Value.Trim();

                                // אם הקישור יחסי → להפוך למלא
                                if (!downloadUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                                {
                                    downloadUrl = BASE_URL.TrimEnd('/') + "/" + downloadUrl.TrimStart('/');
                                }
                            }

                            files.Add(new ColBoHaziHinamFileInfo
                            {
                                Date = dateCell,
                                StoreId = storeId,
                                FileName = fileName,
                                Type = type,
                                Size = size,
                                DownloadUrl = downloadUrl
                            });
                        }
                        catch
                        {
                            continue;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"      ⚠️ שגיאה בפרסור טבלה: {ex.Message}");
            }

            return files;
        }



        /// <summary>
        /// חיפוש קישורי הורדה ישירים (fallback)
        /// </summary>
        private List<ColBoHaziHinamFileInfo> ParseDirectDownloadLinks(string htmlContent, DateTime targetDate)
        {
            var files = new List<ColBoHaziHinamFileInfo>();
            try
            {
                var linkPatterns = new[]
                {
                    @"<a[^>]*href=[""']([^""']*\.(?:zip|gz|xml))[""'][^>]*>([^<]*)</a>",
                    @"<a[^>]*href=[""']([^""']*/downloadFile/[^""']*)[""'][^>]*>[^<]*</a>",
                    @"href=[""']([^""']*\.(?:zip|gz|xml)(\?[^""']*)?)[""']"
                };

                foreach (var pattern in linkPatterns)
                {
                    var matches = Regex.Matches(htmlContent, pattern, RegexOptions.IgnoreCase);
                    foreach (Match match in matches)
                    {
                        var url = match.Groups[1].Value;
                        var display = match.Groups.Count > 2 ? CleanHtmlText(match.Groups[2].Value) : Path.GetFileName(url);
                        if (string.IsNullOrEmpty(display)) display = Path.GetFileName(url);

                        if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                            url = BASE_URL.TrimEnd('/') + "/" + url.TrimStart('/');

                        files.Add(new ColBoHaziHinamFileInfo
                        {
                            Date = targetDate.ToString("dd/MM/yyyy"),
                            StoreId ="",
                            FileName = display,
                            Type ="",
                            Size = "",
                            DownloadUrl = url
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"      ⚠️ שגיאה בפרסור קישורים: {ex.Message}");
            }
            return files;
        }

        /// <summary>
        /// פרסור מבני div (fallback)
        /// </summary>
        private List<ColBoHaziHinamFileInfo> ParseDivStructure(string htmlContent, DateTime targetDate)
        {
            var files = new List<ColBoHaziHinamFileInfo>();
            try
            {
                var divPattern = @"<div[^>]*class=[""'][^""']*(row|file|item)[^""']*[""'][^>]*>(.*?)</div>";
                var divs = Regex.Matches(htmlContent, divPattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);
                foreach (Match div in divs)
                {
                    var content = div.Groups[2].Value;
                    var nameMatch = Regex.Match(content, @">([^<]+\.(zip|gz|xml))<", RegexOptions.IgnoreCase);
                    var linkMatch = Regex.Match(content, @"href=[""']([^""']+)[""']", RegexOptions.IgnoreCase);

                    if (nameMatch.Success)
                    {
                        var fileName = CleanHtmlText(nameMatch.Groups[1].Value);
                        var url = linkMatch.Success ? linkMatch.Groups[1].Value : "";
                        if (!string.IsNullOrEmpty(url) && !url.StartsWith("http"))
                            url = BASE_URL.TrimEnd('/') + "/" + url.TrimStart('/');

                        files.Add(new ColBoHaziHinamFileInfo
                        {
                            Date = targetDate.ToString("dd/MM/yyyy"),
                            StoreId = "",
                            FileName = fileName,
                            Type = "",
                            Size = "",
                            DownloadUrl = url
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"      ⚠️ שגיאה בפרסור div: {ex.Message}");
            }
            return files;
        }
        private string CleanHtmlText(string html)
        {
            if (string.IsNullOrEmpty(html)) return "";
            try
            {
                var text = Regex.Replace(html, @"<[^>]+>", "");
                text = HttpUtility.HtmlDecode(text);
                return text.Trim();
            }
            catch { return html.Trim(); }
        }
        // ==================== פונקציות הורדה ושמירת קבצים ====================

        public async Task<DownloadResult> DownloadChain(ChainConfig config, string date)
        {
            BASE_URL = config.BaseUrl;
            var startTime = DateTime.Now;

            var result = new DownloadResult
            {
                ChainName = config.Name,
                Success = false,
                DownloadedFiles = 0,
                SampleFiles = new List<string>(),
                StoresFiles = 0,
                PriceFiles = 0,
                PromoFiles = 0
            };

            try
            {
                Console.WriteLine($"\n🏪 מתחיל הורדה: {config.Name}");

                var chainDir = Path.Combine(BaseDownloadPath, config.Id);
                Directory.CreateDirectory(chainDir);

                var targetDate = DateTime.Parse(date);
                string currentDate = targetDate.ToString("yyyy-MM-dd");

                // ===== עמוד ראשון =====
                string firstUrl = $"{BASE_URL}/?d={Uri.EscapeDataString(currentDate)}";
                Console.WriteLine($"      🌐 קורא עמוד 1");

                var firstResponse = await _httpClient.GetAsync(firstUrl);
                if (!firstResponse.IsSuccessStatusCode)
                {
                    result.ErrorMessage = $"שגיאת HTTP: {firstResponse.StatusCode}";
                    return result;
                }

                var firstHtml = await firstResponse.Content.ReadAsStringAsync();
                int totalPages = GetTotalPages(firstHtml);

                Console.WriteLine($"      🔢 זוהו {totalPages} עמודים");

                // ===== לולאת עמודים =====
                for (int page = 1; page <= totalPages; page++)
                {
                    Console.WriteLine($"      📄 מעבד עמוד {page}/{totalPages}");

                    string pageHtml;

                    if (page == 1)
                    {
                        pageHtml = firstHtml;
                    }
                    else
                    {
                        //await Task.Delay(_random.Next(800, 2200));

                        string pageUrl =
                            $"{BASE_URL}/?p={page}&s=&f=&t=&d={Uri.EscapeDataString(currentDate)}";

                        var resp = await _httpClient.GetAsync(pageUrl);
                        if (!resp.IsSuccessStatusCode)
                        {
                            Console.WriteLine($"      ⚠️ שגיאת HTTP בעמוד {page}: {resp.StatusCode}");
                            continue;
                        }

                        pageHtml = await resp.Content.ReadAsStringAsync();
                    }

                    var pageFiles = ParseCityMarketHtml(pageHtml, targetDate);

                    if (!pageFiles.Any())
                    {
                        Console.WriteLine($"      ⚠️ עמוד {page} ריק — מדלג");
                        continue;
                    }

                    Console.WriteLine($"         📦 נמצאו {pageFiles.Count} קבצים");

                    // 🔍 ניתוח קבצים
                    AnalyzeAvailableFiles(pageFiles);

                    // 🏪 Stores
                    result.StoresFiles += await DownloadStoresFiles(pageFiles, chainDir);

                    // 📍 סניפים
                    var stores = GetUniqueStores(pageFiles);
                    Console.WriteLine($"         📍 זוהו {stores.Count} סניפים");

                    if (stores.Any())
                    {
                        result.PriceFiles += await DownloadPriceFiles(pageFiles, stores, chainDir);
                        result.PromoFiles += await DownloadPromoFiles(pageFiles, stores, chainDir);
                    }
                }

                result.DownloadedFiles =
                    result.StoresFiles + result.PriceFiles + result.PromoFiles;

                result.Success = true;
                result.Duration = (DateTime.Now - startTime).TotalSeconds;

                Console.WriteLine(
                    $"      📊 סיכום: {result.StoresFiles} Stores + {result.PriceFiles} Prices + {result.PromoFiles} Promos = {result.DownloadedFiles}");

                return result;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                result.Duration = (DateTime.Now - startTime).TotalSeconds;
                Console.WriteLine($"      ❌ שגיאה ב-DownloadChain: {ex.Message}");
                return result;
            }
        }

        private void AnalyzeAvailableFiles(List<ColBoHaziHinamFileInfo> files)
        {
            var types = files
                .GroupBy(f =>  f.Type ?? "")
                .ToDictionary(g => g.Key, g => g.Count());

            Console.WriteLine($"      🔍 ניתוח קבצים:");
            foreach (var kv in types)
                Console.WriteLine($"         📄 {kv.Key}: {kv.Value}");
        }


        private async Task<int> DownloadStoresFiles(List<ColBoHaziHinamFileInfo> availableFiles, string chainDir)
        {
            Console.WriteLine($"      📋 מחפש קבצי Stores...");
           
            var storesFiles = availableFiles
                .Where(f => f.Type.Equals("חנויות", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(f => ExtractTimeFromFileName(f.FileName))
                .ToList();

            if (!storesFiles.Any())
            {
                Console.WriteLine($"      ⚠️ לא נמצאו קבצי Stores");
                return 0;
            }

            var latest = storesFiles.First();
            var success = await DownloadAndSaveFileWithRetry(latest, chainDir, "Stores");
            return success ? 1 : 0;
        }

        private async Task<int> DownloadPriceFiles(List<ColBoHaziHinamFileInfo> availableFiles, List<string> stores, string chainDir)
        {
            Console.WriteLine($"      💰 מוריד קבצי Price...");
            int downloaded = 0;
            var limitedStores = stores.ToList();

            foreach (var store in limitedStores)
            {

                var pricefull = availableFiles
                    .Where(f => f.Type.Equals("מחירים", StringComparison.OrdinalIgnoreCase) &&
                               f.StoreId == store &&
                                f.FileName.ToLower().Contains("pricefull"))
                    .OrderByDescending(f => ExtractTimeFromFileName(f.FileName))
                    .ToList();
                var price = availableFiles
                   .Where(f => f.Type.Equals("מחירים", StringComparison.OrdinalIgnoreCase) &&
                              f.StoreId == store &&
                                !f.FileName.ToLower().Contains("pricefull"))
                   .OrderByDescending(f => ExtractTimeFromFileName(f.FileName))
                   .ToList();

                if (pricefull.Any())
                {
                    var latest = pricefull.First();
                    Console.WriteLine($"         🎯 סניף {store} Price: {latest.FileName}");
                    //await Task.Delay(_random.Next(500, 1400));
                    if (await DownloadAndSaveFileWithRetry(latest, chainDir, "Pricefull")) downloaded++;
                }
                if (price.Any())
                {
                    var latest = price.First();
                    Console.WriteLine($"         🎯 סניף {store} Price: {latest.FileName}");
                    //await Task.Delay(_random.Next(500, 1400));
                    if (await DownloadAndSaveFileWithRetry(latest, chainDir, "Price")) downloaded++;
                }
            }

            Console.WriteLine($"      💰 הורדו {downloaded} קבצי Price");
            return downloaded;
        }


        private async Task<int> DownloadPromoFiles(List<ColBoHaziHinamFileInfo> availableFiles, List<string> stores, string chainDir)
        {
            Console.WriteLine($"      🎁 מחפש קבצי Promo...");
            int downloaded = 0;
            var limitedStores = stores.ToList();

            foreach (var store in limitedStores)
            {
                var promoFull = availableFiles
                    .Where(f => f.Type.Equals("מבצעים", StringComparison.OrdinalIgnoreCase) &&
                                f.StoreId== store&&
                                f.FileName.ToLower().Contains("promofull"))
                    .OrderByDescending(f => ExtractTimeFromFileName(f.FileName))
                    .ToList();

                var promo = availableFiles
                    .Where(f => f.Type.Equals("מבצעים", StringComparison.OrdinalIgnoreCase) &&
                                 f.StoreId == store &&
                                !f.FileName.ToLower().Contains("promofull"))
                    .OrderByDescending(f => ExtractTimeFromFileName(f.FileName))
                    .ToList();

                if (promoFull.Any())
                {
                    var latestFull = promoFull.First();
                    Console.WriteLine($"         🎯 סניף {store} PromoFull: {latestFull.FileName}");
                    //await Task.Delay(_random.Next(500, 1400));
                    if (await DownloadAndSaveFileWithRetry(latestFull, chainDir, "PromoFull")) downloaded++;
                }

                if (promo.Any())
                {
                    var latest = promo.First();
                    Console.WriteLine($"         🎯 סניף {store} Promo: {latest.FileName}");
                    //await Task.Delay(_random.Next(500, 1400));
                    if (await DownloadAndSaveFileWithRetry(latest, chainDir, "Promo")) downloaded++;
                }
            }

            if (downloaded > 0) Console.WriteLine($"      🎁 הורדו {downloaded} קבצי Promo");
            else Console.WriteLine($"      🎁 לא נמצאו קבצי Promo");

            return downloaded;
        }

        private async Task<bool> DownloadAndSaveFileWithRetry(ColBoHaziHinamFileInfo fileInfo, string chainDir, string fileType, int maxRetries = 3)
        {
            string? oldFilePath = null;
            try
            {
                var storeId = fileInfo.StoreId;
                var newDateStr = ExtractTimeFromFileName(fileInfo.FileName);
                DateTime newDate;
                DateTime.TryParseExact(newDateStr, "yyyyMMddHHmm", null, System.Globalization.DateTimeStyles.None, out newDate);

                var typeDir = Path.Combine(chainDir, fileType);
                Directory.CreateDirectory(typeDir);
                var searchPattern = "";
                if (fileInfo.Type== "חנויות")
                     searchPattern = $"*-{storeId}00-*.*";
                else
                 searchPattern = $"*-{storeId}-*.*";
                var existingFiles = Directory.GetFiles(typeDir, searchPattern);
                Console.WriteLine($"         📄 נמצאו {existingFiles.Length} קבצים תואמים בתיקייה {typeDir}.");

                if (existingFiles.Any())
                {
                    var existingFile = existingFiles.OrderByDescending(f => f).First();
                    var existingDateStr = ExtractTimeFromFileName(Path.GetFileName(existingFile));
                    existingDateStr = existingDateStr.Split('_')[0];
                    DateTime existingDate;
                    if (DateTime.TryParseExact(existingDateStr, "yyyyMMddHHmm", null, System.Globalization.DateTimeStyles.None, out existingDate))
                    {
                        Console.WriteLine($"         🔍 השוואת תאריכים: חדש = {newDateStr} ({newDate}), ישן = {existingDateStr} ({existingDate})");
                        if (newDate <= existingDate)
                        {
                            Console.WriteLine($"         📁 הסניף {storeId} כבר מעודכן ({existingDate:yyyy-MM-dd HH:mm}) — מדלג.");
                            return true;
                        }
                        else
                        {
                            oldFilePath = existingFile;
                            Console.WriteLine($"         🆕 נמצא קובץ חדש לסניף {storeId}, נמחק את הישן לאחר הורדה מוצלחת.");
                        }
                    }
                }

                for (int attempt = 1; attempt <= maxRetries; attempt++)
                {
                    try
                    {
                        if (attempt > 1)
                        {
                            Console.WriteLine($"         🔄 ניסיון {attempt}/{maxRetries}: {fileInfo.FileName}");
                            await Task.Delay(_random.Next(300, 600));
                        }

                        var success = await DownloadAndSaveFile(fileInfo, chainDir, fileType);
                        if (success)
                        {
                            if (!string.IsNullOrEmpty(oldFilePath) && File.Exists(oldFilePath))
                            {
                                File.Delete(oldFilePath);
                                Console.WriteLine($"         🧹 נמחק הקובץ הישן: {Path.GetFileName(oldFilePath)}");
                            }
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"         ⚠️ ניסיון {attempt} נכשל: {ex.Message}");
                        if (attempt == maxRetries) Console.WriteLine($"         ❌ נכשל לאחר {maxRetries} ניסיונות: {fileInfo.FileName}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"         ⚠️ שגיאה בתהליך retry: {ex.Message}");
            }
            return false;
        }

        private async Task<bool> DownloadAndSaveFile(ColBoHaziHinamFileInfo fileInfo, string chainDir, string fileType)
        {
            try
            {
                var typeDir = Path.Combine(chainDir, fileType);
                Directory.CreateDirectory(typeDir);

                var downloadUrl = fileInfo.DownloadUrl;
                if (string.IsNullOrEmpty(downloadUrl))
                {
                    Console.WriteLine($"         ❌ לא נמצא קישור הורדה עבור {fileInfo.FileName}");
                    return false;
                }

                Console.WriteLine($"         📥 מוריד מ: {downloadUrl}");
                var response = await _httpClient.GetAsync(downloadUrl);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"         ❌ שגיאה בהורדה: {response.StatusCode}");
                    return false;
                }

                var fileBytes = await response.Content.ReadAsByteArrayAsync();
                var saved = await ExtractAndSaveXml(fileBytes, fileInfo, typeDir);

                if (saved > 0)
                {
                    Console.WriteLine($"         ✅ נשמרו {saved} קבצי XML עבור {fileInfo.FileName}");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"         ❌ שגיאה ב-DownloadAndSaveFile: {ex.Message}");
                return false;
            }
        }
        private async Task<int> ExtractAndSaveXml(byte[] fileBytes, ColBoHaziHinamFileInfo fileInfo, string typeDir)
        {
            try
            {
                int savedCount = 0;

                string SanitizeFileName(string fn)
                {
                    foreach (var c in Path.GetInvalidFileNameChars())
                        fn = fn.Replace(c, '_');
                    return fn;
                }

                // --- ZIP ---
                if (IsZipFile(fileBytes))
                {
                    using var zipStream = new MemoryStream(fileBytes);
                    using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

                    foreach (var entry in archive.Entries)
                    {
                        if (!string.IsNullOrEmpty(entry.Name) &&
                            entry.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                        {
                            var safe = SanitizeFileName(entry.Name);
                            var xmlPath = Path.Combine(typeDir, safe);

                            entry.ExtractToFile(xmlPath, true);
                            savedCount++;
                        }
                    }

                    return savedCount;
                }

                // --- GZIP ---
                if (IsGzFile(fileBytes))
                {
                    using var gzStream = new MemoryStream(fileBytes);
                    using var gzip = new GZipStream(gzStream, CompressionMode.Decompress);
                    using var reader = new StreamReader(gzip);

                    var xmlContent = await reader.ReadToEndAsync();

                    // יצירת שם XML תקין
                    var xmlFileName = Path.GetFileNameWithoutExtension(fileInfo.FileName) + ".xml";
                    var safe = SanitizeFileName(xmlFileName);
                    var xmlPath = Path.Combine(typeDir, safe);

                    await File.WriteAllTextAsync(xmlPath, xmlContent);
                    return 1;
                }

                // --- NOT ZIP, NOT GZIP → כנראה XML נקי ---
                string finalName = fileInfo.FileName;
                if (!finalName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                    finalName = Path.GetFileNameWithoutExtension(finalName) + ".xml";

                var safeName = SanitizeFileName(finalName);
                var xmlPathFinal = Path.Combine(typeDir, safeName);

                // כאן שומרים רק XML נקי, לא GZIP
                await File.WriteAllTextAsync(xmlPathFinal, Encoding.UTF8.GetString(fileBytes));

                return 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"         ❌ שגיאה בחילוץ: {ex.Message}");
                return 0;
            }
        }

        // ==================== פונקציות עזר ====================

        private List<string> GetUniqueStores(List<ColBoHaziHinamFileInfo> files)
        {
           return files
                .Where(f => f.Type .ToLower().Contains("מחירים")
                          ||f.Type .ToLower().Contains("מבצעים"))
                .Select(f => f.StoreId)
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct()
                .OrderBy(s => s)
                .ToList();
          
        }

        private string ExtractTimeFromFileName(string fileName)
        {
            try
            {
                if (string.IsNullOrEmpty(fileName))
                    return "000000000000";

                // תופס תבנית: -yyyyMMdd-HHmmss
                var m = Regex.Match(fileName, @"-(\d{8})-(\d{6})");
                if (m.Success)
                {
                    // yyyyMMdd + HHmm  (בלי שניות)
                    return m.Groups[1].Value + m.Groups[2].Value.Substring(0, 4);
                }

                return "000000000000";
            }
            catch
            {
                return "000000000000";
            }
        }

        private bool IsZipFile(byte[] fileBytes)
        {
            return fileBytes != null && fileBytes.Length >= 4 &&
                   fileBytes[0] == 0x50 && fileBytes[1] == 0x4B &&
                   (fileBytes[2] == 0x03 || fileBytes[2] == 0x05 || fileBytes[2] == 0x07) &&
                   (fileBytes[3] == 0x04 || fileBytes[3] == 0x06 || fileBytes[3] == 0x08);
        }

        private bool IsGzFile(byte[] fileBytes)
        {
            return fileBytes != null && fileBytes.Length >= 2 &&
                   fileBytes[0] == 0x1F && fileBytes[1] == 0x8B;
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
