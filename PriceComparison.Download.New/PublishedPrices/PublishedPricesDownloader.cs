/*
 * הוראות התקנה:
 * 1. הוסף NuGet Package: Install-Package System.Text.Json
 * 2. אין צורך ב-HtmlAgilityPack - הקוד משתמש ב-Regex
 * 3. הוסף using directives נדרשים (כבר כלולים בקוד)
 * 4. הרץ את התוכנית - יווצר קובץ הגדרות אוטומטית
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
// using HtmlAgilityPack; // יש להתקין: Install-Package HtmlAgilityPack

namespace PriceComparison.Download.New.PublishedPrices
{
    // ========== מודלים בסיסיים ==========

    public class PublishedPricesConfig
    {
        public string Description { get; set; } = "";
        public string LastUpdated { get; set; } = "";
        public List<PublishedPricesChain> Chains { get; set; } = new();
    }

    public class PublishedPricesChain
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string LoginUrl { get; set; } = "";
        public string FileUrl { get; set; } = "";
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public PublishedPricesType Type { get; set; }
        public bool Enabled { get; set; }
        public string Notes { get; set; } = "";
    }

    public enum PublishedPricesType
    {
        CerberusStandard,       // url.publishedprices.co.il  
        CerberusRetail,         // url.retail.publishedprices.co.il
        PublishedPricesStandard, // publishedprices.co.il
        DirectFileAccess,       // לאתרים כמו laibcatalog.co.il
        CustomApi               // לאתרים מיוחדים כמו יינות ביתן
    }

    public class PublishedPricesDownloadResult
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

    public class FileEntry
    {
        public string Name { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
        public string Date { get; set; } = "";
        public string Type { get; set; } = "";
        public string Size { get; set; } = "";
        public DateTime ParsedDate { get; set; }
    }
    public class JsonFileItem
    {
        public string DT_RowId { get; set; } = "";
        public string fname { get; set; } = "";
        public string ftime { get; set; } = "";
        public string name { get; set; } = "";
        public JsonElement size { get; set; }
        public string time { get; set; } = "";
        public string type { get; set; } = "";
        public string value { get; set; } = "";
    }

    public class JsonRoot
    {
        public List<JsonFileItem> aaData { get; set; } = new();
        public string iTotalDisplayRecords { get; set; } = "0";
        public string iTotalRecords { get; set; } = "0";
        public int sEcho { get; set; }
    }

    // ========== מחלקת בסיס מתקדמת נגד חסימות ==========

    public abstract class PublishedPricesDownloaderBase : IDisposable
    {
        protected readonly HttpClient _httpClient;
        protected const string BaseDownloadPath = "Downloads\\";
        
        // ✅ משתנים לניהול אנטי-בוט מתקדם
        private static readonly SemaphoreSlim _downloadSemaphore = new(2, 2);
        private static int _requestCounter = 0;
        private static DateTime _lastRequestTime = DateTime.MinValue;
        private readonly Random _random = new();

        // ✅ רשימת User-Agents מתקדמת
        private static readonly List<string> UserAgents = new()
        {
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.0.0 Safari/537.36 Edg/119.0.0.0",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:121.0) Gecko/20100101 Firefox/121.0",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10.15; rv:121.0) Gecko/20100101 Firefox/121.0"
        };

        public abstract string ChainName { get; protected set; }
        public abstract string ChainId { get; protected set; }

        public abstract PublishedPricesType SiteType { get; }

        protected PublishedPricesDownloaderBase()
        {
            var handler = new HttpClientHandler()
            {
                CookieContainer = new CookieContainer(),
                UseCookies = true
            };

            _httpClient = new HttpClient(handler);
            SetupAdvancedHttpClient();
        }

        // ✅ הגדרת HttpClient מתקדמת נגד זיהוי בוט
        private void SetupAdvancedHttpClient()
        {
            _httpClient.DefaultRequestHeaders.Clear();

            var userAgent = UserAgents[_random.Next(UserAgents.Count)];
            _httpClient.DefaultRequestHeaders.Add("User-Agent", userAgent);
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
            _httpClient.DefaultRequestHeaders.Add("Cache-Control", "max-age=0");

            _httpClient.Timeout = TimeSpan.FromMinutes(15);

            Console.WriteLine($"      🎭 נבחר User-Agent: {userAgent.Substring(0, Math.Min(50, userAgent.Length))}...");
        }

        // ✅ עיכוב מתקדם נגד זיהוי בוט
        protected async Task AdvancedAntiDetectionDelay(string context = "", int baseMinMs = 2000, int baseMaxMs = 5000)
        {
            //await _downloadSemaphore.WaitAsync();

            //try
            //{
            //    var requestCount = Interlocked.Increment(ref _requestCounter);
            //    var timeSinceLastRequest = DateTime.Now - _lastRequestTime;

            //    var multiplier = 1.0;
            //    if (timeSinceLastRequest.TotalSeconds < 2)
            //    {
            //        multiplier = 2.0;
            //    }

            //    if (requestCount % 10 == 0)
            //    {
            //        multiplier = 3.0;
            //    }

            //    var minMs = (int)(baseMinMs * multiplier);
            //    var maxMs = (int)(baseMaxMs * multiplier);
            //    var delayMs = _random.Next(minMs, maxMs);
            //    var noise = _random.Next(-300, 300);
            //    delayMs = Math.Max(1000, delayMs + noise);

            //    Console.WriteLine($"      ⏳ {context} - ממתין {delayMs / 1000:F1} שניות (בקשה #{requestCount})...");

            //    await Task.Delay(delayMs);
            //    _lastRequestTime = DateTime.Now;
            //}
            //finally
            //{
            //    _downloadSemaphore.Release();
            //}
        }

        // ========== שיטות מופשטות ==========
        public abstract Task<string> LoginAsync(PublishedPricesChain config);
        public abstract Task<List<FileEntry>> GetFileListAsync(PublishedPricesChain config,string html);
        public abstract Task<bool> DownloadFileAsync(FileEntry file, string localPath);

        // ========== הורדה ראשית ==========
        public virtual async Task<PublishedPricesDownloadResult> DownloadChain(PublishedPricesChain config, string date)
        {
            var startTime = DateTime.Now;
            var result = new PublishedPricesDownloadResult
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
                Console.WriteLine($"🎯 מטרה: קבצים עדכניים ביותר (StoreFull, PriceFull, PromoFull)");
                Console.WriteLine($"🛡️ מערכת הגנה: אנטי-בוט מתקדם פעיל");

                // יצירת תיקיית רשת
                var chainDir = Path.Combine(BaseDownloadPath, ChainId);
                Directory.CreateDirectory(chainDir);

                // שלב 1: התחברות
                Console.WriteLine($"      🔐 מתחבר לאתר...");
                await AdvancedAntiDetectionDelay("לפני התחברות");

                var loginSuccess = await LoginAsync(config);
                if (loginSuccess=="")
                {
                    result.ErrorMessage = "כישלון בהתחברות לאתר";
                    Console.WriteLine($"      ❌ כישלון בהתחברות");
                    result.Duration = (DateTime.Now - startTime).TotalSeconds;
                    return result;
                }

                Console.WriteLine($"      ✅ התחברות הצליחה");

                // שלב 2: קבלת רשימת קבצים
                Console.WriteLine($"      📋 מקבל רשימת קבצים...");
                await AdvancedAntiDetectionDelay("לפני קבלת רשימת קבצים");

                var fileList = await GetFileListAsync(config,loginSuccess);
                if (!fileList.Any())
                {
                    result.ErrorMessage = "לא נמצאו קבצים זמינים";
                    Console.WriteLine($"      ❌ לא נמצאו קבצים");
                    result.Duration = (DateTime.Now - startTime).TotalSeconds;
                    return result;
                }

                Console.WriteLine($"      📄 נמצאו {fileList.Count} קבצים");

                // שלב 3: סינון וארגון קבצים
                var today = DateTime.Now.Date;
                var latestFiles = FilterLatestFiles(fileList, today);

                Console.WriteLine($"      🎯 נבחרו {latestFiles.Count} קבצים עדכניים");

                // שלב 4: הורדת קבצים
                foreach (var file in latestFiles)
                {
                    Console.WriteLine($"         📥 מוריד: {file.Name}");


                    var originalFileName = SanitizeFileName(file.Name);

                    // תמיד עובדים עם XML
                    var xmlFileName = Path.ChangeExtension(originalFileName, ".xml");

                    // תיקיית סוג הקובץ
                    var folderPath = Path.Combine(
                        chainDir,
                        GetFileTypeFolder(xmlFileName, file.Type)
                    );
                    Directory.CreateDirectory(folderPath);

                    // נתיב סופי
                    var localPath = Path.Combine(folderPath, xmlFileName);

                    // זיהוי סניף
                    var branch = ExtractBranchFromFileName(file.Name);

                    // חיפוש קובץ קיים לאותו סניף
                    var existingFilePath = FindExistingFileForBranch(folderPath,branch, file.Type);
                    Console.WriteLine(existingFilePath!=null);
                    // אם קיים קובץ – בדיקת תאריך
                    if (existingFilePath != null)
                    {
                        var existingFileName = Path.GetFileName(existingFilePath);
                        var existingDate = ExtractDateFromFileName(existingFileName);
                        var existingDateNew = ExtractDateFromFileName(file.Name);
                        Console.WriteLine(existingDate + " "+existingDateNew);
                        if (existingDate >= existingDateNew)
                        {
                            Console.WriteLine("         ⏭️ קובץ קיים עדכני יותר – מדלג");
                            continue;
                        }

                        Console.WriteLine("         🔄 נמצא קובץ ישן – יוחלף");
                    }

                    // הורדה בפועל
                    var downloadSuccess = await DownloadFileAsync(file, localPath);

                    if (downloadSuccess)
                    {
                        // מחיקת הקובץ הישן – רק אחרי הצלחה
                       

                        result.DownloadedFiles++;
                        result.SampleFiles.Add(xmlFileName);

                        if (file.Type.Contains("Store"))
                            result.StoresFiles++;
                        else if (file.Type.Contains("Price"))
                            result.PriceFiles++;
                        else if (file.Type.Contains("Promo"))
                            result.PromoFiles++;

                        Console.WriteLine("         ✅ הורד בהצלחה");
                        if (existingFilePath != null)
                        {
                            try
                            {
                                File.Delete(existingFilePath);
                                Console.WriteLine("         🗑️ הקובץ הישן נמחק");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"         ⚠️ שגיאה במחיקת קובץ ישן: {ex.Message}");
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("         ❌ כישלון בהורדה");
                    }

                  
                }

                // סיכום
                result.Success = result.DownloadedFiles > 0;
                result.Duration = (DateTime.Now - startTime).TotalSeconds;

                Console.WriteLine($"      📊 סיכום: {result.StoresFiles} Stores + {result.PriceFiles} Prices + {result.PromoFiles} Promos = {result.DownloadedFiles} סה\"כ");
                Console.WriteLine($"      ✅ {ChainName}: הורדה הושלמה בהצלחה");

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
        protected string? FindExistingFileForBranch(string folderPath, string branchCode, string fileType)
        {
            if (!Directory.Exists(folderPath))
                return null;

            var existingFiles = Directory.GetFiles(folderPath, "*.xml");

            var existingFile = existingFiles.FirstOrDefault(f =>
            {
                var name = Path.GetFileName(f);

                // בודק סוג הקובץ
                if (!name.Contains(fileType, StringComparison.OrdinalIgnoreCase))
                    return false;

                // מחלץ את הסניף מתוך שם הקובץ ומשווה
                var fileBranch = ExtractBranchFromFileName(name);
                return fileBranch == branchCode;
            });

            return existingFile;
        }


        protected DateTime ExtractDateFromFileName(string fileName)
        {
            try
            {
                if (string.IsNullOrEmpty(fileName))
                    return DateTime.MinValue;

                string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);

                // ---- 1) פורמט: 12 ספרות רצופות בסוף (yyyyMMddHHmm) ----
                var matchShort = Regex.Match(nameWithoutExt, @"(\d{12})$");
                if (matchShort.Success)
                {
                    if (DateTime.TryParseExact(matchShort.Value, "yyyyMMddHHmm",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dtShort))
                    {
                        return dtShort;
                    }
                }

                // ---- 2) פורמט: yyyyMMdd-HHmmss ----
                var matchLong = Regex.Match(nameWithoutExt, @"(\d{8})-(\d{6})$");
                if (matchLong.Success)
                {
                    string combined = matchLong.Groups[1].Value + matchLong.Groups[2].Value.Substring(0, 4); // yyyyMMddHHmm
                    if (DateTime.TryParseExact(combined, "yyyyMMddHHmm",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dtLong))
                    {
                        return dtLong;
                    }
                }

                // ---- fallback: תאריך בלבד ----
                var matchDateOnly = Regex.Match(nameWithoutExt, @"(\d{8})$");
                if (matchDateOnly.Success)
                {
                    string fallback = matchDateOnly.Value + "0000"; // משלים שעה 00:00
                    if (DateTime.TryParseExact(fallback, "yyyyMMddHHmm",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dtFallback))
                    {
                        return dtFallback;
                    }
                }

                return DateTime.MinValue;
            }
            catch
            {
                return DateTime.MinValue;
            }
        }

        // ========== פונקציות עזר ==========

        protected List<FileEntry> FilterLatestFiles(List<FileEntry> files, DateTime targetDate)
        {
            var result = new List<FileEntry>();
            Console.WriteLine(targetDate);
            // סינון קבצי Stores (העדכני ביותר)
            var storeFiles = files
                .Where(f => f.Type.Contains("Store"))
                  .Where(f =>
                  {
                      if (DateTime.TryParseExact(f.Date, "dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fileDate))
                      {
                          return fileDate.Date == targetDate.Date; // רק התאריך
                      }
                      return false;
                  })
                .OrderByDescending(f => f.Date)
                .Take(1);
            result.AddRange(storeFiles);

            // קבלת רשימת סניפים מהקבצים
            var branches = files
                .Where(f => !f.Type.Contains("Store"))
                .Select(f => ExtractBranchFromFileName(f.Name))
                .Where(b => !string.IsNullOrEmpty(b))
                .Distinct()
                .ToList();

            Console.WriteLine($"      📍 זוהו {branches.Count} סניפים");

            // סינון קבצי Price ו-Promo (העדכני ביותר לכל סניף)
            foreach (var branch in branches)
            {
                // קבצי PriceFull
                var priceFullFiles = files
                    .Where(f => f.Name.Contains("PriceFull") && !f.Name.Contains("NULL") && ExtractBranchFromFileName(f.Name) == branch)
                      .Where(f =>
                      {
                          if (DateTime.TryParseExact(f.Date, "dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fileDate))
                          {
                              return fileDate.Date == targetDate.Date; // רק התאריך
                          }
                          return false;
                      })
                    .OrderByDescending(f => f.Date)
                    .Take(1);
                result.AddRange(priceFullFiles);

                var priceFiles = files 
                    .Where(f => f.Name.Contains("Price") && !f.Name.Contains("PriceFull") &&  !f.Name.Contains("NULL") 
                    && ExtractBranchFromFileName(f.Name) == branch)
                    .Where(f =>
                    {
                        if (DateTime.TryParseExact(f.Date, "dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fileDate))
                        {
                            return fileDate.Date == targetDate.Date; // רק התאריך
                        }
                        return false;
                    })
                    .OrderByDescending(f => f.Date)
                    .Take(1);
                     result.AddRange(priceFiles);

                if(branch=="183")
                    Console.WriteLine("@@@@@@@@@@@@@@@@@@@@@");
                // קבצי PromoFull
                var promoFullFiles = files
                    .Where(f => f.Name.Contains("PromoFull") && !f.Name.Contains("NULL") && ExtractBranchFromFileName(f.Name) == branch)
                      .Where(f =>
                      {
                          if (DateTime.TryParseExact(f.Date, "dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fileDate))
                          {
                              Console.WriteLine("נכנס)()()()()()()()");
                              return fileDate.Date == targetDate.Date; // רק התאריך
                          }
                          return false;
                      })
                  .OrderByDescending(f => f.Date)
                    .Take(1);
                result.AddRange(promoFullFiles);


                var promoFiles = files
                   .Where(f => f.Name.Contains("Promo") && !f.Name.Contains("PromoFull") && !f.Name.Contains("NULL") 
                   && ExtractBranchFromFileName(f.Name) == branch)
                     .Where(f =>
                     {
                         if (DateTime.TryParseExact(f.Date, "dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fileDate))
                         {
                             return fileDate.Date == targetDate.Date; // רק התאריך
                         }
                         return false;
                     })
                   .OrderByDescending(f => f.Date)
                   .Take(1);
                result.AddRange(promoFiles);

            }
            foreach (var file in result)
            {
                Console.WriteLine(file.Name);
            }

            return result.ToList();
        }

        protected string ExtractBranchFromFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return "";

            var name = Path.GetFileNameWithoutExtension(fileName);
            var parts = name.Split('-');

            // Promo מורחב: יש לפחות 5 מקטעים
            // PromoFullXXXX-XXX-BBB-YYYYMMDD-HHMMSS
            if (parts.Length >= 5 )
            {
                return parts[2]; // הסניף
            }

            // פורמט רגיל
            // PriceFullXXXX-BBB-YYYYMMDDHHMM
            if (parts.Length >= 3)
            {
                return parts[1];
            }

            return "";
        }


        protected string GetFileTypeFolder(string fileName,string type)
        {
            var lowerName = fileName.ToLower();
            var lowerCategory = type.ToLower();

            if (lowerName.Contains("storesfull") || lowerCategory.Contains("storesfull")) return "StoresFull";
            if (lowerName.Contains("pricefull") || lowerCategory.Contains("pricefull")) return "PriceFull";
            if (lowerName.Contains("promofull") || lowerCategory.Contains("promofull")) return "PromoFull";
            if (lowerName.Contains("stores") || lowerCategory.Contains("stores")) return "Stores";
            if (lowerName.Contains("price") || lowerCategory.Contains("price")) return "Price";
            if (lowerName.Contains("promo") || lowerCategory.Contains("promo")) return "Promo";
            return "Unknown";
        }

        protected string SanitizeFileName(string fileName)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sanitized = string.Join("_", fileName.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
            return sanitized;
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    // ========== מימושים ספציפיים לכל סוג אתר ==========

    // אתרי Cerberus רגילים (url.publishedprices.co.il) - ללא HtmlAgilityPack
    public class CerberusStandardDownloader : PublishedPricesDownloaderBase
    {
        public override string ChainName { get; protected set; } = "";
        public override string ChainId { get; protected set; } = "";
        public override PublishedPricesType SiteType => PublishedPricesType.CerberusStandard;

        public void Initialize(string chainName, string chainId)
        {
            ChainName = chainName;
            ChainId = chainId;
        }

        public override async Task<string> LoginAsync(PublishedPricesChain config)
        {
            try
            {
                // שלב 1: קבלת דף הלוגין
                Console.WriteLine(config.LoginUrl);
                var loginResponse = await _httpClient.GetAsync(config.LoginUrl);
                if (!loginResponse.IsSuccessStatusCode)
                    return "";

                var loginHtml = await loginResponse.Content.ReadAsStringAsync();

                // שלב 2: חילוץ CSRF token
                var csrfMatch = Regex.Match(loginHtml, "<meta name=\"csrftoken\" content=\"([^\"]+)\"",
                                            RegexOptions.IgnoreCase);
                var csrfToken = csrfMatch.Success ? csrfMatch.Groups[1].Value : "";
                Console.WriteLine(csrfToken);

                if (string.IsNullOrEmpty(csrfToken))
                    return "";


                // שלב 3: הכנת POST
                var formData = new FormUrlEncodedContent(new[]
                {
                        new KeyValuePair<string,string>("username", config.Username),
                  new KeyValuePair<string,string>("password", config.Password),
                  new KeyValuePair<string,string>("csrftoken", csrfToken),
                  config.LoginUrl==" https://publishedprices.co.il/login?r=%2Ffile"?
                  new KeyValuePair<string, string>("r", "/file"):new KeyValuePair<string,string>("r", "")
                
                     //new KeyValuePair<string,string>("r", "")
                 });

                // שלב 4: שליחת POST
                var loginUri = new Uri(config.LoginUrl);
                var baseUri = loginUri.GetLeftPart(UriPartial.Authority);
                var loginPostUrl = baseUri + "/login/user";

                var loginPostResponse = await _httpClient.PostAsync(loginPostUrl, formData);

                if (!loginPostResponse.IsSuccessStatusCode &&
                    loginPostResponse.StatusCode != System.Net.HttpStatusCode.Found)
                    return "";

                var responseContent = await loginPostResponse.Content.ReadAsStringAsync();
                // שלב 5: בדיקה אם ההתחברות הצליחה
                bool loginSuccess = !responseContent.Contains("login-form");
                if (loginSuccess)
                    return responseContent;
                else
                    return "";

            }
            catch
            {
                return "";
            }
        }



        public override async Task<List<FileEntry>> GetFileListAsync(PublishedPricesChain config,string html)
        {
            try
            {
                if (config.FileUrl == "https://publishedprices.co.il/file/d/Yuda/")
                {
                    Console.WriteLine("📂 כניסה לתקיית Yuda לפני שליפת קבצים");

                    var folderResponse = await _httpClient.GetAsync(config.FileUrl);
                    var folderHtml = await folderResponse.Content.ReadAsStringAsync();

                    // אם את משתמשת ב-html בהמשך – תחליפי
                    html = folderHtml;
                    Console.WriteLine(html);
                }
                var csrfMatch = Regex.Match(html, "<meta name=\"csrftoken\" content=\"([^\"]+)\"",
                                         RegexOptions.IgnoreCase);
                var csrfToken = csrfMatch.Success ? csrfMatch.Groups[1].Value : "";
                Console.WriteLine(csrfToken);

                var url = config.FileUrl == "https://publishedprices.co.il/file/d/Yuda/"?
                    "https://publishedprices.co.il/file/json/dir" :
                    "https://url.publishedprices.co.il/file/json/dir";
                var today = DateTime.Now.ToString("yyyyMMdd");
                Console.WriteLine(today);
                var content = new FormUrlEncodedContent(new[]
{
    new KeyValuePair<string,string>("sEcho","1"),
    new KeyValuePair<string,string>("iColumns","5"),
    new KeyValuePair<string,string>("iDisplayStart","0"),
    new KeyValuePair<string,string>("iDisplayLength","10000"),

    new KeyValuePair<string,string>("mDataProp_0","fname"),
    new KeyValuePair<string,string>("mDataProp_1","typeLabel"),
    new KeyValuePair<string,string>("mDataProp_2","size"),
    new KeyValuePair<string,string>("mDataProp_3","ftime"),
    new KeyValuePair<string,string>("mDataProp_4",""),

    new KeyValuePair<string,string>("sSearch",today),
    new KeyValuePair<string,string>("bRegex","false"),
    new KeyValuePair<string,string>("iSortingCols","0"),
    config.FileUrl=="https://publishedprices.co.il/file/d/Yuda/"? new KeyValuePair<string,string>("cd","/Yuda"):
     new KeyValuePair<string,string>("cd","/"),
    new KeyValuePair<string,string>("csrftoken", csrfToken),

   
});

                var response = await _httpClient.PostAsync(url, content);
                Console.WriteLine(response.IsSuccessStatusCode);
                var json = await response.Content.ReadAsStringAsync();
                //return ParseCerberusFileListWithRegex(html, config.FileUrl);
                return ParseCerberusFileListFromJson(json,config);

            }
            catch (Exception ex)
            {
                Console.WriteLine($"      ❌ שגיאה בקבלת רשימת קבצים: {ex.Message}");
                return new List<FileEntry>();
            }
        }

        public override async Task<bool> DownloadFileAsync(FileEntry file, string localPath)
        {
            try
            {
                Console.WriteLine(file.DownloadUrl);
                var response = await _httpClient.GetAsync(file.DownloadUrl);
                if (!response.IsSuccessStatusCode)
                    return false;

                var fileBytes = await response.Content.ReadAsByteArrayAsync();

                // תמיד נשמור כ-XML
                localPath = Path.ChangeExtension(localPath, ".xml");

                if (IsZipFile(fileBytes))
                {
                    // ZIP → חילוץ XML
                    return await ExtractZipToXml(fileBytes, localPath);
                }
                else if (IsGzipFile(fileBytes))
                {
                    // GZ → חילוץ XML
                    using var ms = new MemoryStream(fileBytes);
                    using var gz = new System.IO.Compression.GZipStream(ms, System.IO.Compression.CompressionMode.Decompress);
                    using var outFile = new FileStream(localPath, FileMode.Create, FileAccess.Write);
                    await gz.CopyToAsync(outFile);
                    return true;
                }
                else
                {
                    // XML רגיל → שמירה ישירה
                    await File.WriteAllBytesAsync(localPath, fileBytes);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"      ❌ שגיאה בהורדת קובץ {file.Name}: {ex.Message}");
                return false;
            }
        }



        // ========== פונקציות עזר ספציפיות - ללא HtmlAgilityPack ==========


        private List<FileEntry> ParseCerberusFileListFromJson(string json, PublishedPricesChain config)
        {
            var files = new List<FileEntry>();

            try
            {
                Console.WriteLine("⏳ מתחיל פענוח JSON");
                var root = JsonSerializer.Deserialize<JsonRoot>(json);
                if (root?.aaData == null || root.aaData.Count == 0)
                {
                    Console.WriteLine("⚠️ אין נתונים ב-aaData");
                    return files;
                }

                foreach (var item in root.aaData)
                {
                    var fileName = item.fname ?? item.name;
                    if (string.IsNullOrEmpty(fileName)) continue;

                    // ניסיון לחלץ תאריך מה-ftime
                    DateTime parsedDate = DateTime.MinValue;
                    if (!string.IsNullOrEmpty(item.ftime))
                    {
                        DateTime.TryParseExact(
                            item.ftime,
                            "dd/MM/yyyy HH:mm",
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.None,
                            out parsedDate
                        );
                    }

                    files.Add(new FileEntry
                    {
                        Name = fileName,
                        Type = DetermineFileType(fileName),
                        Size = item.size.ToString(),
                        Date = item.ftime,
                        ParsedDate = parsedDate,
                        DownloadUrl= config.FileUrl == "https://publishedprices.co.il/file/d/Yuda/"?
                        "https://publishedprices.co.il/file/d/Yuda/" + item.value : 
                        "https://url.publishedprices.co.il/file/d/" +item.value

                    });
                }

                Console.WriteLine($"✅ סיימתי פענוח JSON - נמצאו {files.Count} קבצים");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ שגיאה בפענוח JSON: {ex.Message}");
            }

            return files;
        }


        private string DetermineFileType(string fileName)
        {
            if (fileName.Contains("StoresFull")) return "StoresFull";
            if (fileName.Contains("PriceFull")) return "PriceFull";
            if (fileName.Contains("PromoFull")) return "PromoFull";
            if (fileName.Contains("Stores")) return "Stores";
            if (fileName.Contains("Price")) return "Price";
            if (fileName.Contains("Promo")) return "Promo";
            return "Unknown";
        }

        private bool IsZipFile(byte[] fileBytes)
        {
            return fileBytes.Length >= 4 &&
                   fileBytes[0] == 0x50 && fileBytes[1] == 0x4B &&
                   (fileBytes[2] == 0x03 || fileBytes[2] == 0x05) &&
                   (fileBytes[3] == 0x04 || fileBytes[3] == 0x06);
        }
        private bool IsGzipFile(byte[] bytes)
        {
            // קובץ GZ מתחיל תמיד ב־1F 8B
            return bytes.Length > 2 && bytes[0] == 0x1F && bytes[1] == 0x8B;
        }


        private async Task<bool> ExtractZipToXml(byte[] zipBytes, string outputPath)
        {
            try
            {
                using var zipStream = new MemoryStream(zipBytes);
                using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

                foreach (var entry in archive.Entries)
                {
                    if (entry.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                    {
                        var xmlPath = Path.ChangeExtension(outputPath, ".xml");
                        using var entryStream = entry.Open();
                        using var fileStream = File.Create(xmlPath);
                        await entryStream.CopyToAsync(fileStream);
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"      ❌ שגיאה בחילוץ ZIP: {ex.Message}");
                return false;
            }
        }
    }

    // ========== Factory לניהול כל הרשתות ==========

    public class PublishedPricesDownloaderFactory
    {
        private readonly Dictionary<PublishedPricesType, Func<PublishedPricesDownloaderBase>> _downloaderFactories;

        public PublishedPricesDownloaderFactory()
        {
            _downloaderFactories = new Dictionary<PublishedPricesType, Func<PublishedPricesDownloaderBase>>
            {
                { PublishedPricesType.CerberusStandard, () => new CerberusStandardDownloader() },
                // ניתן להוסיף מימושים נוספים כאן
            };

            Console.WriteLine($"🏭 PublishedPrices Factory הוקם");
            Console.WriteLine($"🛡️ מערכת הגנה: אנטי-בוט מתקדם עם retry ו-backoff");
        }

        public PublishedPricesDownloaderBase? GetDownloader(PublishedPricesType type, string chainName, string chainId)
        {
            if (_downloaderFactories.TryGetValue(type, out var factory))
            {
                var downloader = factory();

                // אתחול פרמטרים ספציפיים
                if (downloader is CerberusStandardDownloader cerberusDownloader)
                {
                    cerberusDownloader.Initialize(chainName, chainId);
                }

                Console.WriteLine($"✅ נמצא Downloader עבור '{chainName}': {type}");
                return downloader;
            }
            else
            {
                Console.WriteLine($"❌ לא נמצא Downloader עבור סוג: {type}");
                return null;
            }
        }

        public List<PublishedPricesType> GetSupportedTypes()
        {
            return _downloaderFactories.Keys.ToList();
        }
       

    }


}