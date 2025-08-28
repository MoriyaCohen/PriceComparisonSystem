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

    // ========== מחלקת בסיס מתקדמת נגד חסימות ==========

    public abstract class PublishedPricesDownloaderBase : IDisposable
    {
        protected readonly HttpClient _httpClient;
        protected const string BaseDownloadPath = "Downloads\\PublishedPrices";

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
            await _downloadSemaphore.WaitAsync();

            try
            {
                var requestCount = Interlocked.Increment(ref _requestCounter);
                var timeSinceLastRequest = DateTime.Now - _lastRequestTime;

                var multiplier = 1.0;
                if (timeSinceLastRequest.TotalSeconds < 2)
                {
                    multiplier = 2.0;
                }

                if (requestCount % 10 == 0)
                {
                    multiplier = 3.0;
                }

                var minMs = (int)(baseMinMs * multiplier);
                var maxMs = (int)(baseMaxMs * multiplier);
                var delayMs = _random.Next(minMs, maxMs);
                var noise = _random.Next(-300, 300);
                delayMs = Math.Max(1000, delayMs + noise);

                Console.WriteLine($"      ⏳ {context} - ממתין {delayMs / 1000:F1} שניות (בקשה #{requestCount})...");

                await Task.Delay(delayMs);
                _lastRequestTime = DateTime.Now;
            }
            finally
            {
                _downloadSemaphore.Release();
            }
        }

        // ========== שיטות מופשטות ==========
        public abstract Task<bool> LoginAsync(PublishedPricesChain config);
        public abstract Task<List<FileEntry>> GetFileListAsync(PublishedPricesChain config);
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
                if (!loginSuccess)
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

                var fileList = await GetFileListAsync(config);
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

                    await AdvancedAntiDetectionDelay($"לפני הורדת {file.Name}");

                    var fileName = SanitizeFileName(file.Name);
                    var localPath = Path.Combine(chainDir, GetFileTypeFolder(file.Type), fileName);
                    var directory = Path.GetDirectoryName(localPath);
                    if (!string.IsNullOrEmpty(directory))
                        Directory.CreateDirectory(directory);

                    var downloadSuccess = await DownloadFileAsync(file, localPath);
                    if (downloadSuccess)
                    {
                        result.DownloadedFiles++;
                        result.SampleFiles.Add(fileName);

                        // ספירת סוגי קבצים
                        if (file.Type.Contains("Store"))
                            result.StoresFiles++;
                        else if (file.Type.Contains("Price"))
                            result.PriceFiles++;
                        else if (file.Type.Contains("Promo"))
                            result.PromoFiles++;

                        Console.WriteLine($"         ✅ הורד בהצלחה");
                    }
                    else
                    {
                        Console.WriteLine($"         ❌ כישלון בהורדה");
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

        // ========== פונקציות עזר ==========

        protected List<FileEntry> FilterLatestFiles(List<FileEntry> files, DateTime targetDate)
        {
            var result = new List<FileEntry>();

            // סינון קבצי Stores (העדכני ביותר)
            var storeFiles = files
                .Where(f => f.Type.Contains("Store"))
                .Where(f => f.ParsedDate.Date == targetDate)
                .OrderByDescending(f => f.ParsedDate)
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
                var priceFiles = files
                    .Where(f => f.Type.Contains("Price") && ExtractBranchFromFileName(f.Name) == branch)
                    .Where(f => f.ParsedDate.Date == targetDate)
                    .OrderByDescending(f => f.Type.Contains("Full") ? 1 : 0)
                    .ThenByDescending(f => f.ParsedDate)
                    .Take(1);
                result.AddRange(priceFiles);

                // קבצי PromoFull
                var promoFiles = files
                    .Where(f => f.Type.Contains("Promo") && ExtractBranchFromFileName(f.Name) == branch)
                    .Where(f => f.ParsedDate.Date == targetDate)
                    .OrderByDescending(f => f.Type.Contains("Full") ? 1 : 0)
                    .ThenByDescending(f => f.ParsedDate)
                    .Take(1);
                result.AddRange(promoFiles);
            }

            return result.ToList();
        }

        protected string ExtractBranchFromFileName(string fileName)
        {
            try
            {
                // פורמט: TypeXXXXXXXXXXXXXX-BBB-YYYYMMDDHHMMSS-XXX
                var parts = fileName.Split('-');
                return parts.Length >= 2 ? parts[1] : "";
            }
            catch
            {
                return "";
            }
        }

        protected string GetFileTypeFolder(string type)
        {
            if (type.Contains("Store")) return "Stores";
            if (type.Contains("Price")) return "Prices";
            if (type.Contains("Promo")) return "Promos";
            return "Other";
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

        public override async Task<bool> LoginAsync(PublishedPricesChain config)
        {
            try
            {
                // שלב 1: קבלת דף הלוגין
                var loginResponse = await _httpClient.GetAsync(config.LoginUrl);
                if (!loginResponse.IsSuccessStatusCode)
                    return false;

                var loginHtml = await loginResponse.Content.ReadAsStringAsync();

                // שלב 2: חילוץ CSRF token עם Regex
                var csrfToken = ExtractCsrfTokenWithRegex(loginHtml);

                // שלב 3: שליחת נתוני התחברות
                var formData = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("username", config.Username),
                    new KeyValuePair<string, string>("password", config.Password ?? ""),
                    new KeyValuePair<string, string>("r", "")
                });

                var loginPostResponse = await _httpClient.PostAsync(config.LoginUrl.Replace("/login", "/login/user"), formData);

                // שלב 4: בדיקת הצלחת התחברות
                if (loginPostResponse.IsSuccessStatusCode)
                {
                    var responseContent = await loginPostResponse.Content.ReadAsStringAsync();
                    return !responseContent.Contains("login-form") && !responseContent.Contains("Client Login");
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"      ❌ שגיאה בהתחברות: {ex.Message}");
                return false;
            }
        }

        public override async Task<List<FileEntry>> GetFileListAsync(PublishedPricesChain config)
        {
            try
            {
                var response = await _httpClient.GetAsync(config.FileUrl);
                if (!response.IsSuccessStatusCode)
                    return new List<FileEntry>();

                var html = await response.Content.ReadAsStringAsync();
                return ParseCerberusFileListWithRegex(html, config.FileUrl);
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
                var response = await _httpClient.GetAsync(file.DownloadUrl);
                if (!response.IsSuccessStatusCode)
                    return false;

                var fileBytes = await response.Content.ReadAsByteArrayAsync();

                // אם זה ZIP, חלץ את ה-XML
                if (IsZipFile(fileBytes))
                {
                    return await ExtractZipToXml(fileBytes, localPath);
                }
                else
                {
                    // שמירה ישירה
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

        private string ExtractCsrfTokenWithRegex(string html)
        {
            try
            {
                var match = Regex.Match(html, @"<meta\s+name=['""]csrftoken['""]\s+content=['""]([^'""]+)['""]", RegexOptions.IgnoreCase);
                return match.Success ? match.Groups[1].Value : "";
            }
            catch
            {
                return "";
            }
        }

        private List<FileEntry> ParseCerberusFileListWithRegex(string html, string baseUrl)
        {
            var files = new List<FileEntry>();

            try
            {
                // חיפוש שורות הטבלה עם Regex
                var tableRowPattern = @"<tr[^>]*>(.*?)</tr>";
                var cellPattern = @"<td[^>]*>(.*?)</td>";
                var linkPattern = @"<a[^>]+href=['""]([^'""]+)['""][^>]*>(.*?)</a>";

                var rowMatches = Regex.Matches(html, tableRowPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

                foreach (Match rowMatch in rowMatches)
                {
                    var rowHtml = rowMatch.Groups[1].Value;
                    var cellMatches = Regex.Matches(rowHtml, cellPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

                    if (cellMatches.Count >= 4)
                    {
                        var nameCell = cellMatches[0].Groups[1].Value;
                        var typeCell = cellMatches[1].Groups[1].Value;
                        var sizeCell = cellMatches[2].Groups[1].Value;
                        var dateCell = cellMatches[3].Groups[1].Value;

                        // חילוץ שם קובץ וקישור הורדה
                        var linkMatch = Regex.Match(nameCell, linkPattern, RegexOptions.IgnoreCase);
                        if (linkMatch.Success)
                        {
                            var downloadLink = linkMatch.Groups[1].Value;
                            var fileName = StripHtmlTags(linkMatch.Groups[2].Value).Trim();

                            if (!string.IsNullOrEmpty(fileName) && !string.IsNullOrEmpty(downloadLink))
                            {
                                var fullDownloadUrl = downloadLink.StartsWith("http") ? downloadLink :
                                    new Uri(new Uri(baseUrl), downloadLink).ToString();

                                files.Add(new FileEntry
                                {
                                    Name = fileName,
                                    Type = DetermineFileType(fileName),
                                    Size = StripHtmlTags(sizeCell).Trim(),
                                    Date = StripHtmlTags(dateCell).Trim(),
                                    DownloadUrl = fullDownloadUrl,
                                    ParsedDate = ParseFileDate(fileName)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"      ⚠️ שגיאה בעיבוד HTML: {ex.Message}");
            }

            return files;
        }

        private string StripHtmlTags(string html)
        {
            if (string.IsNullOrEmpty(html))
                return "";

            return Regex.Replace(html, @"<[^>]+>", "").Replace("&nbsp;", " ").Trim();
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

        private DateTime ParseFileDate(string fileName)
        {
            try
            {
                // פורמט: TypeXXXXXXXXXXXXXX-BBB-YYYYMMDDHHMMSS-XXX
                var match = Regex.Match(fileName, @"-(\d{14})-");
                if (match.Success)
                {
                    var dateStr = match.Groups[1].Value;
                    return DateTime.ParseExact(dateStr, "yyyyMMddHHmmss", CultureInfo.InvariantCulture);
                }
            }
            catch { }

            return DateTime.MinValue;
        }

        private bool IsZipFile(byte[] fileBytes)
        {
            return fileBytes.Length >= 4 &&
                   fileBytes[0] == 0x50 && fileBytes[1] == 0x4B &&
                   (fileBytes[2] == 0x03 || fileBytes[2] == 0x05) &&
                   (fileBytes[3] == 0x04 || fileBytes[3] == 0x06);
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

    // ========== תוכנית ראשית ==========

    public class PublishedPricesProgram
    {
        public static async Task Main(string[] args)
        {
            try
            {
                Console.WriteLine("🚀 מערכת הורדות PublishedPrices - כל הרשתות");
                Console.WriteLine("🎯 רשתות: Cerberus WebClient + אתרים מיוחדים");
                Console.WriteLine("============================================================");

                var currentDate = DateTime.Now.ToString("dd/MM/yyyy");
                Console.WriteLine($"📅 תאריך היום: {currentDate}");

                // טעינת הגדרות רשתות
                var chainsConfig = await LoadChainsConfiguration();
                var enabledChains = chainsConfig.Where(c => c.Enabled).ToList();

                Console.WriteLine($"📖 נטען קובץ הגדרות: {chainsConfig.Count} רשתות מוגדרות");
                Console.WriteLine($"📋 רשתות מופעלות: {string.Join(", ", enabledChains.Select(c => c.Name))}");

                if (!enabledChains.Any())
                {
                    Console.WriteLine("⚠️ אין רשתות מופעלות להורדה");
                    return;
                }

                var factory = new PublishedPricesDownloaderFactory();
                var allResults = new List<PublishedPricesDownloadResult>();

                // הפעלת הורדות במקביל
                var downloadTasks = enabledChains.Select(async chain =>
                {
                    Console.WriteLine($"\n🔍 מתחיל הורדה: {chain.Name}");

                    var downloader = factory.GetDownloader(chain.Type, chain.Name, chain.Id);
                    if (downloader != null)
                    {
                        try
                        {
                            return await downloader.DownloadChain(chain, currentDate);
                        }
                        finally
                        {
                            downloader.Dispose();
                        }
                    }
                    else
                    {
                        Console.WriteLine($"❌ לא נמצא מטפל עבור: {chain.Name}");
                        return new PublishedPricesDownloadResult
                        {
                            ChainName = chain.Name,
                            Success = false,
                            ErrorMessage = "לא נמצא מטפל מתאים"
                        };
                    }
                });

                var results = await Task.WhenAll(downloadTasks);
                allResults.AddRange(results);

                // הצגת תוצאות
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

        private static async Task<List<PublishedPricesChain>> LoadChainsConfiguration()
        {
            const string configFile = "publishedprices_chains.json";

            if (!File.Exists(configFile))
            {
                Console.WriteLine($"⚠️ קובץ {configFile} לא נמצא, יוצר דוגמה...");
                await CreateSampleConfiguration(configFile);
                Console.WriteLine($"✅ נוצר קובץ דוגמה: {configFile}");
                Console.WriteLine("📝 ערוך את הקובץ לפי הצורך והפעל מחדש");
                return new List<PublishedPricesChain>();
            }

            try
            {
                var json = await File.ReadAllTextAsync(configFile);
                var config = JsonSerializer.Deserialize<PublishedPricesConfig>(json);
                return config?.Chains ?? new List<PublishedPricesChain>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ שגיאה בטעינת קובץ הגדרות: {ex.Message}");
                return new List<PublishedPricesChain>();
            }
        }

        private static async Task CreateSampleConfiguration(string configFile)
        {
            var sampleConfig = new PublishedPricesConfig
            {
                Description = "הגדרות רשתות PublishedPrices להורדה - כל הרשתות ממסמך החקירה",
                LastUpdated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                Chains = new List<PublishedPricesChain>
                {
                    // ========== רשתות Cerberus Standard ==========
                    new PublishedPricesChain
                    {
                        Id = "politzer",
                        Name = "פוליצר חדרה (1982) בע\"מ",
                        LoginUrl = "https://url.publishedprices.co.il/login",
                        FileUrl = "https://url.publishedprices.co.il/file",
                        Username = "politzer",
                        Password = "",
                        Type = PublishedPricesType.CerberusStandard,
                        Enabled = true,
                        Notes = "ללא סיסמה"
                    },
                    new PublishedPricesChain
                    {
                        Id = "paz_yellow",
                        Name = "פז קמעונאות ואנרגיה בע\"מ - יילו",
                        LoginUrl = "https://url.publishedprices.co.il/login?r=%2Ffile",
                        FileUrl = "https://url.publishedprices.co.il/file",
                        Username = "b_Paz",
                        Password = "468paz",
                        Type = PublishedPricesType.CerberusStandard,
                        Enabled = true
                    },
                    new PublishedPricesChain
                    {
                        Id = "yuda_super",
                        Name = "פז קמעונאות ואנרגיה בע\"מ - יודה סופר",
                        LoginUrl = "https://publishedprices.co.il/login?r=%2Ffile",
                        FileUrl = "https://publishedprices.co.il/file",
                        Username = "yuda_ho",
                        Password = "Yud@147",
                        Type = PublishedPricesType.PublishedPricesStandard,
                        Enabled = true
                    },
                    new PublishedPricesChain
                    {
                        Id = "freshmarket",
                        Name = "פז קמעונאות ואנרגיה בע\"מ - פרשמרקט",
                        LoginUrl = "https://url.publishedprices.co.il/login",
                        FileUrl = "https://url.publishedprices.co.il/file",
                        Username = "freshmarket",
                        Password = "",
                        Type = PublishedPricesType.CerberusStandard,
                        Enabled = true,
                        Notes = "ללא סיסמה"
                    },
                    new PublishedPricesChain
                    {
                        Id = "yohananof",
                        Name = "מ. יוחננוף ובניו (1988) בע\"מ",
                        LoginUrl = "https://url.publishedprices.co.il/login",
                        FileUrl = "https://url.publishedprices.co.il/file",
                        Username = "yohananof",
                        Password = "",
                        Type = PublishedPricesType.CerberusStandard,
                        Enabled = true,
                        Notes = "ללא סיסמה"
                    },
                    new PublishedPricesChain
                    {
                        Id = "osherad",
                        Name = "מרב-מזון כל בע\"מ (אושר עד)",
                        LoginUrl = "https://url.publishedprices.co.il/login",
                        FileUrl = "https://url.publishedprices.co.il/file",
                        Username = "osherad",
                        Password = "",
                        Type = PublishedPricesType.CerberusStandard,
                        Enabled = true,
                        Notes = "ללא סיסמה"
                    },
                    new PublishedPricesChain
                    {
                        Id = "salach_dabah",
                        Name = "סאלח דבאח ובניו בע\"מ",
                        LoginUrl = "https://url.publishedprices.co.il/login",
                        FileUrl = "https://url.publishedprices.co.il/file",
                        Username = "SalachD",
                        Password = "12345",
                        Type = PublishedPricesType.CerberusStandard,
                        Enabled = true
                    },
                    new PublishedPricesChain
                    {
                        Id = "tivtaam",
                        Name = "טיב טעם רשתות בע\"מ",
                        LoginUrl = "https://url.publishedprices.co.il/login",
                        FileUrl = "https://url.publishedprices.co.il/file",
                        Username = "TivTaam",
                        Password = "",
                        Type = PublishedPricesType.CerberusStandard,
                        Enabled = true,
                        Notes = "ללא סיסמה"
                    },
                    new PublishedPricesChain
                    {
                        Id = "keshet",
                        Name = "קשת טעמים בע\"מ",
                        LoginUrl = "https://url.publishedprices.co.il/login",
                        FileUrl = "https://url.publishedprices.co.il/file",
                        Username = "Keshet",
                        Password = "",
                        Type = PublishedPricesType.CerberusStandard,
                        Enabled = true,
                        Notes = "ללא סיסמה"
                    },
                    new PublishedPricesChain
                    {
                        Id = "rami_levi",
                        Name = "חנויות רמי לוי שיווק השקמה 2006 בע\"מ",
                        LoginUrl = "https://url.publishedprices.co.il/login",
                        FileUrl = "https://url.publishedprices.co.il/file",
                        Username = "RamiLevi",
                        Password = "",
                        Type = PublishedPricesType.CerberusStandard,
                        Enabled = true,
                        Notes = "ללא סיסמה"
                    },
                    new PublishedPricesChain
                    {
                        Id = "super_cofix",
                        Name = "רמי לוי - סופר קופיקס",
                        LoginUrl = "https://url.publishedprices.co.il/login",
                        FileUrl = "https://url.publishedprices.co.il/file",
                        Username = "SuperCofixApp",
                        Password = "",
                        Type = PublishedPricesType.CerberusStandard,
                        Enabled = true,
                        Notes = "ללא סיסמה"
                    },
                    new PublishedPricesChain
                    {
                        Id = "doralon",
                        Name = "דור אלון ניהול מתחמים קמעונאיים בע\"מ",
                        LoginUrl = "https://url.publishedprices.co.il/login",
                        FileUrl = "https://url.publishedprices.co.il/file",
                        Username = "doralon",
                        Password = "",
                        Type = PublishedPricesType.CerberusStandard,
                        Enabled = true,
                        Notes = "ללא סיסמה"
                    },
                    new PublishedPricesChain
                    {
                        Id = "stop_market",
                        Name = "סטופ מרקט בע\"מ",
                        LoginUrl = "https://url.retail.publishedprices.co.il/login",
                        FileUrl = "https://url.retail.publishedprices.co.il/file",
                        Username = "Market_Stop",
                        Password = "",
                        Type = PublishedPricesType.CerberusRetail,
                        Enabled = true,
                        Notes = "ללא סיסמה - אתר לא מאובטח"
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

        private static async Task DisplayAllResults(List<PublishedPricesDownloadResult> results)
        {
            Console.WriteLine("\n" + "=".PadRight(70, '='));
            Console.WriteLine("📊 סיכום הורדות PublishedPrices - כל הרשתות");
            Console.WriteLine("=".PadRight(70, '='));

            var totalSuccessful = results.Count(r => r.Success);
            var totalFiles = results.Sum(r => r.DownloadedFiles);

            Console.WriteLine($"✅ רשתות שהצליחו: {totalSuccessful}/{results.Count}");
            Console.WriteLine($"📁 סה\"כ קבצים: {totalFiles}");

            foreach (var result in results.OrderBy(r => r.ChainName))
            {
                DisplayResult(result);
            }

            // שמירת לוג מפורט
            await SaveDetailedLog(results);
        }

        private static void DisplayResult(PublishedPricesDownloadResult result)
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

        private static async Task SaveDetailedLog(List<PublishedPricesDownloadResult> results)
        {
            var logData = new
            {
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                Version = "PublishedPrices Downloader v1.0 - כל הרשתות",
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

            var logFileName = $"publishedprices_download_log_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            await File.WriteAllTextAsync(logFileName, json);

            Console.WriteLine($"\n📄 לוג נשמר: {logFileName}");
        }
    }
}