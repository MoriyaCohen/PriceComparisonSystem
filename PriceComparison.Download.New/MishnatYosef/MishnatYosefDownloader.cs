using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using PriceComparison.Download.New.BinaProject;

namespace PriceComparison.Download.New.MishnatYosef
{
    /// <summary>
    /// מודל לקובץ ברשת משנת יוסף - API של Cloudflare Workers
    /// </summary>
    public class MishnatYosefFileInfo
    {
        public string name { get; set; } = "";
        public string type { get; set; } = "";
        public string date { get; set; } = "";
        public string url { get; set; } = "";
    }

    /// <summary>
    /// מוריד קבצים מרשת משנת יוסף - API שונה מבינה פרוגקט
    /// </summary>
    public class MishnatYosefDownloader : IChainDownloader
    {
        private readonly HttpClient _httpClient;
        private readonly Random _random = new();
        private const string BaseDownloadPath = "Downloads";

        // הגדרות API ייחודיות לרשת משנת יוסף
        private const string API_BASE_URL = "https://list-files.w5871031-kt.workers.dev/";
        private const string WEB_BASE_URL = "https://chp-kt.pages.dev/";

        public string ChainName => "משנת יוסף (קיי.טי.)";
        public string ChainId => "MishnatYosef";

        public MishnatYosefDownloader()
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

            // User-Agent דמוי דפדפן
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

            // Headers נוספים
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json,text/html,application/xhtml+xml,*/*");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "he-IL,he;q=0.9,en-US;q=0.8,en;q=0.7");
            _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
            _httpClient.DefaultRequestHeaders.Add("DNT", "1");
            _httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
            _httpClient.DefaultRequestHeaders.Add("Referer", WEB_BASE_URL);

            _httpClient.Timeout = TimeSpan.FromMinutes(10);
        }

        public bool CanHandle(string chainId)
        {
            return chainId.Equals("mishnatyosef", StringComparison.OrdinalIgnoreCase) ||
                   chainId.Equals("mishnat-yosef", StringComparison.OrdinalIgnoreCase) ||
                   chainId.Equals("mishnat_yosef", StringComparison.OrdinalIgnoreCase) ||
                   chainId.Equals("kt-mishnat", StringComparison.OrdinalIgnoreCase) ||
                   chainId.Equals("משנת-יוסף", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// קבלת רשימת קבצים זמינים מה-API של משנת יוסף
        /// </summary>
        public async Task<List<FileMetadata>> GetAvailableFiles(string date)
        {
            try
            {
                Console.WriteLine($"      🌐 מתחבר ל-API של משנת יוסף: {API_BASE_URL}");

                // עיכוב קל למניעת זיהוי בוט
                await Task.Delay(_random.Next(1000, 3000));

                var response = await _httpClient.GetAsync(API_BASE_URL);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"      ❌ שגיאת HTTP: {response.StatusCode}");
                    return new List<FileMetadata>();
                }

                var jsonContent = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrWhiteSpace(jsonContent))
                {
                    Console.WriteLine($"      ⚠️ תגובה ריקה מהשרת");
                    return new List<FileMetadata>();
                }

                // פרסור JSON של משנת יוסף
                var mishnatFiles = JsonSerializer.Deserialize<List<MishnatYosefFileInfo>>(jsonContent) ?? new List<MishnatYosefFileInfo>();

                Console.WriteLine($"      ✅ נמצאו {mishnatFiles.Count} קבצים זמינים");

                // המרה לפורמט המוכר של המערכת
                var convertedFiles = ConvertToStandardFormat(mishnatFiles, date);

                Console.WriteLine($"      🔍 לאחר סינון לתאריך {date}: {convertedFiles.Count} קבצים רלוונטיים");

                return convertedFiles;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"      💥 שגיאה בקבלת קבצים: {ex.Message}");
                return new List<FileMetadata>();
            }
        }

        /// <summary>
        /// המרת קבצים מפורמט משנת יוסף לפורמט סטנדרטי
        /// </summary>
        private List<FileMetadata> ConvertToStandardFormat(List<MishnatYosefFileInfo> mishnatFiles, string targetDate)
        {
            var result = new List<FileMetadata>();

            foreach (var file in mishnatFiles)
            {
                try
                {
                    // בדיקת תאריך - האם הקובץ רלוונטי ליום המבוקש
                    if (!IsDateRelevant(file.date, targetDate))
                        continue;

                    var convertedFile = new FileMetadata
                    {
                        FileNm = file.name,
                        DateFile = file.date,
                        WStore = ExtractStoreFromFileName(file.name),
                        WFileType = DetermineFileType(file.name),
                        Company = "משנת יוסף",
                        LastUpdateDate = file.date,
                        LastUpdateTime = "",
                        FileType = file.type
                    };

                    result.Add(convertedFile);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"      ⚠️ שגיאה בהמרת קובץ {file.name}: {ex.Message}");
                }
            }

            return result.OrderByDescending(f => ExtractTimeFromFileName(f.FileNm)).ToList();
        }

        /// <summary>
        /// בדיקה האם תאריך הקובץ רלוונטי ליום המבוקש
        /// </summary>
        private bool IsDateRelevant(string fileDate, string targetDate)
        {
            try
            {
                // ניסיון להמיר את התאריכים לפורמט אחיד
                if (DateTime.TryParse(targetDate, out var targetDateTime))
                {
                    var fileDateString = fileDate.Replace("-", "/");

                    if (DateTime.TryParse(fileDateString, out var fileDateTime))
                    {
                        return fileDateTime.Date == targetDateTime.Date;
                    }

                    // אם לא הצלחנו להמיר, נבדוק אם התאריך מופיע בשם הקובץ
                    return fileDate.Contains(targetDateTime.ToString("yyyy-MM-dd")) ||
                           fileDate.Contains(targetDateTime.ToString("dd/MM/yyyy")) ||
                           fileDate.Contains(targetDateTime.ToString("yyyy/MM/dd"));
                }

                return true; // במקרה של ספק, נכלול את הקובץ
            }
            catch
            {
                return true; // במקרה של ספק, נכלול את הקובץ
            }
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
                Console.WriteLine($"🎯 מטרה: קבצים עדכניים מ-API של Cloudflare Workers");

                // יצירת תיקיית רשת
                var chainDir = Path.Combine(BaseDownloadPath, "MishnatYosef");
                Directory.CreateDirectory(chainDir);

                // קבלת כל הקבצים הזמינים
                var availableFiles = await GetAvailableFiles(date);

                if (!availableFiles.Any())
                {
                    result.ErrorMessage = "לא נמצאו קבצים זמינים למרות מספר ניסיונות";
                    Console.WriteLine($"      ❌ לא נמצאו קבצים זמינים להיום");
                    result.Duration = (DateTime.Now - startTime).TotalSeconds;
                    return result;
                }

                // ניתוח הקבצים
                AnalyzeAvailableFiles(availableFiles);

                // שלב 1: הורדת קבצי Stores
                result.StoresFiles = await DownloadStoresFiles(availableFiles, chainDir);

                // שלב 2: זיהוי סניפים
                var stores = GetUniqueStores(availableFiles);
                Console.WriteLine($"      📍 זוהו {stores.Count} סניפים");

                if (stores.Any())
                {
                    // שלב 3: הורדת קבצי מחירים
                    result.PriceFiles = await DownloadPriceFiles(availableFiles, stores, chainDir);

                    // שלב 4: הורדת קבצי מבצעים
                    result.PromoFiles = await DownloadPromoFiles(availableFiles, stores, chainDir);
                }

                // סיכום
                result.DownloadedFiles = result.StoresFiles + result.PriceFiles + result.PromoFiles;
                result.Success = true;
                result.Duration = (DateTime.Now - startTime).TotalSeconds;

                Console.WriteLine($"      📊 סיכום משנת יוסף: {result.StoresFiles} Stores + {result.PriceFiles} Prices + {result.PromoFiles} Promos = {result.DownloadedFiles} סה\"כ");
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

        /// <summary>
        /// ניתוח קבצים זמינים
        /// </summary>
        private void AnalyzeAvailableFiles(List<FileMetadata> files)
        {
            var types = files.GroupBy(f => DetermineFileType(f.FileNm)).ToDictionary(g => g.Key, g => g.Count());
            Console.WriteLine($"      🔍 ניתוח קבצים מ-API משנת יוסף:");
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
            Console.WriteLine($"      🎯 מוריד: {latestStores.FileNm}");

            var success = await DownloadAndSaveFile(latestStores, chainDir, "Stores");
            return success ? 1 : 0;
        }

        /// <summary>
        /// הורדת קבצי מחירים - הורדת גם Price וגם PriceFull
        /// </summary>
        private async Task<int> DownloadPriceFiles(List<FileMetadata> availableFiles, List<string> stores, string chainDir)
        {
            Console.WriteLine($"      💰 מוריד קבצי Price...");

            int downloaded = 0;

            foreach (var store in stores)
            {
                // חיפוש קבצי PriceFull
                var priceFullFiles = availableFiles
                    .Where(f => f.FileNm.ToLower().Contains("pricefull") &&
                               ExtractStoreFromFileName(f.FileNm) == store)
                    .OrderByDescending(f => ExtractTimeFromFileName(f.FileNm))
                    .ToList();

                // חיפוש קבצי Price רגיל
                var priceFiles = availableFiles
                    .Where(f => f.FileNm.ToLower().Contains("price") &&
                               !f.FileNm.ToLower().Contains("pricefull") &&
                               !f.FileNm.ToLower().Contains("promo") &&
                               ExtractStoreFromFileName(f.FileNm) == store)
                    .OrderByDescending(f => ExtractTimeFromFileName(f.FileNm))
                    .ToList();

                // הורדת PriceFull אם קיים
                if (priceFullFiles.Any())
                {
                    var latestPriceFull = priceFullFiles.First();
                    Console.WriteLine($"         🎯 סניף {store} PriceFull: {latestPriceFull.FileNm}");

                    await Task.Delay(_random.Next(500, 1500));
                    var success = await DownloadAndSaveFile(latestPriceFull, chainDir, "PriceFull");
                    if (success) downloaded++;
                }

                // הורדת Price רגיל אם קיים
                if (priceFiles.Any())
                {
                    var latestPrice = priceFiles.First();
                    Console.WriteLine($"         🎯 סניף {store} Price: {latestPrice.FileNm}");

                    await Task.Delay(_random.Next(500, 1500));
                    var success = await DownloadAndSaveFile(latestPrice, chainDir, "Price");
                    if (success) downloaded++;
                }
            }

            Console.WriteLine($"      💰 הורדו {downloaded} קבצי Price");
            return downloaded;
        }

        /// <summary>
        /// הורדת קבצי מבצעים
        /// </summary>
        private async Task<int> DownloadPromoFiles(List<FileMetadata> availableFiles, List<string> stores, string chainDir)
        {
            Console.WriteLine($"      🎁 מחפש קבצי Promo...");

            int downloaded = 0;

            foreach (var store in stores)
            {
                var promoFiles = availableFiles
                    .Where(f => f.FileNm.ToLower().Contains("promo") &&
                               ExtractStoreFromFileName(f.FileNm) == store)
                    .OrderByDescending(f => f.FileNm.ToLower().Contains("promofull") ? 1 : 0)
                    .ThenByDescending(f => ExtractTimeFromFileName(f.FileNm))
                    .ToList();

                if (promoFiles.Any())
                {
                    var latestPromo = promoFiles.First();
                    var fileType = latestPromo.FileNm.ToLower().Contains("promofull") ? "PromoFull" : "Promo";

                    Console.WriteLine($"         🎯 סניף {store}: {latestPromo.FileNm}");

                    // עיכוב קל בין הורדות
                    await Task.Delay(_random.Next(500, 1500));

                    var success = await DownloadAndSaveFile(latestPromo, chainDir, fileType);
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
        /// הורדה ושמירת קובץ - גרסה מיוחדת למשנת יוסף
        /// </summary>
        private async Task<bool> DownloadAndSaveFile(FileMetadata fileInfo, string chainDir, string fileType)
        {
            try
            {
                var typeDir = Path.Combine(chainDir, fileType);
                Directory.CreateDirectory(typeDir);

                // במשנת יוסף, ה-URL מגיע מוכן להורדה מה-API
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
        /// קבלת קישור הורדה ישיר - בצורה שונה ממערכת בינה פרוגקט
        /// </summary>
        private async Task<string> GetDirectDownloadUrl(string fileName)
        {
            try
            {
                // בחזרה ל-API לקבלת הקישור המעודכן
                var response = await _httpClient.GetAsync(API_BASE_URL);

                if (!response.IsSuccessStatusCode)
                    return "";

                var jsonContent = await response.Content.ReadAsStringAsync();
                var files = JsonSerializer.Deserialize<List<MishnatYosefFileInfo>>(jsonContent) ?? new List<MishnatYosefFileInfo>();

                var matchingFile = files.FirstOrDefault(f => f.name.Equals(fileName, StringComparison.OrdinalIgnoreCase));

                return matchingFile?.url ?? "";
            }
            catch
            {
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

        // ========== פונקציות עזר ==========

        private List<string> GetUniqueStores(List<FileMetadata> files)
        {
            return files
                .Where(f => f.FileNm.ToLower().Contains("price") || f.FileNm.ToLower().Contains("promo"))
                .Select(f => ExtractStoreFromFileName(f.FileNm))
                .Where(s => !string.IsNullOrEmpty(s))
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
}