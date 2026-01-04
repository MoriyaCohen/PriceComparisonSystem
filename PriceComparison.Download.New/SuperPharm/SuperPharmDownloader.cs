using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Microsoft.VisualBasic.FileIO;
using PriceComparison.Download.New.BinaProject;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace PriceComparison.Download.New.SuperPharm
{
    /// <summary>
    /// מודל לקובץ ברשת סופר פארם - מבוסס על parsing HTML
    /// </summary>
    public class SuperPharmFileInfo
    {
        public string FileName { get; set; } = "";
        public string Date { get; set; } = "";
        public string Category { get; set; } = "";
        public string BranchName { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
    }

    /// <summary>
    /// מוריד קבצים מרשת סופר פארם - HTML parsing עם תמיכה ב-pagination
    /// </summary>
    public class SuperPharmDownloader : IChainDownloader
    {
        private readonly HttpClient _httpClient;
        private readonly Random _random = new();
        private const string BaseDownloadPath = "Downloads";

        // הגדרות ייחודיות לרשת סופר פארם
        private const string SUPER_PHARM_BASE_URL = "https://prices.super-pharm.co.il/";

        public string ChainName => "סופר פארם (ישראל) בע\"מ";
        public string ChainId => "SuperPharm";

        public SuperPharmDownloader()
        {
            _httpClient = new HttpClient();
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

        public bool CanHandle(string chainId)
        {
            return chainId.Equals("superpharm", StringComparison.OrdinalIgnoreCase) ||
                   chainId.Equals("super-pharm", StringComparison.OrdinalIgnoreCase) ||
                   chainId.Equals("super_pharm", StringComparison.OrdinalIgnoreCase) ||
                   chainId.Equals("סופר-פארם", StringComparison.OrdinalIgnoreCase);
        }
        public async Task<List<FileMetadata>> GetAvailableFiles(string date)
        {
            try
            {
                var targetDate = DateTime.Parse(date);
                // אם לא הועבר תאריך מבחוץ, ניקח את תאריך היום בפורמט הנכון
                string currentDate = targetDate.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
                string encodedDate = Uri.EscapeDataString(currentDate);

                string baseUrl = $"{SUPER_PHARM_BASE_URL}?Date-equals={encodedDate}";
                Console.WriteLine($"      🌐 מתחבר לסופר-פארם עם סינון לפי תאריך {currentDate}");

                var allFiles = new List<SuperPharmFileInfo>();

                // נוריד את העמוד הראשון כדי לדעת כמה עמודים יש
                Console.WriteLine("      📄 קורא עמוד 1 (ראשוני)...");
                var firstResponse = await _httpClient.GetAsync(baseUrl);

                if (!firstResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine($"      ❌ שגיאת HTTP בעמוד הראשון: {firstResponse.StatusCode}");
                    return new List<FileMetadata>();
                }

                var firstHtml = await firstResponse.Content.ReadAsStringAsync();
                var firstPageFiles = ParseSuperPharmHtml(firstHtml, targetDate);
                allFiles.AddRange(firstPageFiles);

                // נחשב את מספר העמודים הכולל מה-HTML
                int totalPages = GetTotalPages(firstHtml);
                if (totalPages <= 1)
                {
                    Console.WriteLine("      ✅ רק עמוד אחד — אין צורך בלולאה.");
                }
                else
                {
                    Console.WriteLine($"      📚 נמצאו {totalPages} עמודים — מתחיל לעבור עליהם...");

                    for (int page = 2; page <= totalPages; page++)
                    {
                        Console.WriteLine($"      📄 קורא עמוד {page}/{totalPages}...");
                        //await Task.Delay(_random.Next(1000, 3000)); // מנוחה קצרה

                        var pageUrl = $"{baseUrl}&page={page}";
                        var response = await _httpClient.GetAsync(pageUrl);
                        if (!response.IsSuccessStatusCode)
                        {
                            Console.WriteLine($"      ⚠️ שגיאת HTTP בעמוד {page}: {response.StatusCode}");
                            break;
                        }

                        var htmlContent = await response.Content.ReadAsStringAsync();
                        var pageFiles = ParseSuperPharmHtml(htmlContent, targetDate);

                        if (pageFiles.Count == 0)
                        {
                            Console.WriteLine($"      ⚠️ עמוד {page} ריק — עוצרים.");
                            break;
                        }

                        allFiles.AddRange(pageFiles);
                    }
                }

                Console.WriteLine($"      ✅ סה\"כ נמצאו {allFiles.Count} קבצים זמינים לכל העמודים של {currentDate}");

                var convertedFiles = ConvertToStandardFormat(allFiles);
                Console.WriteLine($"      🔍 לאחר סינון לתאריך {currentDate}: {convertedFiles.Count} קבצים רלוונטיים");

                return convertedFiles;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"      💥 שגיאה בקבלת קבצים: {ex.Message}");
                return new List<FileMetadata>();
            }
        }


        private int GetTotalPages(string htmlContent)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(htmlContent))
                    return 1;

                var clean = Regex.Replace(htmlContent, @"\s+", " ");
                var options = RegexOptions.IgnoreCase | RegexOptions.Singleline;

                // 1. ניסיון פשוט: לקחת את כל data-page ולשלוף את הגדול ביותר
                var dataPageMatches = Regex.Matches(clean, @"data-page\s*=\s*[""']?(\d+)[""']?", options);
                if (dataPageMatches.Count > 0)
                {
                    var maxFromDataPage = dataPageMatches
                        .Select(m => int.Parse(m.Groups[1].Value))
                        .Max();
                    return maxFromDataPage;
                }

                // 2. fallback: חיפוש קישורים עם ?page=NN
                var hrefPageMatches = Regex.Matches(clean, @"[?&]page=(\d+)", options);
                if (hrefPageMatches.Count > 0)
                {
                    return hrefPageMatches.Select(m => int.Parse(m.Groups[1].Value)).Max();
                }

                // 3. fallback מתקדם: אם יש תגית link rel="last" עם href שמכיל page=
                var relLast = Regex.Match(clean, @"<link[^>]*rel\s*=\s*[""']last[""'][^>]*href\s*=\s*[""']([^""']+)[""']", options);
                if (relLast.Success)
                {
                    var href = relLast.Groups[1].Value;
                    var m = Regex.Match(href, @"[?&]page=(\d+)");
                    if (m.Success) return int.Parse(m.Groups[1].Value);
                }

                // אם לא נמצא שום סימן, נחזור 1
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ שגיאה בחישוב סה\"כ עמודים: {ex.Message}");
            }

            return 1;
        }

        /// <summary>
        /// פרסור HTML של סופר פארם לחילוץ נתוני הקבצים - גרסה משופרת
        /// </summary>
        private List<SuperPharmFileInfo> ParseSuperPharmHtml(string htmlContent, DateTime targetDate)
        {
            var files = new List<SuperPharmFileInfo>();

            try
            {
                // שיטה 1: חיפוש טבלאות קלסיות
                files.AddRange(ParseTableRows(htmlContent, targetDate));

                // שיטה 2: חיפוש קישורי הורדה ישירים
                if (files.Count == 0)
                {
                    files.AddRange(ParseDirectDownloadLinks(htmlContent, targetDate));
                }

                // שיטה 3: חיפוש במבנה div
                if (files.Count == 0)
                {
                    files.AddRange(ParseDivStructure(htmlContent, targetDate));
                }

                // הסרת כפילויות לפי שם קובץ
                files = files.GroupBy(f => f.FileName)
                            .Select(g => g.First())
                            .ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"      ⚠️ שגיאה בפרסור HTML: {ex.Message}");
            }

            return files;
        }

        /// <summary>
        /// פרסור שורות טבלה קלסיות
        /// </summary>
        private List<SuperPharmFileInfo> ParseTableRows(string htmlContent, DateTime targetDate)
        {
            var files = new List<SuperPharmFileInfo>();

            try
            {
                // חיפוש שורות בטבלה - דפוס regex משופר
                var rowPattern = @"<tr[^>]*>(.*?)</tr>";
                var rows = Regex.Matches(htmlContent, rowPattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);

                foreach (Match row in rows)
                {
                    var rowHtml = row.Groups[1].Value;

                    // דילוג על header rows
                    if (rowHtml.Contains("<th") || !rowHtml.Contains("<td"))
                        continue;

                    // חילוץ תאים מהשורה
                    var cellPattern = @"<td[^>]*>(.*?)</td>";
                    var cells = Regex.Matches(rowHtml, cellPattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);

                    if (cells.Count >= 4) // לפחות 4 עמודות: שם, תאריך, קטגוריה, הורדה
                    {
                        var fileName = "";
                        var date = "";
                        var category = "";
                        var branchName = "";
                        var downloadUrl = "";

                        // ניסיון חילוץ לפי סדר עמודות שונים
                        if (cells.Count >= 5)
                        {
                            // פורמט: שם, תאריך, קטגוריה, סניף, הורדה
                            fileName = CleanHtmlText(cells[0].Groups[1].Value);
                            date = CleanHtmlText(cells[1].Groups[1].Value);
                            category = CleanHtmlText(cells[2].Groups[1].Value);
                            branchName = CleanHtmlText(cells[3].Groups[1].Value);
                            downloadUrl = ExtractDownloadUrl(cells[4].Groups[1].Value);
                        }
                        else if (cells.Count >= 4)
                        {
                            // פורמט: שם, תאריך, קטגוריה, הורדה
                            fileName = CleanHtmlText(cells[0].Groups[1].Value);
                            date = CleanHtmlText(cells[1].Groups[1].Value);
                            category = CleanHtmlText(cells[2].Groups[1].Value);
                            downloadUrl = ExtractDownloadUrl(cells[3].Groups[1].Value);
                        }

                        if (!string.IsNullOrEmpty(fileName) && !string.IsNullOrEmpty(downloadUrl))
                        {
                            files.Add(new SuperPharmFileInfo
                            {
                                FileName = fileName,
                                Date = targetDate.ToString("dd/MM/yyyy"),
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
                Console.WriteLine($"      ⚠️ שגיאה בפרסור טבלה: {ex.Message}");
            }

            return files;
        }

        /// <summary>
        /// חיפוש קישורי הורדה ישירים בHTML
        /// </summary>
        private List<SuperPharmFileInfo> ParseDirectDownloadLinks(string htmlContent, DateTime targetDate)
        {
            var files = new List<SuperPharmFileInfo>();

            try
            {
                // דפוסים שונים לקישורי הורדה
                var linkPatterns = new[]
                {
            @"<a[^>]*href=[""']([^""']*\.(?:zip|gz|xml))[""'][^>]*>([^<]*)</a>",
            @"href=[""']([^""']*Download/[^""']*)[""']",
            @"href=[""']([^""']*\.(?:zip|gz|xml)(\?[^""']*)?)[""']"
        };

                foreach (var pattern in linkPatterns)
                {
                    var matches = Regex.Matches(htmlContent, pattern, RegexOptions.IgnoreCase);

                    foreach (Match match in matches)
                    {
                        var url = match.Groups[1].Value;
                        var fileName = match.Groups.Count > 2 ? CleanHtmlText(match.Groups[2].Value) : Path.GetFileName(url);

                        if (string.IsNullOrEmpty(fileName) || fileName.Contains("Download/"))
                            fileName = Path.GetFileName(url);

                        if (!string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(fileName))
                        {
                            // ✅ תיקון URL יחסי לאבסולוטי (כדי שיכלול /Download/)
                            if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                            {
                                if (!url.Contains("Download/"))
                                    url = SUPER_PHARM_BASE_URL.TrimEnd('/') + "/Download/" + url.TrimStart('/');
                                else
                                    url = SUPER_PHARM_BASE_URL.TrimEnd('/') + "/" + url.TrimStart('/');
                            }

                            files.Add(new SuperPharmFileInfo
                            {
                                FileName = fileName,
                                Date = targetDate.ToString("dd/MM/yyyy"),
                                Category = DetermineFileType(fileName, ""),
                                BranchName = ExtractStoreFromFileName(fileName, ""),
                                DownloadUrl = url
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ שגיאה בחיפוש קישורים: {ex.Message}");
            }

            return files;
        }

        /// <summary>
        /// פרסור מבנה div
        /// </summary>
        private List<SuperPharmFileInfo> ParseDivStructure(string htmlContent, DateTime targetDate)
        {
            var files = new List<SuperPharmFileInfo>();

            try
            {
                // חיפוש בתוך divs עם class מיוחד
                var divPattern = @"<div[^>]*class=[""'][^""']*file[^""']*[""'][^>]*>(.*?)</div>";
                var divs = Regex.Matches(htmlContent, divPattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);

                foreach (Match div in divs)
                {
                    var divContent = div.Groups[1].Value;

                    // חיפוש קישור בתוך הdiv
                    var linkMatch = Regex.Match(divContent, @"href=[""']([^""']+)[""']", RegexOptions.IgnoreCase);
                    var nameMatch = Regex.Match(divContent, @">([^<]+\.(zip|gz|xml))<", RegexOptions.IgnoreCase);

                    if (linkMatch.Success && nameMatch.Success)
                    {
                        var url = linkMatch.Groups[1].Value;
                        var fileName = CleanHtmlText(nameMatch.Groups[1].Value);

                        if (!url.StartsWith("http"))
                        {
                            url = SUPER_PHARM_BASE_URL.TrimEnd('/') + "/" + url.TrimStart('/');
                        }

                        files.Add(new SuperPharmFileInfo
                        {
                            FileName = fileName,
                            Date =targetDate.ToString("dd/MM/yyyy"),
                            Category = DetermineFileType(fileName, ""),
                            BranchName = ExtractStoreFromFileName(fileName, ""),
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

        /// <summary>
        /// חילוץ קישור הורדה - גרסה משופרת
        /// </summary>
        private string ExtractDownloadUrl(string cellContent)
        {
            try
            {
                var patterns = new[]
                {
                    @"href=[""']([^""']+)[""']",
                    @"onclick=[""'][^""']*window\.open\([""']([^""']+)[""']",
                    @"data-url=[""']([^""']+)[""']"
                };

                foreach (var pattern in patterns)
                {
                    var match = Regex.Match(cellContent, pattern, RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        var url = match.Groups[1].Value;

                        // תיקון URL יחסי לאבסולוטי
                        if (!string.IsNullOrEmpty(url) && !url.StartsWith("http"))
                        {
                            url = SUPER_PHARM_BASE_URL.TrimEnd('/') + "/" + url.TrimStart('/');
                        }

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
        /// המרת קבצים מפורמט סופר פארם לפורמט סטנדרטי
        /// </summary>
        private List<FileMetadata> ConvertToStandardFormat(List<SuperPharmFileInfo> superPharmFiles)
        {
            var result = new List<FileMetadata>();

            foreach (var file in superPharmFiles)
            {
                try
                {
                    var convertedFile = new FileMetadata
                    {
                        FileNm = file.FileName,
                        DateFile = file.Date, // כבר תואם ל-targetDate
                        WStore = ExtractStoreFromFileName(file.FileName, file.BranchName),
                        WFileType = DetermineFileType(file.FileName, file.Category),
                        Company = "סופר פארם",
                        LastUpdateDate = file.Date,
                        LastUpdateTime = "",
                        FileType = GetFileExtension(file.FileName)
                    };

                    result.Add(convertedFile);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"      ⚠️ שגיאה בהמרת קובץ {file.FileName}: {ex.Message}");
                }
            }

            // מיון לפי שעת הקובץ בשם (אם יש)
                    return result
            .GroupBy(f => new { f.WFileType, f.WStore })           // קיבוץ לפי קטגוריה + סניף
            .Select(g => g.OrderByDescending(f => ExtractTimeFromFileName(f.FileNm)).First())
            .ToList();

        }

        /// <summary>
        /// הורדה ראשית של הרשת
        /// </summary>
        public async Task<DownloadResult> DownloadChain(ChainConfig config, string date)
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
                Console.WriteLine($"\n🏪 מתחיל הורדה: {ChainName}");
                Console.WriteLine($"🎯 מטרה: הורדה לפי עמודים (totalPages)");

                var targetDate = DateTime.Parse(date);
                string currentDate = targetDate.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
                string encodedDate = Uri.EscapeDataString(currentDate);

                var chainDir = Path.Combine(BaseDownloadPath, "SuperPharm");
                Directory.CreateDirectory(chainDir);

                string baseUrl = $"{SUPER_PHARM_BASE_URL}?Date-equals={encodedDate}";

                // --- עמוד ראשון: גם פרסור וגם חישוב totalPages
                Console.WriteLine("      📄 קורא עמוד 1...");
                var firstResponse = await _httpClient.GetAsync(baseUrl);

                if (!firstResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine($"      ❌ שגיאת HTTP בעמוד הראשון: {firstResponse.StatusCode}");
                    return result;
                }

                var firstHtml = await firstResponse.Content.ReadAsStringAsync();
                int totalPages = GetTotalPages(firstHtml);

                Console.WriteLine($"      📚 נמצאו {totalPages} עמודים");

                // לולאה על כל העמודים
                for (int page = 1; page <= totalPages; page++)
                {
                    string pageUrl = page == 1
                        ? baseUrl
                        : $"{baseUrl}&page={page}";

                    if (page > 1)
                    {
                        Console.WriteLine($"      📄 קורא עמוד {page}/{totalPages}...");
                        var response = await _httpClient.GetAsync(pageUrl);

                        if (!response.IsSuccessStatusCode)
                        {
                            Console.WriteLine($"      ⚠️ שגיאת HTTP בעמוד {page}: {response.StatusCode}");
                            break;
                        }

                        firstHtml = await response.Content.ReadAsStringAsync();
                    }

                    var rawFiles = ParseSuperPharmHtml(firstHtml, targetDate);

                    if (!rawFiles.Any())
                    {
                        Console.WriteLine($"      ⚠️ עמוד {page} ריק — ממשיך");
                        continue;
                    }

                    var pageFiles = ConvertToStandardFormat(rawFiles);

                    Console.WriteLine($"      📄 עמוד {page}: נמצאו {pageFiles.Count} קבצים");

                    // ניתוח
                    AnalyzeAvailableFiles(pageFiles);

                    // Stores
                    result.StoresFiles += await DownloadStoresFiles(pageFiles, chainDir);

                    // סניפים
                    var stores = GetUniqueStores(pageFiles);

                    if (stores.Any())
                    {
                        result.PriceFiles += await DownloadPriceFiles(pageFiles, stores, chainDir);
                        result.PromoFiles += await DownloadPromoFiles(pageFiles, stores, chainDir);
                    }

                    //await Task.Delay(_random.Next(1000, 2500));
                }

                result.DownloadedFiles =
                    result.StoresFiles + result.PriceFiles + result.PromoFiles;

                result.Success = true;
                result.Duration = (DateTime.Now - startTime).TotalSeconds;

                Console.WriteLine(
                    $"      📊 סיכום סופר פארם: {result.StoresFiles} Stores + {result.PriceFiles} Prices + {result.PromoFiles} Promos = {result.DownloadedFiles}");

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


        /// <summary>
        /// ניתוח קבצים זמינים
        /// </summary>
        private void AnalyzeAvailableFiles(List<FileMetadata> files)
        {
            var types = files.GroupBy(f => DetermineFileType(f.FileNm, "")).ToDictionary(g => g.Key, g => g.Count());
            Console.WriteLine($"      🔍 ניתוח קבצים מסופר פארם:");
            foreach (var type in types)
                Console.WriteLine($"         📄 {type.Key}: {type.Value}");
        }

        /// <summary>
        /// הורדת קבצי Stores
        /// </summary>
        private async Task<int> DownloadStoresFiles(List<FileMetadata> availableFiles, string chainDir)
        {
            Console.WriteLine($"      📋 מחפש קבצי Stores...");

            var storesFiles = availableFiles
                .Where(f => f.FileNm.ToLower().Contains("stores"))
                .OrderByDescending(f => f.FileNm.ToLower().Contains("storesfull") ? 1 : 0)
                .ThenByDescending(f => ExtractTimeFromFileName(f.FileNm))
                .ToList();

            if (!storesFiles.Any())
            {
                Console.WriteLine($"      ⚠️ לא נמצאו קבצי Stores");
                return 0;
            }
            
            var latestStores = storesFiles.First();
            var success = await DownloadAndSaveFileWithRetry(latestStores, chainDir, "Stores");
            //Console.WriteLine($"      🎯 מוריד: {latestStores.FileNm}");

            //var success = await DownloadAndSaveFile(latestStores, chainDir, "Stores");
            return success ? 1 : 0;
        }

        /// <summary>
        /// הורדת קבצי מחירים עם retry
        /// </summary>
        private async Task<int> DownloadPriceFiles(List<FileMetadata> availableFiles, List<string> stores, string chainDir)
        {
            Console.WriteLine($"      💰 מוריד קבצי Price...");

            int downloaded = 0;
            var limitedStores = stores.ToList();

            foreach (var store in limitedStores)
            {
                // חיפוש קבצי PriceFull
                var priceFullFiles = availableFiles
                    .Where(f => f.FileNm.ToLower().Contains("pricefull") &&
                               ExtractStoreFromFileName(f.FileNm, "") == store)
                    .OrderByDescending(f => ExtractTimeFromFileName(f.FileNm))
                    .ToList();

                // חיפוש קבצי Price רגיל
                var priceFiles = availableFiles
                    .Where(f => f.FileNm.ToLower().Contains("price") &&
                               !f.FileNm.ToLower().Contains("pricefull") &&
                               !f.FileNm.ToLower().Contains("promo") &&
                               ExtractStoreFromFileName(f.FileNm, "") == store)
                    .OrderByDescending(f => ExtractTimeFromFileName(f.FileNm))
                    .ToList();

                // הורדת PriceFull אם קיים
                if (priceFullFiles.Any())
                {
                    var latestPriceFull = priceFullFiles.First();
                    Console.WriteLine($"         🎯 סניף {store} PriceFull: {latestPriceFull.FileNm}");

                    //await Task.Delay(_random.Next(500, 1500));
                    var success = await DownloadAndSaveFileWithRetry(latestPriceFull, chainDir, "PriceFull");
                    if (success) downloaded++;
                }

                // הורדת Price רגיל אם קיים
                if (priceFiles.Any())
                {
                    var latestPrice = priceFiles.First();
                    Console.WriteLine($"         🎯 סניף {store} Price: {latestPrice.FileNm}");

                    //await Task.Delay(_random.Next(500, 1500));
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
        private async Task<int> DownloadPromoFiles(List<FileMetadata> availableFiles, List<string> stores, string chainDir)
        {
            Console.WriteLine($"      🎁 מחפש קבצי Promo...");

            int downloaded = 0;
            var limitedStores = stores.ToList();
            foreach (var store in limitedStores)
            {
                // חיפוש קבצי PromoFull
                var promoFullFiles = availableFiles
                    .Where(f => f.FileNm.ToLower().Contains("promofull") &&
                               ExtractStoreFromFileName(f.FileNm, "") == store)
                    .OrderByDescending(f => ExtractTimeFromFileName(f.FileNm))
                    .ToList();

                // חיפוש קבצי Promo רגיל
                var promoFiles = availableFiles
                    .Where(f => f.FileNm.ToLower().Contains("promo") &&
                               !f.FileNm.ToLower().Contains("promofull") &&
                               ExtractStoreFromFileName(f.FileNm, "") == store)
                    .OrderByDescending(f => ExtractTimeFromFileName(f.FileNm))
                    .ToList();

                // הורדת PromoFull אם קיים
                if (promoFullFiles.Any())
                {
                    var latestPromoFull = promoFullFiles.First();
                    Console.WriteLine($"         🎯 סניף {store} PromoFull: {latestPromoFull.FileNm}");

                    //await Task.Delay(_random.Next(500, 1500));
                    var success = await DownloadAndSaveFileWithRetry(latestPromoFull, chainDir, "PromoFull");
                    if (success) downloaded++;
                }

                // הורדת Promo רגיל אם קיים
                if (promoFiles.Any())
                {
                    var latestPromo = promoFiles.First();
                    Console.WriteLine($"         🎯 סניף {store} Promo: {latestPromo.FileNm}");

                    //await Task.Delay(_random.Next(500, 1500));
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

        private async Task<bool> DownloadAndSaveFileWithRetry(FileMetadata fileInfo, string chainDir, string fileType, int maxRetries = 3)
        {
            string? oldFilePath = null; 

            try
            {

                var fileName = Path.GetFileName(fileInfo.FileNm);
                fileName = fileName.Split('?')[0];
                var storeId = ExtractStoreFromFileName(fileName, "");
                var newDateStr = ExtractTimeFromFileName(fileName);

                if (!DateTime.TryParseExact(newDateStr, "yyyyMMddHHmm", null, System.Globalization.DateTimeStyles.None, out var newDate))
                    newDate = DateTime.MinValue;

                var searchPattern = $"*-{storeId}-*.*";
                //Console.WriteLine($"🔎 מחפש קובץ קיים לפי תבנית: {searchPattern} בתוך {chainDir}");
                //Console.WriteLine($"📂 נתיב מלא לתיקייה: {Path.GetFullPath(chainDir)}");

                var typeDir = Path.Combine(chainDir, fileType);
                Directory.CreateDirectory(typeDir);
                var existingFiles = Directory.GetFiles(typeDir, searchPattern);
                Console.WriteLine($"📄 נמצאו {existingFiles.Length} קבצים תואמים.");

                if (existingFiles.Any())
                {
                    Console.WriteLine("הצליח-----");

                    var existingFile = existingFiles.First();
                    var existingDateStr = ExtractTimeFromFileName(Path.GetFileName(existingFile));
                    existingDateStr = existingDateStr.Split('_')[0];
                    if (DateTime.TryParseExact(existingDateStr, "yyyyMMddHHmm", null, System.Globalization.DateTimeStyles.None, out var existingDate))
                    {
                        Console.WriteLine($"🔍 השוואת תאריכים: חדש = {newDateStr} ({newDate}), ישן = {existingDateStr} ({existingDate})");

                        if (newDate <= existingDate)
                        {
                            Console.WriteLine($"📁 הסניף {storeId} כבר מעודכן ({existingDate:yyyy-MM-dd HH:mm}), מדלג על הורדה.");
                            return true;
                        }
                        else
                        {
                            // נשמור את הנתיב למחיקה רק אחרי הורדה מוצלחת
                            oldFilePath = existingFile;
                            Console.WriteLine($"🆕 נמצא קובץ חדש לסניף {storeId}, נמחוק את הישן רק לאחר שהחדש ירד בהצלחה.");
                        }
                    }
                }

                // הורדה עם ניסיונות חוזרים
                for (int attempt = 1; attempt <= maxRetries; attempt++)
                {
                    try
                    {
                        if (attempt > 1)
                        {
                            Console.WriteLine($"         🔄 ניסיון {attempt}/{maxRetries}: {fileInfo.FileNm}");
                            //await Task.Delay(_random.Next(2000, 5000));
                        }

                        var success = await DownloadAndSaveFile(fileInfo, chainDir, fileType);
                        if (success)
                        {
                            // רק כאן מוחקים את הקובץ הישן
                            if (!string.IsNullOrEmpty(oldFilePath) && File.Exists(oldFilePath))
                            {
                                File.Delete(oldFilePath);
                                Console.WriteLine($"🧹 נמחק הקובץ הישן: {Path.GetFileName(oldFilePath)}");
                            }

                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"         ⚠️ ניסיון {attempt} נכשל: {ex.Message}");
                        if (attempt == maxRetries)
                            Console.WriteLine($"         ❌ נכשל לאחר {maxRetries} ניסיונות: {fileInfo.FileNm}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"         ⚠️ שגיאה בתהליך הורדה: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// הורדה ושמירת קובץ - גרסה מיוחדת לסופר פארם
        /// </summary>
        private async Task<bool> DownloadAndSaveFile(FileMetadata fileInfo, string chainDir, string fileType)
        {
            try
            {
                var typeDir = Path.Combine(chainDir, fileType);
                Directory.CreateDirectory(typeDir);

                // בסופר פארם, ה-URL מגיע מוכן להורדה מה-HTML parsing
                var downloadUrl = await GetDirectDownloadUrl(fileInfo.FileNm);
                
                if (string.IsNullOrEmpty(downloadUrl))
                {
                    Console.WriteLine($"         ❌ לא נמצא קישור הורדה עבור {fileInfo.FileNm}");
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
        /// קבלת קישור הורדה ישיר - צריך לחזור ל-HTML לקבלת ה-URL המעודכן
        /// </summary>

        private async Task<string> GetDirectDownloadUrl(string fileName)
        {
            try
            {
                // אם הקובץ הוא כבר כתובת מלאה, נשתמש בה כמות שהיא
                string url = fileName.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? fileName
                    : $"{SUPER_PHARM_BASE_URL.TrimEnd('/')}/Download/{fileName.TrimStart('/')}";

                // נוודא שהקובץ באמת קיים לפני הורדה
                var response = await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
                if (response.IsSuccessStatusCode)
                    return url;

                Console.WriteLine($"⚠️ לא נמצא קישור הורדה עבור {fileName}");
                return "";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ שגיאה ב-GetDirectDownloadUrl: {ex.Message}");
                return "";
            }
        }


        /// <summary>
        /// חילוץ ושמירת XML - זהה לגרסה הרגילה
        /// </summary>
        private async Task<int> ExtractAndSaveXml(byte[] fileBytes, FileMetadata fileInfo, string typeDir)
        {
            try
            {
                int savedCount = 0;

                // ניקוי שם קובץ מתווים אסורים
                string Sanitize(string name)
                {
                    foreach (var c in Path.GetInvalidFileNameChars())
                        name = name.Replace(c, '_');
                    return name;
                }

                // יצירת שם קובץ XML חד משמעי
                string BuildXmlName(string originalName)
                {
                    var clean = Sanitize(originalName);
                    clean = clean.Replace(".gz", "").Replace(".zip", "").Replace(".xml", "");
                    return clean + ".xml";   // ❗ תמיד מסתיים ב-xml
                }

                // ---------------- ZIP ----------------
                if (IsZipFile(fileBytes))
                {
                    using var zipStream = new MemoryStream(fileBytes);
                    using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

                    foreach (var entry in archive.Entries)
                    {
                        if (!string.IsNullOrEmpty(entry.Name))
                        {
                            var targetName = BuildXmlName(entry.Name);
                            var xmlPath = Path.Combine(typeDir, targetName);

                            entry.ExtractToFile(xmlPath, true);
                            savedCount++;
                        }
                    }
                }

                // ---------------- GZ ----------------
                else if (IsGzFile(fileBytes))
                {
                    using var gzStream = new MemoryStream(fileBytes);
                    using var decompressionStream = new GZipStream(gzStream, CompressionMode.Decompress);
                    using var reader = new StreamReader(decompressionStream);

                    var xmlContent = await reader.ReadToEndAsync();

                    var xmlName = BuildXmlName(fileInfo.FileNm);
                    var xmlPath = Path.Combine(typeDir, xmlName);

                    await File.WriteAllTextAsync(xmlPath, xmlContent);
                    savedCount = 1;
                }

                // ---------------- RAW XML ----------------
                else
                {
                    var xmlName = BuildXmlName(fileInfo.FileNm);
                    var xmlPath = Path.Combine(typeDir, xmlName);

                    await File.WriteAllBytesAsync(xmlPath, fileBytes);
                    savedCount = 1;
                }

                Console.WriteLine($"         ✅ נשמרו {savedCount} קבצי XML ({fileInfo.FileNm})");
                return savedCount;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"         ❌ שגיאה בחילוץ: {ex.Message}");
                return 0;
            }
        }


        // ========== פונקציות עזר ==========

        private List<string> GetUniqueStores(List<FileMetadata> files)
        {
            return files
                .Where(f => f.FileNm.ToLower().Contains("price") || f.FileNm.ToLower().Contains("promo"))
                .Select(f => ExtractStoreFromFileName(f.FileNm, ""))
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct()
                .OrderBy(s => s)
                .ToList();
        }

        private string DetermineFileType(string fileName, string category)
        {
            var lowerName = fileName.ToLower();
            var lowerCategory = category.ToLower();

            if (lowerName.Contains("storesfull") || lowerCategory.Contains("storesfull")) return "StoresFull";
            if (lowerName.Contains("pricefull") || lowerCategory.Contains("pricefull")) return "PriceFull";
            if (lowerName.Contains("promofull") || lowerCategory.Contains("promofull")) return "PromoFull";
            if (lowerName.Contains("stores") || lowerCategory.Contains("stores")) return "Stores";
            if (lowerName.Contains("price") || lowerCategory.Contains("price")) return "Price";
            if (lowerName.Contains("promo") || lowerCategory.Contains("promo")) return "Promo";
            return "Unknown";
        }

        private string ExtractStoreFromFileName(string fileName, string branchName)
        {
            try
            {
                // אם יש שם סניף בנתונים, נשתמש בו
                if (!string.IsNullOrEmpty(branchName))
                    return branchName;

                // אחרת ננסה לחלץ מהשם
                var parts = fileName.Split('-');
                return parts.Length >= 2 ? parts[1] : "";
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
                var parts = fileName.Split('-');
                return parts.Length >= 3 ? parts[parts.Length - 1].Replace(".gz", "").Replace(".xml", "") : "000000000000";
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

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}