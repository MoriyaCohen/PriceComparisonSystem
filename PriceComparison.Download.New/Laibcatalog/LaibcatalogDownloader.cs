using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using System.Linq;
using PriceComparison.Download.New.Storage;
using PriceComparison.Download.New.BinaProject;
using System.Reflection.Metadata;
using System.Text.RegularExpressions;
using System.Web;
using System.IO.Compression;
using System.Collections;
using System.Security.Claims;
using Microsoft.Extensions.FileSystemGlobbing;
using System.Xml;
using PriceComparison.Download.New.Shufersal;


namespace PriceComparison.Download.New.Laibcatalog

{
    public class FileInfoData
    {
        public string FileName { get; set; } = "";
        public string Chain { get; set; } = "";
        public string Branch { get; set; } = "";
        public string Type { get; set; } = "";
        public string Extension { get; set; } = "";
        public string Size { get; set; } = "";
        public string Date { get; set; } = "";
        public string downloadUrl { get; set; } = "";
    }

    public class LaibcatalogDownloader 
    {
        private readonly HttpClient _httpClient;
        private readonly FileManager _fileManager;
        private readonly Random _random = new();
        private const string BaseDownloadPath = "Downloads";
        private string LAIBCATALOG_BASE_URL="";

        public LaibcatalogDownloader(HttpClient httpClient, FileManager fileManager)
        {
            _httpClient = httpClient;
            _fileManager = fileManager;
            SetupHttpClient();
        }

        //public string ChainName => "ח.כהן וויקטורי ומחסני השוק";
        //public string ChainId => "laibcatalog";
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
            return chainId.Equals("laibcatalog", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<List<FileInfoData>> GetAvailableFiles(string date,string _BASE_URL)
        {
            try
            {
                var targetDate = DateTime.Parse(date);
                string currentDate = targetDate.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
                string encodedDate = Uri.EscapeDataString(currentDate);

                Console.WriteLine($"      🌐 מתחבר ליבקטלוג עם תמיכה ב-pagination: {_BASE_URL}");
                var allFiles = new List<FileInfoData>();

                var response = await _httpClient.GetAsync(_BASE_URL);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine("שגיאה בחיבור");
                }

                var htmlContent = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrWhiteSpace(htmlContent))
                {
                    Console.WriteLine("      ⚠️ תגובה ריקה מהשרת בעמוד ");
                }
                var pageFiles = ParseLaibcatalogHtml(htmlContent,_BASE_URL);
                Console.WriteLine("-----------------");
                //foreach (var item in pageFiles)
                //{
                //    Console.WriteLine($"{item.Chain} | {item.Type} | {item.FileName}");
                //    Console.WriteLine("----------");
                //}
                allFiles.AddRange(pageFiles);

                Console.WriteLine($"      ✅ סה\"כ נמצאו {allFiles.Count} קבצים זמינים מכל העמודים");

                var convertedFiles = ConvertToStandardFormat(allFiles);

                Console.WriteLine($"      🔍 לאחר המרה וסינון: {convertedFiles.Count} קבצים רלוונטיים");

                return convertedFiles;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"      💥 שגיאה בקבלת קבצים: {ex.Message}");
                Console.WriteLine($"      💥 פרטי שגיאה: {ex.StackTrace}");
                return new List<FileInfoData>();
            }
            //return JsonSerializer.Deserialize<List<FileMetadata>>(response) ?? new List<FileMetadata>();
        }
        private List<FileInfoData> ConvertToStandardFormat(List<FileInfoData> laibcatalog)
        {
            var result = new List<FileInfoData>();

            // קבלת התאריך של היום
            var today = DateTime.Now;

            Console.WriteLine($"      🔍 בודק {laibcatalog.Count} קבצים לתאריך היום: {today:dd/MM/yyyy}");

            // דיבוג - הצגת כמה דוגמאות של תאריכים
            //var sampleDates = laibcatalog.Take(5).Select(f => f.Date).ToList();
            //Console.WriteLine($"      📅 דוגמאות תאריכים מהשרת: {string.Join(", ", sampleDates)}");

            foreach (var file in laibcatalog)
            {
                try
                {
                    // בדיקת תאריך - האם הקובץ מהיום (גרסה מורחבת)
                    if (IsFromTodayExtended(file.Date, today))
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
            return result.OrderByDescending(f => ParseUpdateTimeForSorting(f.Date)).ToList();
        }
        private bool IsFromTodayExtended(string updateTimeStr, DateTime today)
        {
            if (string.IsNullOrEmpty(updateTimeStr))
                return false;

            var culture = new System.Globalization.CultureInfo("he-IL");

            // כל הפורמטים הרלוונטיים
            var formats = new[]
            {
        "dd/MM/yyyy HH:mm:ss",
        "d/M/yyyy HH:mm:ss",
        "dd/MM/yyyy",
        "d/M/yyyy",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-dd"
    };

            // ניסיון פרסור
            if (DateTime.TryParseExact(updateTimeStr, formats, culture,
                System.Globalization.DateTimeStyles.None, out var updateTime))
            {
                return updateTime.Date == today.Date;
            }

            // ניסיון פרסור רגיל (ליתר ביטחון)
            if (DateTime.TryParse(updateTimeStr, culture,
                System.Globalization.DateTimeStyles.None, out updateTime))
            {
                return updateTime.Date == today.Date;
            }

            return false;
        }



        private List<FileInfoData> ParseLaibcatalogHtml(string htmlContent, string baseUrl)
        {
            var files = new List<FileInfoData>();

            try
            {
                if (baseUrl.Contains("laibcatalog.co.il", StringComparison.OrdinalIgnoreCase))
                {
                    // הקוד הקיים שלך עם Regex
                    var tablePattern = @"<div id=""download_content"">.*?<table>(?<TableContent>.*?)</table>.*?</div>";
                    var tableMatch = Regex.Match(htmlContent, tablePattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);

                    if (!tableMatch.Success)
                        return files;

                    var tableContent = tableMatch.Groups["TableContent"].Value;

                    var rowPattern = @"<tr>\s*<td>(?<FileName>.*?)</td>\s*<td>(?<Chain>.*?)</td>\s*<td>(?<Branch>.*?)</td>\s*<td>(?<Type>.*?)</td>\s*<td>(?<Extension>.*?)</td>\s*<td>(?<Size>.*?)</td>\s*<td>(?<Date>.*?)</td>\s*<td>.*?<a\s+href=['""](?<Url>[^'""]+)['""].*?>.*?</a>\s*</td>\s*</tr>";
                    var matches = Regex.Matches(tableContent, rowPattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);

                    foreach (Match match in matches)
                    {
                        var file = new FileInfoData
                        {
                            FileName = CleanHtmlText(match.Groups["FileName"].Value),
                            Chain = CleanHtmlText(match.Groups["Chain"].Value),
                            Branch = CleanHtmlText(match.Groups["Branch"].Value),
                            Type = CleanHtmlText(match.Groups["Type"].Value),
                            Extension = CleanHtmlText(match.Groups["Extension"].Value),
                            Size = CleanHtmlText(match.Groups["Size"].Value),
                            Date = CleanHtmlText(match.Groups["Date"].Value),
                            downloadUrl = match.Groups["Url"].Value.Replace("\\", "/")
                        };
                        files.Add(file);
                    }

                    files = files.GroupBy(f => f.FileName)
                                 .Select(g => g.OrderByDescending(x => ParseUpdateTimeForSorting(x.Date)).First())
                                 .ToList();
                }
                else if (baseUrl.Contains("141.226.203.152"))
                {
                    Console.WriteLine("נכנס");


                    // Regex תופס את כל השורות אחרי השורה הראשונה
                    var rowPattern = @"<tr>\s*<td>(?<FileName>.*?)</td>\s*<td>(?<Chain>.*?)</td>\s*<td>(?<Branch>.*?)</td>\s*<td>(?<Type>.*?)</td>\s*<td>(?<Extension>.*?)</td>\s*<td>(?<Size>.*?)</td>\s*<td>(?<Date>.*?)</td>\s*<td>.*?<a\s+href=['""](?<Url>[^'""]+)['""].*?>.*?</a>\s*</td>\s*</tr>";

                    var matches = Regex.Matches(htmlContent, rowPattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);

                    foreach (Match match in matches)
                    {
                        var file = new FileInfoData
                        {
                            FileName = match.Groups["FileName"].Value.Trim(),
                            Chain = match.Groups["Chain"].Value.Trim(),
                            Branch = match.Groups["Branch"].Value.Trim(),
                            Type = match.Groups["Type"].Value.Trim(),
                            Extension = match.Groups["Extension"].Value.Trim(),
                            Size = match.Groups["Size"].Value.Trim(),
                            Date = match.Groups["Date"].Value.Trim(),
                            downloadUrl = match.Groups["Url"].Value.Replace("\\", "/")
                        };
                        files.Add(file);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ שגיאה בפרסור HTML Laibcatalog: {ex.Message}");
            }

            return files;
        }



        // פונקציה שמנקה HTML
        private string CleanHtmlText(string html)
        {
            if (string.IsNullOrEmpty(html)) return "";
            var text = Regex.Replace(html, @"<[^>]+>", "");
            return System.Net.WebUtility.HtmlDecode(text).Trim();
        }


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
     
    

        public async Task<DownloadResult> DownloadChain(ChainConfig config, string date)
        {
            LAIBCATALOG_BASE_URL = config.BaseUrl;
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
                Console.WriteLine($"🎯 מטרה: קבצים עדכניים מ-HTML parsing עם pagination");


                var chainDir =BaseDownloadPath;
                Directory.CreateDirectory(chainDir);

                var availableFiles = await GetAvailableFiles(date, LAIBCATALOG_BASE_URL);
                if (!availableFiles.Any())
                {
                    result.ErrorMessage = "לא נמצאו קבצים זמינים למרות מספר ניסיונות";
                    Console.WriteLine($"      ❌ לא נמצאו קבצים זמינים להיום");
                    result.Duration = (DateTime.Now - startTime).TotalSeconds;
                    return result;
                }
                Console.WriteLine($"      ✅ נמצאו {availableFiles.Count} קבצים זמינים  ");

                AnalyzeAvailableFiles(availableFiles);

                int totalDownloaded = 0;
               
                var chains = GetUniqueNetworks(availableFiles);
                foreach (var c in chains)
                {
                    var chainSpecificDir = Path.Combine(chainDir, c); // יצירת תיקיה לרשת
                    Directory.CreateDirectory(chainSpecificDir);

                    result.StoresFiles= await DownloadStoresFiles(availableFiles, chainSpecificDir,c );

                var stores = GetUniqueStores(availableFiles,c);

                if (stores.Any())
                {
                    // שלב 3: הורדת קבצי מחירים
                    result.PriceFiles = await DownloadPriceFiles(availableFiles, stores, chainSpecificDir, c);

                    // שלב 4: הורדת קבצי מבצעים
                    result.PromoFiles = await DownloadPromoFiles(availableFiles, stores, chainSpecificDir,c);
                }
                  }

                result.DownloadedFiles = result.StoresFiles + result.PriceFiles + result.PromoFiles;
                result.Success = true;
                result.Duration = (DateTime.Now - startTime).TotalSeconds;

                Console.WriteLine($"      📊 סיכום : {result.StoresFiles} Stores + {result.PriceFiles} Prices + {result.PromoFiles} Promos = {result.DownloadedFiles} סה\"כ");
                Console.WriteLine($"      ✅ {config.Name}: הורדה הושלמה בהצלחה");

                return result;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                result.Duration = (DateTime.Now - startTime).TotalSeconds;
                Console.WriteLine($"      ❌ שגיאה כללית ב{config.Name}: {ex.Message}");
                return result;
            }
          
        }
        private List<string> GetUniqueNetworks(List<FileInfoData> availableFiles)
        {
            // מחזיר רשימה של שמות רשתות ייחודיות לפי מה שמופיע בקבצים
            var networks = availableFiles
                .Select(f => f.Chain?.Trim())   // לוודא שאין רווחים מיותרים
                .Where(name => !string.IsNullOrEmpty(name)) // סינון שמות ריקים
                .Distinct() // שמות ייחודיים
                .OrderBy(name => name) // אפשר למיין לפי סדר אלפביתי
                .ToList();

            Console.WriteLine($"זוהו {networks.Count} רשתות: {string.Join(", ", networks)}");

            return networks;
        }
        private List<string> GetUniqueStores(List<FileInfoData> files,String c)
        {
            return files
                .Where(f => (f.Type.ToLower().Contains("מבצעים") || f.Type.ToLower().Contains("מחירים")) && f.Chain == c)
                .Select(f => ExtractStoreFromBranch(f.Branch,""))
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct()
                .OrderBy(s => int.Parse(s))
                .ToList();


        }
        private void AnalyzeAvailableFiles(List<FileInfoData> files)
        {
            var types = files.GroupBy(f => DetermineFileType(f.FileName, f.Type)).ToDictionary(g => g.Key, g => g.Count());
            Console.WriteLine($"      🔍 ניתוח קבצים מליבפרוגקט:");
            foreach (var type in types)
                Console.WriteLine($"         📄 {type.Key}: {type.Value}");
        }
        private string DetermineFileType(string fileName, string category)
        {
            var lowerName = fileName.ToLower();
            var lowerCategory = category.ToLower();

            if (lowerCategory.Contains("סניפים") || lowerName.Contains("storesFull")) return "Stores";
            if (lowerCategory.Contains("pricesfull") || lowerName.Contains("pricefull")) return "PriceFull";
            if (lowerCategory.Contains("promosfull") || lowerName.Contains("promofull")) return "PromoFull";
            if (lowerCategory.Contains("prices") || lowerName.Contains("price")) return "Price";
            if (lowerCategory.Contains("promos") || lowerName.Contains("promo")) return "Promo";
            return "Unknown";
        }
        private async Task<int> DownloadPriceFiles(List<FileInfoData> availableFiles, List<string> stores, string chainDir, string chain)
        {
            Console.WriteLine($"      💰 מוריד קבצי Price...");
            int downloaded = 0;
            // הגבלה ל-5 סניפים לבדיקה
            var limitedStores = stores.ToList();

            foreach (var store in limitedStores)
            {
                // חיפוש קבצי PriceFull
                var priceFullFiles = availableFiles
               .Where(f => (f.Type.ToLower() == "מחירים מלא" || f.FileName.ToLower().Contains("pricefull")) &&
                           ExtractStoreFromBranch(f.Branch, "") == store &&
                           f.Chain == chain) // סינון לפי רשת
               .OrderByDescending(f => ParseUpdateTimeForSorting(f.Date))
               .ToList();

                //חיפוש קבצי Price רגיל
                var priceFiles = availableFiles
              .Where(f => (f.Type.ToLower() == "מחירים") &&
                          !f.FileName.ToLower().Contains("pricefull") &&
                          ExtractStoreFromBranch(f.Branch, "") == store &&
                          f.Chain == chain) // סינון לפי רשת
              .OrderByDescending(f => ParseUpdateTimeForSorting(f.Date))
              .ToList();

                Console.WriteLine($"         🔍 סניף {store}: {priceFullFiles.Count} PriceFull, {priceFiles.Count} Price");

                // הורדת PriceFull אם קיים
                if (priceFullFiles.Any())
                {
                    var latestPriceFull = priceFullFiles.First();
                    Console.WriteLine($"         🎯 סניף {store} PriceFull: {latestPriceFull.FileName}");

                    //await Task.Delay(_random.Next(500, 1500));
                    var success = await DownloadAndSaveFileWithRetry(latestPriceFull, chainDir, "PriceFull");
                    if (success) downloaded++;
                }

                //הורדת Price רגיל אם קיים
                if (priceFiles.Any())
                {
                    var latestPrice = priceFiles.First();
                    Console.WriteLine($"         🎯 סניף {store} Price: {latestPrice.FileName}");

                    //await Task.Delay(_random.Next(500, 1500));
                    var success = await DownloadAndSaveFileWithRetry(latestPrice, chainDir, "Price");
                    if (success) downloaded++;
                }
            }

            Console.WriteLine($"      💰 הורדו {downloaded} קבצי Price");
            return downloaded;
        }
      
        private async Task<int> DownloadPromoFiles(List<FileInfoData> availableFiles, List<string> stores, string chainDir,string chain)
        {
            Console.WriteLine($"      🎁 מחפש קבצי Promo...");
          
            int downloaded = 0;

            // הגבלה ל-5 סניפים לבדיקה
            var limitedStores = stores.ToList();

            foreach (var store in limitedStores)
            {
             
                // חיפוש קבצי PromoFull
                var promoFullFiles = availableFiles
              .Where(f => (f.Type.ToLower() == "מבצעים מלא" || f.FileName.ToLower().Contains("promofull")) &&
                          ExtractStoreFromBranch(f.Branch, "") == store &&
                          f.Chain == chain) // סינון לפי רשת
              .OrderByDescending(f => ParseUpdateTimeForSorting(f.Date))
              .ToList();
                Console.WriteLine(promoFullFiles.Count());
                //חיפוש קבצי Promo רגיל
                var promoFiles = availableFiles
                 .Where(f => (f.Type.ToLower() == "מבצעים") &&
                             !f.FileName.ToLower().Contains("promofull") &&
                             ExtractStoreFromBranch(f.Branch, "") == store &&
                             f.Chain == chain) // סינון לפי רשת
                 .OrderByDescending(f => ParseUpdateTimeForSorting(f.Date))
                 .ToList();

                Console.WriteLine($"         🔍 סניף {store}: {promoFullFiles.Count} PromoFull, {promoFiles.Count} Promo");

                // הורדת PromoFull אם קיים
                if (promoFullFiles.Any())
                {
                    var latestPromoFull = promoFullFiles.First();
                    Console.WriteLine($"         🎯 סניף {store} PromoFull: {latestPromoFull.FileName}");

                    //await Task.Delay(_random.Next(500, 1500));
                    var success = await DownloadAndSaveFileWithRetry(latestPromoFull, chainDir, "PromoFull");
                    if (success) downloaded++;
                }

                //הורדת Promo רגיל אם קיים
                if (promoFiles.Any())
                {
                    var latestPromo = promoFiles.First();
                    Console.WriteLine($"         🎯 סניף {store} Promo: {latestPromo.FileName}");

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
        private async Task<int> DownloadStoresFiles(List<FileInfoData> availableFiles, string chainDir,string chain)
        {
            Console.WriteLine($"      📋 מחפש קבצי Stores...");
            Console.WriteLine(chain);
            // חיפוש קבצי Stores לפי קטגוריה וגם לפי שם
            var storesFiles = availableFiles
              .Where(f => (f.Type.ToLower() == "stores" || f.FileName.ToLower().Contains("stores")) &&
                          f.Chain == chain)
              .OrderByDescending(f => f.FileName.ToLower().Contains("storesfull") ? 1 : 0)
              .ThenByDescending(f => ParseUpdateTimeForSorting(f.Date))
              .ToList();


            // אם לא נמצאו קבצי Stores רגילים
            if (!storesFiles.Any())
            {
                Console.WriteLine($"      ⚠️ לא נמצאו קבצי Stores");
                return 0;
            }
            // אם נמצאו קבצי Stores — נוריד את העדכני ביותר
            var latestStores = storesFiles.First();
            Console.WriteLine($"      🎯 מוריד: {latestStores.FileName}");

            var storesSuccess = await DownloadAndSaveFileWithRetry(latestStores, chainDir, "Stores");
            return storesSuccess ? 1 : 0;
        }
        private async Task<bool> DownloadAndSaveFileWithRetry(
       FileInfoData fileInfo,
       string chainDir,
       string fileType,
       int maxRetries = 3)
        {
            string? oldFilePath = null;

            try
            {
                var fileName = Path.GetFileName(fileInfo.FileName ?? "");
                Console.WriteLine(fileName);

                // חילוץ מזהה סניף
                //var storeId = ExtractStoreFromBranch(fileInfo.Branch);
                //if (string.IsNullOrEmpty(storeId))
                //{
                var storeId = ExtractStoreFromBranch("",fileName);
                //}
                Console.WriteLine(storeId);
                // חילוץ תאריך
                var newDate = ParseUpdateTimeForSorting(fileInfo.Date ?? "", fileInfo.FileName ?? "");
                if (newDate == DateTime.MinValue)
                {
                    Console.WriteLine($"⚠️ לא זוהה תאריך לקובץ {fileName}, לא ניתן להשוות.");
                }

                // חיפוש קובץ קיים



                var typeDir = Path.Combine(chainDir, fileType);
                Directory.CreateDirectory(typeDir);

                var searchPattern = $"*-{storeId}-*.*";
                Console.WriteLine(searchPattern);
                var existingFiles = Directory.GetFiles(typeDir, searchPattern);

                Console.WriteLine($"📄 נמצאו {existingFiles.Length} קבצים תואמים.");

                if (existingFiles.Any())
                {
                    var existingFile = existingFiles.First();
                    var existingDate = ExtractDateFromFileName(existingFile);

                    Console.WriteLine($"🔍 השוואת תאריכים: חדש = {newDate}, ישן = {existingDate}");

                    if (existingDate != DateTime.MinValue && newDate <= existingDate)
                    {
                        Console.WriteLine($"📁 סניף {storeId} כבר מעודכן ({existingDate:yyyy-MM-dd HH:mm}), מדלג על הורדה.");
                        return true;
                    }
                    else
                    {
                        oldFilePath = existingFile;
                        Console.WriteLine($"🆕 נמצא קובץ חדש לסניף {storeId}, נוריד ונטפל בישן לאחר ההורדה.");
                    }
                }

                // הורדה עם retry
                for (int attempt = 1; attempt <= maxRetries; attempt++)
                {
                    try
                    {
                        if (attempt > 1)
                        {
                            Console.WriteLine($"🔄 ניסיון {attempt}/{maxRetries}: {fileName}");
                            await Task.Delay(_random.Next(300, 600));
                        }

                        var success = await DownloadAndSaveFile(fileInfo, chainDir, fileType);
                        if (success)
                        {
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
                        Console.WriteLine($"⚠️ ניסיון {attempt} נכשל: {ex.Message}");
                        if (attempt == maxRetries)
                            Console.WriteLine($"❌ נכשל לאחר {maxRetries} ניסיונות: {fileName}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ שגיאה בתהליך הורדה: {ex.Message}");
            }

            return false;
        }
       


        private string ExtractStoreFromBranch(string branchName,string fileName)
        {
            
            if (branchName.Any())
            {

                var match = Regex.Match(branchName, @"(\d+)$"); // מחפש מספר בסוף השם
                if (match.Success)
                    return match.Groups[1].Value;

                // חלופה: מחפש מספר בכל מקום
                match = Regex.Match(branchName, @"\d+");
                if (match.Success)
                    return match.Groups[0].Value;
            }
            else
            {
                var match = Regex.Match(fileName, @"-(\d+)-");

                if (match.Success)
                {
                    return  match.Groups[1].Value; // זה יהיה "250"
                }
            }
            return "";
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

        private async Task<bool> DownloadAndSaveFile(FileInfoData fileInfo, string chainDir, string fileType)
        {
            try
            {
                var typeDir = Path.Combine(chainDir, fileType);
                Directory.CreateDirectory(typeDir);
                // יצירת תקיית רשת



                Console.WriteLine($"         📥 מוריד מ: {fileInfo.downloadUrl}");
                var urlToDownload = fileInfo.downloadUrl;
                if (!urlToDownload.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    urlToDownload = LAIBCATALOG_BASE_URL.TrimEnd('/') + "/" + urlToDownload.TrimStart('/');

                var response = await _httpClient.GetAsync(urlToDownload);

                //var response = await _httpClient.GetAsync(fileInfo.downloadUrl);

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
        private async Task<int> ExtractAndSaveXml(byte[] fileBytes, FileInfoData fileInfo, string typeDir)
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
