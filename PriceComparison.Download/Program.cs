/*using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;


var builder = Host.CreateDefaultBuilder(args)

    .ConfigureAppConfiguration((context, config) =>
    {
        config.AddJsonFile("appsettings.json");
    })
    .ConfigureServices((context, services) =>
    {
        services.AddSingleton<DownloadCoordinator>();
    });

Console.WriteLine("🚀 Starting PriceComparison.Download...");

var host = builder.Build();
var coordinator = host.Services.GetRequiredService<DownloadCoordinator>();
await coordinator.RunAsync();

Console.WriteLine("🏁 Finished.");*/

/*using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using System.Linq;
using System.Globalization;

namespace PriceComparison.Download
{
    /// <summary>
    /// מוריד מדויק לקינג סטור:
    /// 1. StoresFull העדכני ביותר להיום
    /// 2. PriceFull העדכני ביותר לכל סניף
    /// 3. PromoFull העדכני ביותר לכל סניף
    /// 4. חילוץ ZIP לקבצי XML
    /// </summary>
    class Program
    {
        private static readonly string BASE_URL = "https://kingstore.binaprojects.com";
        private static readonly string DOWNLOAD_FOLDER = "DownloadedFiles";
        private static readonly HttpClient httpClient = new HttpClient();

        static async Task Main(string[] args)
        {
            Console.WriteLine("🏪 מוריד קינג סטור - גרסה מדויקת");
            Console.WriteLine("📋 מטרה: StoresFull + PriceFull + PromoFull (העדכניים ביותר)");

            try
            {
                SetupHttpClient();
                CreateDownloadFolder();

                var todayDate = DateTime.Now.ToString("dd/MM/yyyy");
                Console.WriteLine($"📅 מחפש קבצים לתאריך: {todayDate}");

                // שלב 1: הורדת StoresFull העדכני ביותר
                Console.WriteLine("\n🏢 שלב 1: הורדת קובץ סניפים (StoresFull)...");
                await DownloadLatestStoresFull(todayDate);

                // שלב 2: קבלת רשימת כל הסניפים הזמינים
                Console.WriteLine("\n📍 שלב 2: זיהוי כל הסניפים הזמינים...");
                var allStores = await GetAllAvailableStores(todayDate);

                // שלב 3: הורדת PriceFull העדכני ביותר לכל סניף
                Console.WriteLine("\n💰 שלב 3: הורדת PriceFull לכל סניף...");
                await DownloadLatestPriceFullForAllStores(todayDate, allStores);

                // שלב 4: הורדת PromoFull העדכני ביותר לכל סניף
                Console.WriteLine("\n🎁 שלב 4: הורדת PromoFull לכל סניף...");
                await DownloadLatestPromoFullForAllStores(todayDate, allStores);

                Console.WriteLine("\n✅ כל ההורדות הושלמו!");
                ShowFinalSummary();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ שגיאה: {ex.Message}");
                Console.WriteLine($"📋 פרטים: {ex}");
            }

            Console.WriteLine("\n🔍 לחץ מקש כלשהו לסיום...");
            Console.ReadKey();
        }

        private static void SetupHttpClient()
        {
            httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            httpClient.DefaultRequestHeaders.Add("Accept", "*");
            httpClient.DefaultRequestHeaders.Add("Accept-Language", "he-IL,he;q=0.8");
            Console.WriteLine("🌐 HTTP Client הוכן");
        }

        private static void CreateDownloadFolder()
        {
            if (!Directory.Exists(DOWNLOAD_FOLDER))
                Directory.CreateDirectory(DOWNLOAD_FOLDER);

            var subFolders = new[] { "StoresFull", "PriceFull", "PromoFull", "ZIP_Files", "Raw_Data" };
            foreach (var folder in subFolders)
            {
                var path = Path.Combine(DOWNLOAD_FOLDER, folder);
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
            }

            Console.WriteLine($"📁 תיקיות מוכנות: {Path.GetFullPath(DOWNLOAD_FOLDER)}");
        }

        /// <summary>
        /// הורדת קובץ StoresFull העדכני ביותר - מתוקן
        /// </summary>
        private static async Task DownloadLatestStoresFull(string date)
        {
            Console.WriteLine("🔍 מחפש קבצי StoresFull...");

            // חיפוש ספציפי לקבצי StoresFull
            var allStoresFiles = await GetFilesByPattern(date, "", new[] { "store" });

            // סינון רק לקבצי StoresFull (לא Store רגיל)
            var storesFullFiles = allStoresFiles.Where(f =>
                f.FileName.ToLower().Contains("storesfull") ||
                f.FileName.ToLower().Contains("stores_full") ||
                f.FileName.ToLower().Contains("storesmichsan") ||
                (f.FileName.ToLower().Contains("store") && f.FileName.ToLower().Contains("full"))
            ).ToList();

            Console.WriteLine($"🏪 נמצאו {storesFullFiles.Count} קבצי StoresFull:");
            foreach (var file in storesFullFiles)
            {
                var parsedDate = ParseDateTime(file.DateFile);
                Console.WriteLine($"   📄 {file.FileName} - {file.DateFile} (parsed: {parsedDate:HH:mm:ss})");
            }

            if (!storesFullFiles.Any())
            {
                Console.WriteLine("⚠️ לא נמצאו קבצי StoresFull");
                return;
            }

            // מיון לפי תאריך ושעה - העדכני ביותר (הגבוה ביותר)
            var latestStoresFile = storesFullFiles
                .OrderByDescending(f => ParseDateTime(f.DateFile))
                .FirstOrDefault();

            if (latestStoresFile != null)
            {
                Console.WriteLine($"📥 נבחר הקובץ העדכני ביותר: {latestStoresFile.FileName} ({latestStoresFile.DateFile})");
                await DownloadAndExtractFile(latestStoresFile, "StoresFull");
            }
        }

        /// <summary>
        /// קבלת כל הסניפים הזמינים מהמערכת
        /// </summary>
        private static async Task<List<string>> GetAllAvailableStores(string date)
        {
            var stores = new HashSet<string>();

            // נקבל קבצים מכל הסוגים ונחלץ מהם את שמות הסניפים
            var allFiles = await GetAllFilesForDate(date);

            foreach (var file in allFiles)
            {
                if (!string.IsNullOrEmpty(file.Store) && file.Store.Trim() != "")
                {
                    stores.Add(file.Store.Trim());
                }
            }

            var storesList = stores.OrderBy(s => s).ToList();
            Console.WriteLine($"🏪 נמצאו {storesList.Count} סניפים:");

            foreach (var store in storesList.Take(10))
            {
                Console.WriteLine($"   🏪 {store}");
            }

            if (storesList.Count > 10)
                Console.WriteLine($"   ... ועוד {storesList.Count - 10} סניפים");

            return storesList;
        }

        /// <summary>
        /// הורדת PriceFull העדכני ביותר לכל סניף - מתוקן
        /// </summary>
        private static async Task DownloadLatestPriceFullForAllStores(string date, List<string> stores)
        {
            Console.WriteLine("🔍 מחפש קבצי PriceFull בלבד...");

            var allPriceFiles = await GetFilesByPattern(date, "", new[] { "price", "מחיר" });

            // סינון מדויק יותר - רק PriceFull ולא Price רגיל
            var priceFullFiles = allPriceFiles.Where(f =>
                f.FileName.ToLower().Contains("pricefull") ||
                f.FileName.ToLower().Contains("price_full") ||
                (f.FileName.ToLower().Contains("price") && f.FileName.ToLower().Contains("full")) ||
                f.TypeFile.ToLower().Contains("מחירים מלא") ||
                f.TypeFile.ToLower().Contains("pricefull")
            ).ToList();

            // הוצאת קבצי Price רגילים (בלי Full)
            priceFullFiles = priceFullFiles.Where(f =>
                !f.FileName.ToLower().Equals("price") &&
                !f.FileName.ToLower().StartsWith("price7") && // למנוע Price7290058108879 רגיל
                f.FileName.ToLower().Contains("full")
            ).ToList();

            Console.WriteLine($"💰 נמצאו {priceFullFiles.Count} קבצי PriceFull:");
            foreach (var file in priceFullFiles.Take(10))
            {
                var parsedDate = ParseDateTime(file.DateFile);
                Console.WriteLine($"   📄 {file.FileName} - {file.Store} - {file.DateFile} (parsed: {parsedDate:HH:mm:ss})");
            }

            if (!priceFullFiles.Any())
            {
                Console.WriteLine("⚠️ לא נמצאו קבצי PriceFull. נבדוק את כל הסוגים...");
                await InvestigateAllFileTypes(date);
                return;
            }

            int downloadedCount = 0;
            foreach (var store in stores.Take(5)) // נגביל ל-5 ראשונים לבדיקה
            {
                Console.WriteLine($"\n🔍 מחפש קבצי PriceFull לסניף: {store}");

                var storeFiles = priceFullFiles
                    .Where(f => f.Store.Contains(store) || store.Contains(f.Store))
                    .OrderByDescending(f => ParseDateTime(f.DateFile))
                    .ToList();

                Console.WriteLine($"   📁 נמצאו {storeFiles.Count} קבצי PriceFull לסניף זה");
                foreach (var file in storeFiles)
                {
                    var parsedDate = ParseDateTime(file.DateFile);
                    Console.WriteLine($"      🕒 {file.FileName} - {file.DateFile} (parsed: {parsedDate:HH:mm:ss})");
                }

                var storeLatestPrice = storeFiles.FirstOrDefault();

                if (storeLatestPrice != null)
                {
                    Console.WriteLine($"📥 נבחר העדכני ביותר: {storeLatestPrice.FileName}");
                    await DownloadAndExtractFile(storeLatestPrice, "PriceFull");
                    downloadedCount++;
                    await Task.Delay(500);
                }
                else
                {
                    Console.WriteLine($"⚠️ לא נמצא PriceFull לסניף: {store}");
                }
            }

            Console.WriteLine($"✅ הורדו {downloadedCount} קבצי PriceFull");
        }

        /// <summary>
        /// הורדת PromoFull העדכני ביותר לכל סניף - מתוקן
        /// </summary>
        private static async Task DownloadLatestPromoFullForAllStores(string date, List<string> stores)
        {
            Console.WriteLine("🔍 מחפש קבצי PromoFull בלבד...");

            var allPromoFiles = await GetFilesByPattern(date, "", new[] { "promo", "מבצע" });

            // סינון מדויק יותר - רק PromoFull ולא Promo רגיל
            var promoFullFiles = allPromoFiles.Where(f =>
                f.FileName.ToLower().Contains("promofull") ||
                f.FileName.ToLower().Contains("promo_full") ||
                (f.FileName.ToLower().Contains("promo") && f.FileName.ToLower().Contains("full")) ||
                f.TypeFile.ToLower().Contains("מבצעים מלא") ||
                f.TypeFile.ToLower().Contains("promofull")
            ).ToList();

            Console.WriteLine($"🎁 נמצאו {promoFullFiles.Count} קבצי PromoFull:");
            foreach (var file in promoFullFiles.Take(10))
            {
                var parsedDate = ParseDateTime(file.DateFile);
                Console.WriteLine($"   📄 {file.FileName} - {file.Store} - {file.DateFile} (parsed: {parsedDate:HH:mm:ss})");
            }

            if (!promoFullFiles.Any())
            {
                Console.WriteLine("⚠️ לא נמצאו קבצי PromoFull");
                return;
            }

            int downloadedCount = 0;
            foreach (var store in stores.Take(5)) // נגביל ל-5 ראשונים לבדיקה
            {
                Console.WriteLine($"\n🔍 מחפש קבצי PromoFull לסניף: {store}");

                var storeFiles = promoFullFiles
                    .Where(f => f.Store.Contains(store) || store.Contains(f.Store))
                    .OrderByDescending(f => ParseDateTime(f.DateFile))
                    .ToList();

                Console.WriteLine($"   📁 נמצאו {storeFiles.Count} קבצי PromoFull לסניף זה");
                foreach (var file in storeFiles)
                {
                    var parsedDate = ParseDateTime(file.DateFile);
                    Console.WriteLine($"      🕒 {file.FileName} - {file.DateFile} (parsed: {parsedDate:HH:mm:ss})");
                }

                var storeLatestPromo = storeFiles.FirstOrDefault();

                if (storeLatestPromo != null)
                {
                    Console.WriteLine($"📥 נבחר העדכני ביותר: {storeLatestPromo.FileName}");
                    await DownloadAndExtractFile(storeLatestPromo, "PromoFull");
                    downloadedCount++;
                    await Task.Delay(500);
                }
                else
                {
                    Console.WriteLine($"⚠️ לא נמצא PromoFull לסניף: {store}");
                }
            }

            Console.WriteLine($"✅ הורדו {downloadedCount} קבצי PromoFull");
        }

        /// <summary>
        /// הורדה וחילוץ קובץ יחיד (ZIP → XML)
        /// </summary>
        private static async Task DownloadAndExtractFile(KingStoreFileInfo fileInfo, string targetFolder)
        {
            try
            {
                // שלב 1: קבלת קישור ההורדה
                var metaResponse = await httpClient.PostAsync(
                    $"{BASE_URL}/Download.aspx?FileNm={fileInfo.FileName}",
                    new StringContent(""));

                if (metaResponse.IsSuccessStatusCode)
                {
                    var metaContent = await metaResponse.Content.ReadAsStringAsync();
                    var downloadUrl = ExtractDownloadUrl(metaContent);

                    if (!string.IsNullOrEmpty(downloadUrl))
                    {
                        // שלב 2: הורדת קובץ ה-ZIP
                        var fileResponse = await httpClient.GetAsync(downloadUrl);

                        if (fileResponse.IsSuccessStatusCode)
                        {
                            var zipBytes = await fileResponse.Content.ReadAsByteArrayAsync();

                            // שלב 3: שמירת ה-ZIP (לבדיקה)
                            string zipFileName = $"{fileInfo.FileName}_{DateTime.Now:yyyyMMdd_HHmmss}.zip";
                            string zipPath = Path.Combine(DOWNLOAD_FOLDER, "ZIP_Files", zipFileName);
                            await File.WriteAllBytesAsync(zipPath, zipBytes);

                            // שלב 4: חילוץ ה-XML מה-ZIP
                            await ExtractXmlFromZip(zipBytes, fileInfo, targetFolder);

                            Console.WriteLine($"💾 הושלם: {fileInfo.FileName} ({zipBytes.Length:N0} bytes)");
                        }
                        else
                        {
                            Console.WriteLine($"❌ כישלון בהורדת {fileInfo.FileName}: {fileResponse.StatusCode}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"⚠️ לא נמצא קישור להורדה עבור {fileInfo.FileName}");
                        // שמירת המטא-דאטה לבדיקה
                        string metaPath = Path.Combine(DOWNLOAD_FOLDER, "Raw_Data", $"{fileInfo.FileName}_meta.json");
                        await File.WriteAllTextAsync(metaPath, metaContent);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ שגיאה בהורדת {fileInfo.FileName}: {ex.Message}");
            }
        }

        /// <summary>
        /// חילוץ קובץ XML מתוך ZIP
        /// </summary>
        private static async Task ExtractXmlFromZip(byte[] zipBytes, KingStoreFileInfo fileInfo, string targetFolder)
        {
            try
            {
                using var zipStream = new MemoryStream(zipBytes);
                using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

                foreach (var entry in archive.Entries)
                {
                    if (entry.Name.ToLower().EndsWith(".xml"))
                    {
                        using var entryStream = entry.Open();
                        using var reader = new StreamReader(entryStream);
                        var xmlContent = await reader.ReadToEndAsync();

                        // שמירת קובץ ה-XML
                        string xmlFileName = $"{fileInfo.Store}_{fileInfo.TypeFile}_{DateTime.Now:yyyyMMdd_HHmmss}.xml";
                        // ניקוי תווים לא חוקיים מהשם
                        xmlFileName = string.Join("_", xmlFileName.Split(Path.GetInvalidFileNameChars()));

                        string xmlPath = Path.Combine(DOWNLOAD_FOLDER, targetFolder, xmlFileName);
                        await File.WriteAllTextAsync(xmlPath, xmlContent);

                        Console.WriteLine($"📄 חולץ XML: {xmlFileName} ({xmlContent.Length:N0} chars)");
                        break; // נקח רק את ה-XML הראשון
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ שגיאה בחילוץ ZIP עבור {fileInfo.FileName}: {ex.Message}");
            }
        }

        /// <summary>
        /// קבלת קבצים לפי דפוס חיפוש
        /// </summary>
        private static async Task<List<KingStoreFileInfo>> GetFilesByPattern(string date, string store, string[] searchTerms)
        {
            var allFiles = new List<KingStoreFileInfo>();

            // נחפש בכל סוגי הקבצים (0-5)
            for (int fileType = 0; fileType <= 5; fileType++)
            {
                var files = await GetFilesFromServer(date, store, fileType.ToString());

                if (searchTerms != null && searchTerms.Any())
                {
                    files = files.Where(f =>
                        searchTerms.Any(term =>
                            f.FileName.ToLower().Contains(term.ToLower()) ||
                            f.TypeFile.ToLower().Contains(term.ToLower())
                        )
                    ).ToList();
                }

                allFiles.AddRange(files);
                await Task.Delay(100); // זמן המתנה קצר בין בקשות
            }

            return allFiles.Distinct().ToList();
        }

        /// <summary>
        /// קבלת כל הקבצים לתאריך נתון
        /// </summary>
        private static async Task<List<KingStoreFileInfo>> GetAllFilesForDate(string date)
        {
            return await GetFilesByPattern(date, "", null); // בלי סינון - כל הקבצים
        }

        /// <summary>
        /// קבלת קבצים מהשרת
        /// </summary>
        private static async Task<List<KingStoreFileInfo>> GetFilesFromServer(string date, string store, string fileType)
        {
            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("WStore", store),
                new KeyValuePair<string, string>("WDate", date),
                new KeyValuePair<string, string>("WFileType", fileType)
            });

            try
            {
                var response = await httpClient.PostAsync($"{BASE_URL}/MainIO_Hok.aspx", formContent);

                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync();
                    return ParseFilesList(jsonContent);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ שגיאה בקבלת קבצים מסוג {fileType}: {ex.Message}");
            }

            return new List<KingStoreFileInfo>();
        }

        /// <summary>
        /// חילוץ קישור ההורדה מהמטא-דאטה
        /// </summary>
        private static string ExtractDownloadUrl(string metaContent)
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(metaContent);
                foreach (JsonElement element in doc.RootElement.EnumerateArray())
                {
                    if (element.TryGetProperty("SPath", out JsonElement pathProp))
                    {
                        return pathProp.GetString() ?? "";
                    }
                }
            }
            catch (JsonException)
            {
                // חיפוש URL בצורה פשוטה יותר
                if (metaContent.Contains("http"))
                {
                    var lines = metaContent.Split('\n');
                    foreach (var line in lines)
                    {
                        if (line.Contains("http") && (line.Contains(".zip") || line.Contains(".xml")))
                        {
                            return line.Trim().Trim('"');
                        }
                    }
                }
            }

            return "";
        }

        /// <summary>
        /// חקירה מעמיקה של כל סוגי הקבצים הזמינים
        /// </summary>
        private static async Task InvestigateAllFileTypes(string date)
        {
            Console.WriteLine("\n🔍 חקירה מעמיקה - בודק כל סוג קובץ:");

            for (int fileType = 0; fileType <= 5; fileType++)
            {
                Console.WriteLine($"\n--- סוג קובץ {fileType} ---");

                var files = await GetFilesFromServer(date, "", fileType.ToString());

                Console.WriteLine($"📁 נמצאו {files.Count} קבצים מסוג {fileType}");

                if (files.Any())
                {
                    // הצגת סוגי הקבצים הייחודיים
                    var uniqueTypes = files.Select(f => f.TypeFile).Distinct().ToList();
                    Console.WriteLine($"🏷️ סוגי קבצים: {string.Join(", ", uniqueTypes)}");

                    // הצגת כמה דוגמאות
                    foreach (var file in files.Take(3))
                    {
                        Console.WriteLine($"   📄 {file.FileName} | {file.TypeFile} | {file.Store}");
                    }

                    if (files.Count > 3)
                        Console.WriteLine($"   ... ועוד {files.Count - 3} קבצים");
                }

                await Task.Delay(300);
            }
        }

        /// <summary>
        /// פיענוח תאריך מחרוזת - מתוקן לטיפול נכון בשעות
        /// </summary>
        private static DateTime ParseDateTime(string dateStr)
        {
            if (string.IsNullOrEmpty(dateStr))
                return DateTime.MinValue;

            // נסה פורמטים שונים של תאריך
            var formats = new[]
            {
                "dd/MM/yyyy HH:mm:ss",
                "dd/MM/yyyy HH:mm",
                "yyyy-MM-dd HH:mm:ss",
                "dd/MM/yyyy",
                "MM/dd/yyyy HH:mm:ss",
                "MM/dd/yyyy HH:mm"
            };

            foreach (var format in formats)
            {
                if (DateTime.TryParseExact(dateStr, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime result))
                {
                    Console.WriteLine($"🕒 נתח תאריך: {dateStr} → {result:dd/MM/yyyy HH:mm:ss}");
                    return result;
                }
            }

            // אם כל הניסיונות נכשלו, נסה Parse רגיל
            if (DateTime.TryParse(dateStr, out DateTime fallbackResult))
            {
                Console.WriteLine($"🕒 נתח תאריך (fallback): {dateStr} → {fallbackResult:dd/MM/yyyy HH:mm:ss}");
                return fallbackResult;
            }

            Console.WriteLine($"⚠️ לא הצליח לנתח תאריך: {dateStr}");
            return DateTime.MinValue;
        }

        /// <summary>
        /// פיענוח רשימת קבצים מ-JSON
        /// </summary>
        private static List<KingStoreFileInfo> ParseFilesList(string jsonContent)
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(jsonContent);
                var files = new List<KingStoreFileInfo>();

                foreach (JsonElement element in doc.RootElement.EnumerateArray())
                {
                    if (element.TryGetProperty("FileNm", out JsonElement fileNmProp))
                    {
                        var fileInfo = new KingStoreFileInfo
                        {
                            FileName = fileNmProp.GetString() ?? "",
                            Company = element.TryGetProperty("Company", out var comp) ? comp.GetString() ?? "" : "",
                            Store = element.TryGetProperty("Store", out var store) ? store.GetString() ?? "" : "",
                            TypeFile = element.TryGetProperty("TypeFile", out var type) ? type.GetString() ?? "" : "",
                            DateFile = element.TryGetProperty("DateFile", out var date) ? date.GetString() ?? "" : ""
                        };

                        files.Add(fileInfo);
                    }
                }

                return files;
            }
            catch (JsonException)
            {
                return new List<KingStoreFileInfo>();
            }
        }

        /// <summary>
        /// הצגת סיכום סופי
        /// </summary>
        private static void ShowFinalSummary()
        {
            Console.WriteLine("\n📊 סיכום סופי:");

            var folders = new[] { "StoresFull", "PriceFull", "PromoFull" };
            int totalXmlFiles = 0;

            foreach (var folder in folders)
            {
                var path = Path.Combine(DOWNLOAD_FOLDER, folder);
                if (Directory.Exists(path))
                {
                    var xmlFiles = Directory.GetFiles(path, "*.xml");
                    Console.WriteLine($"📁 {folder}: {xmlFiles.Length} קבצי XML");
                    totalXmlFiles += xmlFiles.Length;

                    // הצגת גודל כולל
                    var totalSize = xmlFiles.Sum(f => new System.IO.FileInfo(f).Length);
                    Console.WriteLine($"   💾 גודל כולל: {totalSize:N0} bytes");
                }
            }

            Console.WriteLine($"\n🎯 סה\"כ: {totalXmlFiles} קבצי XML מוכנים לעיבוד!");
            Console.WriteLine($"📂 תיקיית ההורדות: {Path.GetFullPath(DOWNLOAD_FOLDER)}");
        }

        public class KingStoreFileInfo
        {
            public string FileName { get; set; } = "";
            public string Company { get; set; } = "";
            public string Store { get; set; } = "";
            public string TypeFile { get; set; } = "";
            public string DateFile { get; set; } = "";
        }
    }
}*/

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using System.Linq;
using System.Globalization;
using System.Text;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System.Threading;

namespace PriceComparison.Download
{
    /// <summary>
    /// מוריד אוטומטי לקינג סטור עם Azure Storage:
    /// 1. רץ אוטומטית כל יום ב-3 בלילה
    /// 2. מוריד StoresFull + PriceFull + PromoFull לכל הסניפים
    /// 3. שומר ל-Azure Storage במבנה מסודר
    /// 4. מבצע החלפה בטוחה ללא הפרעה לשימוש
    /// </summary>
    class Program
    {
        // הגדרות Azure Storage
        
        private static readonly string CONTAINER_NAME = "price-comparison-data";

        // הגדרות קינג סטור
        private static readonly string BASE_URL = "https://kingstore.binaprojects.com";
        private static readonly string CHAIN_PREFIX = "kingstore";

        // שירותי Azure
        private static BlobServiceClient blobServiceClient;
        private static BlobContainerClient containerClient;
        private static readonly HttpClient httpClient = new HttpClient();

        // הגדרות זמן
        private static readonly TimeSpan SCHEDULE_TIME = new TimeSpan(3, 0, 0); // 3:00 AM
        private static readonly Timer schedulerTimer;

        static async Task Main(string[] args)
        {
            Console.WriteLine("🚀 מתחיל מוריד קינג סטור אוטומטי עם Azure Storage");
            Console.WriteLine($"📅 מתוזמן לרוץ כל יום ב-{SCHEDULE_TIME:hh\\:mm}");

            try
            {
                // הגדרה ראשונית
                await InitializeServices();

                // בדיקה אם להריץ עכשיו או לחכות לתזמון
                if (args.Length > 0 && args[0] == "--run-now")
                {
                    Console.WriteLine("🔄 מריץ עכשיו לפי בקשה...");
                    await RunDailyDownload();
                }
                else if (args.Length > 0 && args[0] == "--schedule")
                {
                    Console.WriteLine("⏰ מפעיל מצב תזמון אוטומטי...");
                    StartScheduler();

                    // המתנה אינסופית
                    Console.WriteLine("🔄 התוכנית רצה ברקע. לחץ Ctrl+C לעצירה.");
                    while (true)
                    {
                        await Task.Delay(60000); // בדיקה כל דקה
                        ShowStatus();
                    }
                }
                else
                {
                    Console.WriteLine("🔧 אפשרויות הרצה:");
                    Console.WriteLine("  --run-now    : הרץ מיידית");
                    Console.WriteLine("  --schedule   : הפעל תזמון אוטומטי");
                    Console.WriteLine("\n🔄 מריץ פעם אחת עכשיו...");
                    await RunDailyDownload();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ שגיאה קריטית: {ex.Message}");
                Console.WriteLine($"📋 פרטים מלאים: {ex}");
            }

            Console.WriteLine("\n🔍 לחץ מקש כלשהו לסיום...");
            Console.ReadKey();
        }

        /// <summary>
        /// אתחול כל השירותים הנדרשים
        /// </summary>
        private static async Task InitializeServices()
        {
            Console.WriteLine("🔧 מאתחל שירותים...");

            // הגדרת HTTP Client
            SetupHttpClient();

            // הגדרת Azure Storage
            await SetupAzureStorage();

            Console.WriteLine("✅ כל השירותים מוכנים");
        }

        /// <summary>
        /// הגדרת HTTP Client לחיקוי דפדפן
        /// </summary>
        private static void SetupHttpClient()
        {
            httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            httpClient.DefaultRequestHeaders.Add("Accept", "*/*");
            httpClient.DefaultRequestHeaders.Add("Accept-Language", "he-IL,he;q=0.8");

            Console.WriteLine("🌐 HTTP Client הוכן (מדמה Chrome)");
        }

        /// <summary>
        /// הגדרת Azure Storage וכנטיינר
        /// </summary>
        private static async Task SetupAzureStorage()
        {
            try
            {
                blobServiceClient = new BlobServiceClient(AZURE_CONNECTION_STRING);
                containerClient = blobServiceClient.GetBlobContainerClient(CONTAINER_NAME);

                // יצירת Container אם לא קיים
                await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);

                Console.WriteLine($"☁️ Azure Storage מוכן: {CONTAINER_NAME}");
                Console.WriteLine($"🔗 Account: {blobServiceClient.AccountName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ שגיאה בהגדרת Azure Storage: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// הפעלת התזמון האוטומטי
        /// </summary>
        private static void StartScheduler()
        {
            var now = DateTime.Now;
            var scheduledTime = now.Date.Add(SCHEDULE_TIME);

            // אם השעה כבר עברה היום, תזמן למחר
            if (scheduledTime <= now)
                scheduledTime = scheduledTime.AddDays(1);

            var delay = scheduledTime - now;

            Console.WriteLine($"⏰ תזמון הוגדר: {scheduledTime:dd/MM/yyyy HH:mm:ss}");
            Console.WriteLine($"⏱️ זמן המתנה: {delay.TotalHours:F1} שעות");

            var timer = new Timer(async _ => await RunDailyDownload(), null, delay, TimeSpan.FromDays(1));
        }

        /// <summary>
        /// הרצה יומית - הפונקציה הראשית שרצה כל יום
        /// </summary>
        private static async Task RunDailyDownload()
        {
            var startTime = DateTime.Now;
            Console.WriteLine($"\n🚀 מתחיל הורדה יומית: {startTime:dd/MM/yyyy HH:mm:ss}");
            Console.WriteLine("=" + new string('=', 50));

            try
            {
                var todayDate = DateTime.Now.ToString("dd/MM/yyyy");
                var todayPath = DateTime.Now.ToString("yyyy/MM/dd");

                // שלב 1: הורדת StoresFull
                Console.WriteLine($"\n🏢 שלב 1: הורדת קובץ סניפים להיום ({todayDate})...");
                await DownloadLatestStoresFull(todayDate, todayPath);

                // שלב 2: קבלת רשימת סניפים
                Console.WriteLine("\n📍 שלב 2: זיהוי כל הסניפים...");
                var allStores = await GetAllAvailableStores(todayDate);

                if (!allStores.Any())
                {
                    Console.WriteLine("⚠️ לא נמצאו סניפים - מפסיק");
                    return;
                }

                // שלב 3: הורדת קבצי מחירים לכל הסניפים
                Console.WriteLine($"\n💰 שלב 3: הורדת PriceFull ל-{allStores.Count} סניפים...");
                await DownloadLatestPriceFullForAllStores(todayDate, todayPath, allStores);

                // שלב 4: הורדת קבצי מבצעים לכל הסניפים
                Console.WriteLine($"\n🎁 שלב 4: הורדת PromoFull ל-{allStores.Count} סניפים...");
                await DownloadLatestPromoFullForAllStores(todayDate, todayPath, allStores);

                // שלב 5: מחיקת נתונים ישנים (למעט היום)
                Console.WriteLine("\n🧹 שלב 5: מחיקת נתונים ישנים...");
                await CleanupOldData(todayPath);

                // שלב 6: סיכום
                var endTime = DateTime.Now;
                var duration = endTime - startTime;

                Console.WriteLine("\n📊 סיכום הורדה יומית:");
                await ShowAzureSummary(todayPath);
                Console.WriteLine($"⏱️ משך זמן: {duration.TotalMinutes:F1} דקות");
                Console.WriteLine($"✅ הושלם בהצלחה: {endTime:dd/MM/yyyy HH:mm:ss}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ שגיאה בהורדה יומית: {ex.Message}");
                Console.WriteLine($"📋 פרטים: {ex}");

                // כאן ניתן להוסיף התראה (מייל, SMS וכו')
                await LogError($"שגיאה בהורדה יומית: {ex.Message}");
            }
        }

        /// <summary>
        /// הורדת קובץ StoresFull העדכני ביותר
        /// </summary>
        private static async Task DownloadLatestStoresFull(string date, string datePath)
        {
            try
            {
                var allStoresFiles = await GetFilesByPattern(date, "", new[] { "store" });

                var storesFullFiles = allStoresFiles.Where(f =>
                    f.FileName.ToLower().Contains("storesfull") ||
                    f.FileName.ToLower().Contains("stores_full") ||
                    (f.FileName.ToLower().Contains("store") && f.FileName.ToLower().Contains("full"))
                ).ToList();

                Console.WriteLine($"🏪 נמצאו {storesFullFiles.Count} קבצי StoresFull");

                if (!storesFullFiles.Any())
                {
                    Console.WriteLine("⚠️ לא נמצאו קבצי StoresFull");
                    return;
                }

                var latestStoresFile = storesFullFiles
                    .OrderByDescending(f => ParseDateTime(f.DateFile))
                    .FirstOrDefault();

                if (latestStoresFile != null)
                {
                    Console.WriteLine($"📥 מוריד: {latestStoresFile.FileName} ({latestStoresFile.DateFile})");
                    await DownloadAndUploadToAzure(latestStoresFile, "storesfull", datePath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ שגיאה בהורדת StoresFull: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// הורדת PriceFull לכל הסניפים
        /// </summary>
        private static async Task DownloadLatestPriceFullForAllStores(string date, string datePath, List<string> stores)
        {
            try
            {
                var allPriceFiles = await GetFilesByPattern(date, "", new[] { "price", "מחיר" });

                var priceFullFiles = allPriceFiles.Where(f =>
                    f.FileName.ToLower().Contains("pricefull") &&
                    f.FileName.ToLower().Contains("full")
                ).ToList();

                Console.WriteLine($"💰 נמצאו {priceFullFiles.Count} קבצי PriceFull סה\"כ");

                int downloadedCount = 0;
                int totalStores = stores.Count;

                foreach (var store in stores)
                {
                    Console.WriteLine($"🔍 מעבד סניף {store} ({downloadedCount + 1}/{totalStores})");

                    var storeFiles = priceFullFiles
                        .Where(f => f.Store.Equals(store, StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(f => ParseDateTime(f.DateFile))
                        .ToList();

                    var latestPriceFile = storeFiles.FirstOrDefault();

                    if (latestPriceFile != null)
                    {
                        await DownloadAndUploadToAzure(latestPriceFile, "pricefull", datePath);
                        downloadedCount++;
                    }
                    else
                    {
                        Console.WriteLine($"⚠️ לא נמצא PriceFull לסניף: {store}");
                    }

                    // המתנה קצרה כדי לא לעמוס על השרת
                    await Task.Delay(200);
                }

                Console.WriteLine($"✅ הורדו {downloadedCount} קבצי PriceFull מתוך {totalStores} סניפים");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ שגיאה בהורדת PriceFull: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// הורדת PromoFull לכל הסניפים
        /// </summary>
        private static async Task DownloadLatestPromoFullForAllStores(string date, string datePath, List<string> stores)
        {
            try
            {
                var allPromoFiles = await GetFilesByPattern(date, "", new[] { "promo", "מבצע" });

                var promoFullFiles = allPromoFiles.Where(f =>
                    f.FileName.ToLower().Contains("promofull") &&
                    f.FileName.ToLower().Contains("full")
                ).ToList();

                Console.WriteLine($"🎁 נמצאו {promoFullFiles.Count} קבצי PromoFull סה\"כ");

                int downloadedCount = 0;
                int totalStores = stores.Count;

                foreach (var store in stores)
                {
                    Console.WriteLine($"🔍 מעבד סניף {store} ({downloadedCount + 1}/{totalStores})");

                    var storeFiles = promoFullFiles
                        .Where(f => f.Store.Equals(store, StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(f => ParseDateTime(f.DateFile))
                        .ToList();

                    var latestPromoFile = storeFiles.FirstOrDefault();

                    if (latestPromoFile != null)
                    {
                        await DownloadAndUploadToAzure(latestPromoFile, "promofull", datePath);
                        downloadedCount++;
                    }
                    else
                    {
                        Console.WriteLine($"⚠️ לא נמצא PromoFull לסניף: {store}");
                    }

                    await Task.Delay(200);
                }

                Console.WriteLine($"✅ הורדו {downloadedCount} קבצי PromoFull מתוך {totalStores} סניפים");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ שגיאה בהורדת PromoFull: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// הורדה והעלאה ל-Azure עם חילוץ ZIP
        /// </summary>
        private static async Task DownloadAndUploadToAzure(KingStoreFileInfo fileInfo, string fileType, string datePath)
        {
            try
            {
                // שלב 1: קבלת קישור ההורדה
                var metaResponse = await httpClient.PostAsync(
                    $"{BASE_URL}/Download.aspx?FileNm={fileInfo.FileName}",
                    new StringContent(""));

                if (!metaResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine($"❌ שגיאה בקבלת קישור: {metaResponse.StatusCode}");
                    return;
                }

                var metaContent = await metaResponse.Content.ReadAsStringAsync();
                var downloadUrl = ExtractDownloadUrl(metaContent);

                if (string.IsNullOrEmpty(downloadUrl))
                {
                    Console.WriteLine($"⚠️ לא נמצא קישור להורדה: {fileInfo.FileName}");
                    return;
                }

                // שלב 2: הורדת הקובץ
                var fileResponse = await httpClient.GetAsync(downloadUrl);

                if (!fileResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine($"❌ שגיאה בהורדת קובץ: {fileResponse.StatusCode}");
                    return;
                }

                var zipBytes = await fileResponse.Content.ReadAsByteArrayAsync();

                // שלב 3: חילוץ XML מה-ZIP
                var xmlContent = ExtractXmlFromZipBytes(zipBytes);

                if (string.IsNullOrEmpty(xmlContent))
                {
                    Console.WriteLine($"⚠️ לא נמצא XML בקובץ: {fileInfo.FileName}");
                    return;
                }

                // שלב 4: העלאה ל-Azure
                await UploadToAzure(xmlContent, fileInfo, fileType, datePath);

                Console.WriteLine($"☁️ הועלה בהצלחה: {fileInfo.FileName} ({xmlContent.Length:N0} chars)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ שגיאה בעיבוד {fileInfo.FileName}: {ex.Message}");
            }
        }

        /// <summary>
        /// חילוץ XML מ-ZIP bytes
        /// </summary>
        private static string ExtractXmlFromZipBytes(byte[] zipBytes)
        {
            try
            {
                using var zipStream = new MemoryStream(zipBytes);
                using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

                foreach (var entry in archive.Entries)
                {
                    if (entry.Name.ToLower().EndsWith(".xml"))
                    {
                        using var entryStream = entry.Open();
                        using var reader = new StreamReader(entryStream, Encoding.UTF8);
                        return reader.ReadToEnd();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ שגיאה בחילוץ ZIP: {ex.Message}");
            }

            return "";
        }

        /// <summary>
        /// העלאה ל-Azure Storage עם metadata
        /// </summary>
        private static async Task UploadToAzure(string xmlContent, KingStoreFileInfo fileInfo, string fileType, string datePath)
        {
            try
            {
                // יצירת שם ייחודי לקובץ
                string blobName = $"{CHAIN_PREFIX}/{fileType}/{datePath}";

                // אם זה לא storesfull, הוסף את הסניף
                if (fileType != "storesfull")
                    blobName += $"/{fileInfo.Store}";

                blobName += $"/{fileInfo.FileName}_{DateTime.Now:HHmmss}.xml";

                var blobClient = containerClient.GetBlobClient(blobName);

                // העלאה עם overwrite
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xmlContent));
                await blobClient.UploadAsync(stream, overwrite: true);

                // הוספת Metadata
                var metadata = new Dictionary<string, string>
                {
                    ["OriginalFileName"] = fileInfo.FileName,
                    ["Store"] = fileInfo.Store,
                    ["FileType"] = fileType,
                    ["Chain"] = CHAIN_PREFIX,
                    ["UploadDate"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    ["SourceDate"] = fileInfo.DateFile,
                    ["ContentLength"] = xmlContent.Length.ToString()
                };

                await blobClient.SetMetadataAsync(metadata);

                Console.WriteLine($"📄 {blobName} ({xmlContent.Length:N0} chars)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ שגיאה בהעלאה ל-Azure: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// מחיקת נתונים ישנים (שמירת רק היום הנוכחי)
        /// </summary>
        private static async Task CleanupOldData(string currentDatePath)
        {
            try
            {
                Console.WriteLine("🧹 מוחק נתונים ישנים...");

                var deletedCount = 0;
                var currentPrefix = $"{CHAIN_PREFIX}/";

                await foreach (var blob in containerClient.GetBlobsAsync(prefix: currentPrefix))
                {
                    // בדיקה אם הקובץ לא מהיום הנוכחי
                    if (!blob.Name.Contains(currentDatePath))
                    {
                        var blobClient = containerClient.GetBlobClient(blob.Name);
                        await blobClient.DeleteIfExistsAsync();
                        deletedCount++;

                        if (deletedCount % 10 == 0)
                            Console.WriteLine($"🗑️ נמחקו {deletedCount} קבצים ישנים...");
                    }
                }

                Console.WriteLine($"✅ נמחקו {deletedCount} קבצים ישנים, נשמרו רק קבצי {currentDatePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ שגיאה במחיקת נתונים ישנים: {ex.Message}");
                // לא נעצור את התהליך בגלל בעיה במחיקה
            }
        }

        /// <summary>
        /// קבלת כל הסניפים הזמינים
        /// </summary>
        private static async Task<List<string>> GetAllAvailableStores(string date)
        {
            var stores = new HashSet<string>();
            var allFiles = await GetAllFilesForDate(date);

            foreach (var file in allFiles)
            {
                if (!string.IsNullOrEmpty(file.Store) && file.Store.Trim() != "")
                {
                    stores.Add(file.Store.Trim());
                }
            }

            var storesList = stores.OrderBy(s => s).ToList();
            Console.WriteLine($"🏪 נמצאו {storesList.Count} סניפים");

            return storesList;
        }

        /// <summary>
        /// הצגת סטטוס כללי
        /// </summary>
        private static void ShowStatus()
        {
            var now = DateTime.Now;
            var nextRun = now.Date.AddDays(1).Add(SCHEDULE_TIME);

            if (now.TimeOfDay < SCHEDULE_TIME)
                nextRun = now.Date.Add(SCHEDULE_TIME);

            Console.WriteLine($"\r⏰ עכשיו: {now:HH:mm:ss} | הרצה הבאה: {nextRun:dd/MM HH:mm}    ");
        }

        /// <summary>
        /// הצגת סיכום Azure
        /// </summary>
        private static async Task ShowAzureSummary(string datePath)
        {
            try
            {
                var fileTypes = new[] { "storesfull", "pricefull", "promofull" };
                int totalFiles = 0;
                long totalSize = 0;

                foreach (var fileType in fileTypes)
                {
                    int typeCount = 0;
                    long typeSize = 0;

                    var prefix = $"{CHAIN_PREFIX}/{fileType}/{datePath}/";

                    await foreach (var blob in containerClient.GetBlobsAsync(prefix: prefix))
                    {
                        typeCount++;
                        typeSize += blob.Properties.ContentLength ?? 0;
                    }

                    totalFiles += typeCount;
                    totalSize += typeSize;

                    Console.WriteLine($"☁️ {fileType}: {typeCount} קבצים ({typeSize:N0} bytes)");
                }

                Console.WriteLine($"📊 סה\"כ: {totalFiles} קבצים ({totalSize:N0} bytes)");
                Console.WriteLine($"🔗 Container: {CONTAINER_NAME}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ שגיאה בהצגת סיכום: {ex.Message}");
            }
        }

        /// <summary>
        /// רישום שגיאות ל-Azure (לעתיד - התראות)
        /// </summary>
        private static async Task LogError(string errorMessage)
        {
            try
            {
                var logBlobName = $"logs/errors/{DateTime.Now:yyyy/MM/dd}/error_{DateTime.Now:HHmmss}.log";
                var logContent = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {errorMessage}";

                var logBlobClient = containerClient.GetBlobClient(logBlobName);
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(logContent));
                await logBlobClient.UploadAsync(stream, overwrite: true);
            }
            catch
            {
                // אם לא מצליח לרשום ללוג - לא נעצור את התהליך
            }
        }

        // שאר הפונקציות המקוריות (ללא שינוי)
        #region Helper Functions

        private static async Task<List<KingStoreFileInfo>> GetFilesByPattern(string date, string store, string[] searchTerms)
        {
            var allFiles = new List<KingStoreFileInfo>();

            for (int fileType = 0; fileType <= 5; fileType++)
            {
                var files = await GetFilesFromServer(date, store, fileType.ToString());

                if (searchTerms != null && searchTerms.Any())
                {
                    files = files.Where(f =>
                        searchTerms.Any(term =>
                            f.FileName.ToLower().Contains(term.ToLower()) ||
                            f.TypeFile.ToLower().Contains(term.ToLower())
                        )
                    ).ToList();
                }

                allFiles.AddRange(files);
                await Task.Delay(100);
            }

            return allFiles.Distinct().ToList();
        }

        private static async Task<List<KingStoreFileInfo>> GetAllFilesForDate(string date)
        {
            return await GetFilesByPattern(date, "", null);
        }

        private static async Task<List<KingStoreFileInfo>> GetFilesFromServer(string date, string store, string fileType)
        {
            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("WStore", store),
                new KeyValuePair<string, string>("WDate", date),
                new KeyValuePair<string, string>("WFileType", fileType)
            });

            try
            {
                var response = await httpClient.PostAsync($"{BASE_URL}/MainIO_Hok.aspx", formContent);

                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync();
                    return ParseFilesList(jsonContent);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ שגיאה בקבלת קבצים מסוג {fileType}: {ex.Message}");
            }

            return new List<KingStoreFileInfo>();
        }

        private static string ExtractDownloadUrl(string metaContent)
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(metaContent);
                foreach (JsonElement element in doc.RootElement.EnumerateArray())
                {
                    if (element.TryGetProperty("SPath", out JsonElement pathProp))
                    {
                        return pathProp.GetString() ?? "";
                    }
                }
            }
            catch (JsonException)
            {
                if (metaContent.Contains("http"))
                {
                    var lines = metaContent.Split('\n');
                    foreach (var line in lines)
                    {
                        if (line.Contains("http") && (line.Contains(".zip") || line.Contains(".xml")))
                        {
                            return line.Trim().Trim('"');
                        }
                    }
                }
            }

            return "";
        }

        private static DateTime ParseDateTime(string dateStr)
        {
            if (string.IsNullOrEmpty(dateStr))
                return DateTime.MinValue;

            var formats = new[]
            {
                "dd/MM/yyyy HH:mm:ss",
                "dd/MM/yyyy HH:mm",
                "yyyy-MM-dd HH:mm:ss",
                "dd/MM/yyyy",
                "MM/dd/yyyy HH:mm:ss",
                "MM/dd/yyyy HH:mm"
            };

            foreach (var format in formats)
            {
                if (DateTime.TryParseExact(dateStr, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime result))
                {
                    return result;
                }
            }

            if (DateTime.TryParse(dateStr, out DateTime fallbackResult))
            {
                return fallbackResult;
            }

            return DateTime.MinValue;
        }

        private static List<KingStoreFileInfo> ParseFilesList(string jsonContent)
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(jsonContent);
                var files = new List<KingStoreFileInfo>();

                foreach (JsonElement element in doc.RootElement.EnumerateArray())
                {
                    if (element.TryGetProperty("FileNm", out JsonElement fileNmProp))
                    {
                        var fileInfo = new KingStoreFileInfo
                        {
                            FileName = fileNmProp.GetString() ?? "",
                            Company = element.TryGetProperty("Company", out var comp) ? comp.GetString() ?? "" : "",
                            Store = element.TryGetProperty("Store", out var store) ? store.GetString() ?? "" : "",
                            TypeFile = element.TryGetProperty("TypeFile", out var type) ? type.GetString() ?? "" : "",
                            DateFile = element.TryGetProperty("DateFile", out var date) ? date.GetString() ?? "" : ""
                        };

                        files.Add(fileInfo);
                    }
                }

                return files;
            }
            catch (JsonException)
            {
                return new List<KingStoreFileInfo>();
            }
        }

        #endregion

        public class KingStoreFileInfo
        {
            public string FileName { get; set; } = "";
            public string Company { get; set; } = "";
            public string Store { get; set; } = "";
            public string TypeFile { get; set; } = "";
            public string DateFile { get; set; } = "";
        }
    }
}

