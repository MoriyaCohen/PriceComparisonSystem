using PriceComparison.Download.New.BinaProject;
using PriceComparison.Download.New.colBoHaziHinam;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace PriceComparison.Download.New.Citymarket
{
     public class CitymarketFileInfo
    {
        public string Date { get; set; } = "";
        public string Store { get; set; } = "";
        public string FileName { get; set; } = "";
        public string Type { get; set; } = "";          // לדוגמה: Price / Promo / Stores
        public string FileIsFull { get; set; } = "";    // Partial / Full (או "חלקי"/"מלא")
        public string Size { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
    }
    public class citymarketDownloader
    {
        private readonly HttpClient _httpClient;
        private readonly Random _random = new();
        private const string BaseDownloadPath = "Downloads";
        private string BASE_URL = "";

        //public string ChainName => "CityMarket";
        //public string ChainId => "ColBoHaziHinam";

        public citymarketDownloader()
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
            return chainId.Equals("ColBoHaziHinam", StringComparison.OrdinalIgnoreCase)
                || chainId.Equals("CityMarket", StringComparison.OrdinalIgnoreCase)
                || chainId.Equals("citymarket", StringComparison.OrdinalIgnoreCase);
        }

        // ==================== קבלת רשימת קבצים מהאתר ====================
        public async Task<List<CitymarketFileInfo>> GetAvailableFiles(string date)
        {
            try
            {
                var targetDate = DateTime.Parse(date);
                string currentDate = targetDate.ToString("yyyy-MM-dd");  // פורמט שמתאים ל-d
                string baseUrl = $"{BASE_URL}/?d={Uri.EscapeDataString(currentDate)}";


                Console.WriteLine($"      🌐 מתחבר ל-CityMarket עם סינון לפי תאריך {currentDate}");

                var allFiles = new List<CitymarketFileInfo>();

                // בקשת העמוד הראשון
                var firstResponse = await _httpClient.GetAsync(baseUrl);
                if (!firstResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine($"      ❌ שגיאת HTTP: {firstResponse.StatusCode}");
                    return new List<CitymarketFileInfo>();
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
                return new List<CitymarketFileInfo>();
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
        private List<CitymarketFileInfo> ParseCityMarketHtml(string htmlContent, DateTime targetDate)
        {
            var files = new List<CitymarketFileInfo>();
            try
            {
                // נשתמש בעיקר בפרסור טבלאות (כפי שהדגמת)
                files.AddRange(ParseTableRows(htmlContent, targetDate));

                // במידה ולא נמצאו - ניסיונות fallback
                if (!files.Any())
                    files.AddRange(ParseDirectDownloadLinks(htmlContent, targetDate));
                if (!files.Any())
                    files.AddRange(ParseDivStructure(htmlContent, targetDate));

                // הסרת כפילויות לפי FileName + Store
                files = files
      .GroupBy(f => $"{f.Type}||{f.Store}||{f.FileIsFull}") // קיבוץ לפי שלושה שדות
      .Select(g => g.OrderByDescending(x => x.Date).First())  // בוחר את הקובץ הכי עדכני בכל קבוצה
      .ToList();

            }
            catch (Exception ex)
            {
                Console.WriteLine($"      ⚠️ שגיאה בפרסור עמוד: {ex.Message}");
            }
            return files;
        }

        private List<CitymarketFileInfo> ParseTableRows(string htmlContent, DateTime targetDate)
        {
            var files = new List<CitymarketFileInfo>();
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

                    // נצפה לפחות 6-7 תאים לפי הדוגמה
                    if (cells.Count >= 6)
                    {
                        try
                        {
                            var dateCellRaw = CleanHtmlText(cells[0].Groups[1].Value);
                            var dateCell = Regex.Replace(dateCellRaw, @"\s+| ", " ").Trim();

                            // תא 1: סניף (שם)
                            var store = CleanHtmlText(cells[1].Groups[1].Value);

                            // תא 2: שם הקובץ/מזהה
                            var fileNameCell = cells[2].Groups[1].Value;
                            var fileName = CleanHtmlText(fileNameCell);

                            // תא 3: עמודת Type (למשל "Prices" / "Promo" / "Stores")
                            var type = CleanHtmlText(cells[3].Groups[1].Value);

                            // תא 4: חלקי/מלא (למשל "חלקי" / "מלא" או "partial"/"full")
                            var fileIsFull = CleanHtmlText(cells[4].Groups[1].Value);

                            // תא 5: גודל
                            var size = CleanHtmlText(cells[5].Groups[1].Value);

                            string downloadUrlCell = cells[6].Groups[1].Value;

                            // חיפוש href בתוך התוכן של התא
                            var hrefMatch = Regex.Match(downloadUrlCell, @"href\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase);
                            string downloadUrl = hrefMatch.Success ? hrefMatch.Groups[1].Value : string.Empty;

                            if (!string.IsNullOrEmpty(downloadUrl) && !downloadUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                            {
                                downloadUrl = BASE_URL.TrimEnd('/') + "/" + downloadUrl.TrimStart('/');
                            }

                            files.Add(new CitymarketFileInfo
                            {
                                Date = dateCell,
                                Store = store,
                                FileName = fileName,
                                Type = type,
                                FileIsFull = fileIsFull,
                                Size = size,
                                DownloadUrl = downloadUrl
                            });
                        }
                        catch
                        {
                            // נטפל בשורות שלא פורסו היטב, נמשיך לשורה הבאה
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
        private List<CitymarketFileInfo> ParseDirectDownloadLinks(string htmlContent, DateTime targetDate)
        {
            var files = new List<CitymarketFileInfo>();
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

                        files.Add(new CitymarketFileInfo
                        {
                            Date = targetDate.ToString("dd/MM/yyyy"),
                            Store = "",
                            FileName = display,
                            Type = DetermineFileType(display, ""),
                            FileIsFull = "", // לא ידוע
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
        private List<CitymarketFileInfo> ParseDivStructure(string htmlContent, DateTime targetDate)
        {
            var files = new List<CitymarketFileInfo>();
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

                        files.Add(new CitymarketFileInfo
                        {
                            Date = targetDate.ToString("dd/MM/yyyy"),
                            Store = "",
                            FileName = fileName,
                            Type = DetermineFileType(fileName, ""),
                            FileIsFull = "",
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
                Console.WriteLine("      🌐 קורא עמוד 1");

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

                    string html;

                    if (page == 1)
                    {
                        html = firstHtml;
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

                        html = await resp.Content.ReadAsStringAsync();
                    }

                    var pageFiles = ParseCityMarketHtml(html, targetDate);

                    if (!pageFiles.Any())
                    {
                        Console.WriteLine($"      ⚠️ עמוד {page} ריק — מדלג");
                        continue;
                    }

                    Console.WriteLine($"         📦 נמצאו {pageFiles.Count} קבצים");

                    // 🔍 ניתוח
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


        private void AnalyzeAvailableFiles(List<CitymarketFileInfo> files)
        {
            var types = files
                .GroupBy(f => DetermineFileType(f.FileName, f.Type ?? ""))
                .ToDictionary(g => g.Key, g => g.Count());

            Console.WriteLine($"      🔍 ניתוח קבצים:");
            foreach (var kv in types)
                Console.WriteLine($"         📄 {kv.Key}: {kv.Value}");
        }


        private async Task<int> DownloadStoresFiles(List<CitymarketFileInfo> availableFiles, string chainDir)
        {
            Console.WriteLine($"      📋 מחפש קבצי Stores...");


            var storesFiles = availableFiles
                 .Where(f => f.Type.Equals("Stores", StringComparison.OrdinalIgnoreCase)&&
                 f.FileIsFull=="מלא")
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

        private async Task<int> DownloadPriceFiles(List<CitymarketFileInfo> availableFiles, List<string> stores, string chainDir)
        {
            Console.WriteLine($"      💰 מוריד קבצי Price...");
            int downloaded = 0;
            var limitedStores = stores.ToList();

            foreach (var store in limitedStores)
            {
                // PriceFull first
                var priceFull = availableFiles
                    .Where(f => f.Type== "Prices" &&
                                (f.FileIsFull ?? "").Contains("מלא") &&
                                ExtractStoreFromFileName(f.FileName, "") == store)
                    .OrderByDescending(f => ExtractTimeFromFileName(f.FileName))
                    .ToList();

                var price = availableFiles
                    .Where(f => f.Type == "Prices" &&
                                (f.FileIsFull ?? "").Contains("חלקי") &&
                                ExtractStoreFromFileName(f.FileName, "") == store)
                    .OrderByDescending(f => ExtractTimeFromFileName(f.FileName))
                    .ToList();

                if (priceFull.Any())
                {
                    var latestFull = priceFull.First();
                    Console.WriteLine($"         🎯 סניף {store} PriceFull: {latestFull.FileName}");
                    //await Task.Delay(_random.Next(500, 1400));
                    if (await DownloadAndSaveFileWithRetry(latestFull, chainDir, "PriceFull")) downloaded++;
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


        private async Task<int> DownloadPromoFiles(List<CitymarketFileInfo> availableFiles, List<string> stores, string chainDir)
        {
            Console.WriteLine($"      🎁 מחפש קבצי Promo...");
            int downloaded = 0;
            var limitedStores = stores.ToList();

            foreach (var store in limitedStores)
            {
                var promoFull = availableFiles
                    .Where(f => f.Type.Equals("Promotions", StringComparison.OrdinalIgnoreCase) &&
                                f.FileIsFull.ToLower().Contains("מלא") &&
                                ExtractStoreFromFileName(f.FileName, "") == store)
                    .OrderByDescending(f => f.Date)
                    .ToList();

                var promo = availableFiles
                    .Where(f => f.Type.Equals("Promotions", StringComparison.OrdinalIgnoreCase) &&
                                f.FileIsFull.ToLower().Contains("חלקי") &&
                                 ExtractStoreFromFileName(f.FileName, "") == store)
                    .OrderByDescending(f => f.Date)
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

        private async Task<bool> DownloadAndSaveFileWithRetry(CitymarketFileInfo fileInfo, string chainDir, string fileType, int maxRetries = 3)
        {
            string? oldFilePath = null;
            try
            {
                //var fileName = Path.GetFileName(fileInfo.FileName).Split('?')[0];
                //var storeId = fileInfo.Store;
                //var newDateStr = fileInfo.Date;

                //DateTime newDate;
                //if (!DateTime.TryParseExact(newDateStr, "yyyyMMddHHmm", null, System.Globalization.DateTimeStyles.None, out newDate))
                //    newDate = DateTime.MinValue;


                var storeId = ExtractStoreFromFileName(fileInfo.FileName, "");
                var newDateStr = ExtractTimeFromFileName(fileInfo.FileName);
                DateTime newDate;
                DateTime.TryParseExact(newDateStr, "yyyyMMddHHmm", null, System.Globalization.DateTimeStyles.None, out newDate);

                var typeDir = Path.Combine(chainDir, fileType);
                Directory.CreateDirectory(typeDir);
                var searchPattern = $"*-{storeId}-*.*";
                var existingFiles = Directory.GetFiles(typeDir, searchPattern);
                Console.WriteLine($"         📄 נמצאו {existingFiles.Length} קבצים תואמים בתיקייה {typeDir}.");

                if (existingFiles.Any())
                { 
                    Console.WriteLine("נכנס");
                    var existingFile = existingFiles.OrderByDescending(f => f).First();
                    var existingDateStr = ExtractTimeFromFileName(Path.GetFileName(existingFile));
                    existingDateStr = existingDateStr.Split('_')[0];
                    Console.WriteLine(existingDateStr+"  "+ newDateStr);
                    DateTime existingDate;
                    if (DateTime.TryParseExact(existingDateStr, "yyyyMMddHHmm", null, System.Globalization.DateTimeStyles.None, out existingDate))
                    {
                        Console.WriteLine("נכנס");
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

        private async Task<bool> DownloadAndSaveFile(CitymarketFileInfo fileInfo, string chainDir, string fileType)
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

        /// <summary>
        /// ניסיון לקבל URL ישיר: אם fileName הוא URL נחזיר אותו, אחרת אם הוא מזהה של קובץ (downloadFile/...) נבנה את ה-URL.
        /// במידת הצורך, ניתן לבצע בקשת HEAD כדי לוודא שהוא קיים.
        /// </summary>
        private async Task<string> GetDirectDownloadUrl(string fileNameOrUrl, CitymarketFileInfo fileInfo)
        {
            try
            {
                if (string.IsNullOrEmpty(fileNameOrUrl)) return "";

                // אם זה כבר כתובת מלאה
                if (fileNameOrUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var head = await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, fileNameOrUrl));
                        if (head.IsSuccessStatusCode) return fileNameOrUrl;
                    }
                    catch { return fileNameOrUrl; }
                }

                // נתיב יחסי מתחיל ב-/downloadFile/ או /Download/
                if (fileNameOrUrl.StartsWith("/"))
                {
                    var url = BASE_URL.TrimEnd('/') + fileNameOrUrl;
                    try
                    {
                        var head = await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
                        if (head.IsSuccessStatusCode) return url;
                    }
                    catch { return url; }
                }

                // ניסיון לחלץ מזהה מתוך השם
                var simpleIdMatch = Regex.Match(fileNameOrUrl, @"downloadFile/([a-zA-Z0-9\-]+)", RegexOptions.IgnoreCase);
                if (simpleIdMatch.Success)
                {
                    var partial = simpleIdMatch.Value;
                    var candidate = BASE_URL.TrimEnd('/') + "/" + partial;
                    try
                    {
                        var head = await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, candidate));
                        if (head.IsSuccessStatusCode) return candidate;
                    }
                    catch { return candidate; }
                }

                return "";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"         ⚠️ שגיאה ב-GetDirectDownloadUrl: {ex.Message}");
                return "";
            }
        }

        private async Task<int> ExtractAndSaveXml(byte[] fileBytes, CitymarketFileInfo fileInfo, string typeDir)
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

                if (IsZipFile(fileBytes))
                {
                    using var zipStream = new MemoryStream(fileBytes);
                    using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
                    foreach (var entry in archive.Entries)
                    {
                        if (!string.IsNullOrEmpty(entry.Name) && entry.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                        {
                            var safe = SanitizeFileName(entry.Name);
                            var xmlPath = Path.Combine(typeDir, safe);
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

                    var xmlFileName = fileInfo.FileName;
                    if (xmlFileName.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
                        xmlFileName = xmlFileName.Substring(0, xmlFileName.Length - 3);
                    if (!xmlFileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                        xmlFileName += ".xml";

                    var safe = SanitizeFileName(xmlFileName);
                    var xmlPath = Path.Combine(typeDir, safe);
                    await File.WriteAllTextAsync(xmlPath, xmlContent);
                    savedCount = 1;
                }
                else
                {
                    var xmlFileName = fileInfo.FileName;
                    if (!xmlFileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) &&
                        !xmlFileName.EndsWith(".gz", StringComparison.OrdinalIgnoreCase) &&
                        !xmlFileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                        xmlFileName += ".xml";

                    var safe = SanitizeFileName(xmlFileName);
                    var xmlPath = Path.Combine(typeDir, safe);
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


        // ==================== פונקציות עזר ====================

        private List<string> GetUniqueStores(List<CitymarketFileInfo> files)
        {
            return files
                 .Where(f => (DetermineFileType(f.FileName, f.Type ?? "").ToLower().Contains("price")
                           || DetermineFileType(f.FileName, f.Type ?? "").ToLower().Contains("promo")))
                 .Select(f => ExtractStoreFromFileName(f.FileName, ""))
                 .Where(s => !string.IsNullOrEmpty(s))
                 .Distinct()
                 .OrderBy(s => s)
                 .ToList();

        }


        private string DetermineFileType(string fileName, string category)
        {

            var lowerName = (fileName ?? "").ToLower();
            var lowerCategory = (category ?? "").ToLower();
            // Category column might contain "Prices" and FileIsFull column separate indicates partial/full.
            if (lowerName.Contains("stores") || lowerCategory.Contains("stores")) return "Stores";
            if (lowerName.Contains("price") || lowerCategory.Contains("prices") || lowerCategory.Contains("prices")) return "Price";
            if (lowerName.Contains("promo") || lowerName.Contains("promotion") || lowerCategory.Contains("promo") || lowerCategory.Contains("promotion")) return "Promo";

            return "Unknown";
        }

        private string ExtractStoreFromFileName(string fileName, string branchName)
        {
            try
            {
                if (!string.IsNullOrEmpty(branchName))
                    return branchName;

                if (string.IsNullOrEmpty(fileName))
                    return "";

                var parts = fileName.Split('-');

                // פורמט קצר: Pricexxx-032-20251204
                if (parts.Length == 3)
                {
                    // חלק אמצעי = סניף
                    if (Regex.IsMatch(parts[1], @"^\d{3}$"))
                        return parts[1];
                }

                // פורמט ארוך: Pricexxx-000-034-20251204-123101.gz
                if (parts.Length >= 4)
                {
                    // חלק שלישי = סניף
                    if (Regex.IsMatch(parts[2], @"^\d{3}$"))
                        return parts[2];
                }

                return "";
            }
            catch
            {
                return "";
            }
        }


        private string ExtractTimeFromFileName(string fileName)
        {
            try
            {
                if (string.IsNullOrEmpty(fileName))
                    return "000000000000";

                // פונקציה פנימית לבדוק רק תאריך (yyyyMMdd)
                bool IsValidDate(string value12)
                {
                    if (value12.Length < 8) return false;

                    string yyyy = value12.Substring(0, 4);
                    string MM = value12.Substring(4, 2);
                    string dd = value12.Substring(6, 2);

                    if (!yyyy.StartsWith("20")) return false;

                    return DateTime.TryParseExact(
                        $"{yyyy}{MM}{dd}",
                        "yyyyMMdd",
                        null,
                        System.Globalization.DateTimeStyles.None,
                        out _);
                }

                // ---- הסרת סיומות כמו .gz או .xml ----
                string nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);

                // ---- 1) פורמט קצר: 12 ספרות בסוף השם (yyyyMMddHHmm) ----
                var shortMatch = Regex.Match(nameWithoutExtension, @"(\d{12})$");
                if (shortMatch.Success)
                {
                    var val = shortMatch.Value;
                    if (IsValidDate(val.Substring(0, 8))) // רק התאריך נבדק
                        return val; // מחזיר את כל 12 הספרות כולל השעה
                }

                // ---- 2) פורמט עם מקף: yyyyMMdd-HHmmss ----
                var longMatch = Regex.Match(nameWithoutExtension, @"(\d{8})-(\d{6})$");
                if (longMatch.Success)
                {
                    string date = longMatch.Groups[1].Value; // yyyyMMdd
                    string time = longMatch.Groups[2].Value; // HHmmss

                    if (IsValidDate(date))
                    {
                        string combined = date + time.Substring(0, 4); // yyyyMMddHHmm
                        return combined;
                    }
                }

                // ---- 3) fallback: רק תאריך ----
                var dateOnly = Regex.Match(nameWithoutExtension, @"(\d{8})$");
                if (dateOnly.Success && dateOnly.Value.StartsWith("20"))
                {
                    string fallback = dateOnly.Value + "0000"; // משלים שעה לדפולט 00:00
                    if (IsValidDate(dateOnly.Value))
                        return fallback;
                }

                return "000000000000";
            }
            catch
            {
                return "000000000000";
            }
        }





        private string GetFileExtension(string fileName)
        {
            try
            {
                return Path.GetExtension(fileName).TrimStart('.');
            }
            catch { return ""; }
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
