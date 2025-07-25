using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Text.Json;
using System.IO.Compression;
using PriceComparison.Download.Models;
using PriceComparison.Download.Exceptions;

namespace PriceComparison.Download.Core
{
    /// <summary>
    /// מחלקת בסיס לכל הרשתות המשתמשות בפלטפורמת בינה פרוגקט
    /// מכילה את כל הלוגיקה המשותפת שהועברה מקינג סטור
    /// </summary>
    public abstract class BinaProjectsDownloader : IChainDownloader
    {
        #region Properties & Fields

        /// <summary>
        /// לקוח HTTP למשותף לכל הבקשות
        /// </summary>
        protected static readonly HttpClient httpClient = new HttpClient();

        /// <summary>
        /// נתיב תיקיית ההורדות
        /// </summary>
        protected string DownloadFolder => Path.Combine("DownloadedFiles", ChainPrefix);

        /// <summary>
        /// שם הרשת - יוגדר על ידי המחלקות היורשות
        /// </summary>
        public abstract string ChainName { get; }

        /// <summary>
        /// קוד הרשת (prefix) - יוגדר על ידי המחלקות היורשות
        /// </summary>
        public abstract string ChainPrefix { get; }

        /// <summary>
        /// כתובת בסיס של הרשת - יוגדר על ידי המחלקות היורשות
        /// </summary>
        public abstract string BaseUrl { get; }

        #endregion

        #region Constructor & Initialization

        /// <summary>
        /// בנאי - מגדיר את לקוח ה-HTTP
        /// </summary>
        protected BinaProjectsDownloader()
        {
            SetupHttpClient();
        }

        /// <summary>
        /// הגדרת לקוח ה-HTTP עם כותרות מתאימות
        /// </summary>
        protected virtual void SetupHttpClient()
        {
            if (httpClient.DefaultRequestHeaders.Count == 0)
            {
                httpClient.DefaultRequestHeaders.Add("User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                httpClient.DefaultRequestHeaders.Add("Accept",
                    "application/json, text/javascript, */*; q=0.01");
                httpClient.DefaultRequestHeaders.Add("Accept-Language", "he-IL,he;q=0.9,en;q=0.8");
                httpClient.Timeout = TimeSpan.FromMinutes(10);
            }
        }

        #endregion

        #region Public Interface Implementation

        /// <summary>
        /// הורדת כל הקבצים הנדרשים לתאריך נתון
        /// </summary>
        public virtual async Task<DownloadResult> DownloadAllFilesAsync(DownloadRequest request)
        {
            var result = new DownloadResult
            {
                ChainName = ChainName,
                StartTime = DateTime.Now
            };

            try
            {
                Console.WriteLine($"🏪 מתחיל הורדה מ{ChainName} לתאריך {request.Date}");

                CreateDownloadFolder();

                // שלב 1: הורדת StoresFull
                Console.WriteLine("🏢 מוריד קובץ סניפים...");
                result.StoresFullResult = await DownloadLatestStoresFullAsync(request.Date);

                if (!result.StoresFullResult.IsFullySuccessful)
                {
                    Console.WriteLine("⚠️ בעיה בהורדת קובץ הסניפים - ממשיך עם הסניפים הידועים");
                }

                // קבלת רשימת סניפים
                var storeIds = request.SpecificStores.Any()
                    ? request.SpecificStores
                    : await GetAllAvailableStoresAsync(request.Date);

                Console.WriteLine($"📍 נמצאו {storeIds.Count} סניפים");

                // שלב 2: הורדת PriceFull
                if (request.FileTypes.HasFlag(FileTypeFilter.PriceFull))
                {
                    Console.WriteLine("💰 מוריד קבצי מחירים...");
                    result.PriceFullResult = await DownloadLatestPriceFullForAllStoresAsync(request.Date, storeIds);
                }

                // שלב 3: הורדת PromoFull
                if (request.FileTypes.HasFlag(FileTypeFilter.PromoFull))
                {
                    Console.WriteLine("🎁 מוריד קבצי מבצעים...");
                    result.PromoFullResult = await DownloadLatestPromoFullForAllStoresAsync(request.Date, storeIds);
                }

                result.IsSuccess = true;
                Console.WriteLine($"✅ הושלמה הורדה מ{ChainName}: {result.TotalDownloadedFiles} קבצים");
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"שגיאה כללית בהורדה מ{ChainName}: {ex.Message}";
                Console.WriteLine($"❌ {result.ErrorMessage}");
            }
            finally
            {
                result.EndTime = DateTime.Now;
            }

            return result;
        }

        /// <summary>
        /// הורדת קובץ StoresFull העדכני ביותר
        /// </summary>
        public virtual async Task<ProcessingResult> DownloadLatestStoresFullAsync(string date)
        {
            var result = new ProcessingResult { FileType = "StoresFull" };

            try
            {
                var storesFiles = await GetFilesByPattern(date, "", "1"); // סוג 1 = מחסנים
                var latestStoresFile = storesFiles
                    .Where(f => f.TypeFile.Contains("StoresFull") || f.TypeFile.Contains("מחסנים"))
                    .OrderByDescending(f => f.CreationTime)
                    .FirstOrDefault();

                if (latestStoresFile != null)
                {
                    var downloadResult = await DownloadAndExtractFile(latestStoresFile, "StoresFull");
                    if (downloadResult != null)
                    {
                        result.SuccessfulDownloads = 1;
                        result.DownloadedFiles.Add(downloadResult);
                        result.TotalSize = downloadResult.FileSize;
                    }
                    else
                    {
                        result.FailedDownloads = 1;
                        result.Errors.Add($"כישלון בהורדת {latestStoresFile.FileName}");
                    }
                }
                else
                {
                    result.Errors.Add("לא נמצא קובץ StoresFull לתאריך המבוקש");
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"שגיאה בהורדת StoresFull: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// הורדת קבצי PriceFull לכל הסניפים
        /// </summary>
        public virtual async Task<ProcessingResult> DownloadLatestPriceFullForAllStoresAsync(string date, List<string> storeIds)
        {
            return await DownloadFileTypeForAllStores(date, storeIds, "PriceFull", "4");
        }

        /// <summary>
        /// הורדת קבצי PromoFull לכל הסניפים
        /// </summary>
        public virtual async Task<ProcessingResult> DownloadLatestPromoFullForAllStoresAsync(string date, List<string> storeIds)
        {
            return await DownloadFileTypeForAllStores(date, storeIds, "PromoFull", "5");
        }

        /// <summary>
        /// קבלת רשימת כל הסניפים הזמינים
        /// </summary>
        public virtual async Task<List<string>> GetAllAvailableStoresAsync(string date)
        {
            try
            {
                var allFiles = await GetAllFilesForDate(date);
                var storeIds = allFiles
                    .Where(f => !string.IsNullOrEmpty(f.StoreId) && f.StoreId != "000")
                    .Select(f => f.StoreId)
                    .Distinct()
                    .ToList();

                Console.WriteLine($"📍 נמצאו {storeIds.Count} סניפים עבור {ChainName}");
                return storeIds;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ שגיאה בקבלת רשימת סניפים: {ex.Message}");
                return new List<string>();
            }
        }

        /// <summary>
        /// בדיקת זמינות השירות
        /// </summary>
        public virtual async Task<bool> IsServiceAvailableAsync()
        {
            try
            {
                var response = await httpClient.GetAsync(BaseUrl);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// קבלת סטטיסטיקות הורדה
        /// </summary>
        public virtual async Task<DownloadStatistics> GetDownloadStatisticsAsync(string date)
        {
            var stats = new DownloadStatistics();

            try
            {
                var allFiles = await GetAllFilesForDate(date);

                stats.AvailableStores = allFiles
                    .Where(f => !string.IsNullOrEmpty(f.StoreId) && f.StoreId != "000")
                    .Select(f => f.StoreId)
                    .Distinct()
                    .Count();

                stats.StoresFullCount = allFiles.Count(f => f.TypeFile.Contains("StoresFull"));
                stats.PriceFullCount = allFiles.Count(f => f.TypeFile.Contains("PriceFull"));
                stats.PromoFullCount = allFiles.Count(f => f.TypeFile.Contains("PromoFull"));

                var latestFile = allFiles
                    .Where(f => f.CreationTime.HasValue)
                    .OrderByDescending(f => f.CreationTime)
                    .FirstOrDefault();

                stats.LastUpdateTime = latestFile?.CreationTime;
                stats.EstimatedTotalSize = allFiles.Count * 1024 * 1024; // הערכה גסה
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ שגיאה בקבלת סטטיסטיקות: {ex.Message}");
            }

            return stats;
        }

        #endregion

        #region Protected Helper Methods

        /// <summary>
        /// יצירת תיקיית ההורדות
        /// </summary>
        protected virtual void CreateDownloadFolder()
        {
            if (!Directory.Exists(DownloadFolder))
            {
                Directory.CreateDirectory(DownloadFolder);
                Console.WriteLine($"📁 נוצרה תיקיית הורדות: {DownloadFolder}");
            }
        }

        /// <summary>
        /// הורדת סוג קובץ ספציפי לכל הסניפים
        /// </summary>
        protected virtual async Task<ProcessingResult> DownloadFileTypeForAllStores(
            string date, List<string> storeIds, string fileType, string fileTypeCode)
        {
            var result = new ProcessingResult { FileType = fileType };

            foreach (var storeId in storeIds)
            {
                try
                {
                    var files = await GetFilesByPattern(date, storeId, fileTypeCode);
                    var latestFile = files
                        .Where(f => f.TypeFile.Contains(fileType) && f.StoreId == storeId)
                        .OrderByDescending(f => f.CreationTime)
                        .FirstOrDefault();

                    if (latestFile != null)
                    {
                        var downloadResult = await DownloadAndExtractFile(latestFile, fileType);
                        if (downloadResult != null)
                        {
                            result.SuccessfulDownloads++;
                            result.DownloadedFiles.Add(downloadResult);
                            result.TotalSize += downloadResult.FileSize;
                        }
                        else
                        {
                            result.FailedDownloads++;
                            result.Errors.Add($"כישלון בהורדת {fileType} עבור סניף {storeId}");
                        }
                    }
                    else
                    {
                        result.Errors.Add($"לא נמצא קובץ {fileType} עבור סניף {storeId}");
                    }

                    // המתנה קצרה בין הורדות
                    await Task.Delay(100);
                }
                catch (Exception ex)
                {
                    result.FailedDownloads++;
                    result.Errors.Add($"שגיאה בהורדת {fileType} עבור סניף {storeId}: {ex.Message}");
                }
            }

            Console.WriteLine($"📊 {fileType}: {result.SuccessfulDownloads} הצליחו, {result.FailedDownloads} נכשלו");
            return result;
        }

        /// <summary>
        /// קבלת קבצים לפי pattern
        /// </summary>
        protected virtual async Task<List<Models.FileInfo>> GetFilesByPattern(string date, string store, string fileType)
        {
            var allFiles = new List<Models.FileInfo>();

            // רשימת חיפושים אפשריים
            var searchTerms = new[] { "", "price", "promo", "store", "full" };

            foreach (var term in searchTerms)
            {
                try
                {
                    var files = await GetFilesFromServer(date, store, fileType);

                    if (!string.IsNullOrEmpty(term))
                    {
                        files = files.Where(f =>
                            f.FileName.ToLower().Contains(term.ToLower()) ||
                            f.TypeFile.ToLower().Contains(term.ToLower())
                        ).ToList();
                    }

                    allFiles.AddRange(files);
                    await Task.Delay(100);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ שגיאה בחיפוש קבצים עם תבנית '{term}': {ex.Message}");
                }
            }

            return allFiles.Distinct().ToList();
        }

        /// <summary>
        /// קבלת כל הקבצים לתאריך נתון
        /// </summary>
        protected virtual async Task<List<Models.FileInfo>> GetAllFilesForDate(string date)
        {
            return await GetFilesByPattern(date, "", "0"); // 0 = הכל
        }

        /// <summary>
        /// קבלת קבצים מהשרת
        /// </summary>
        protected virtual async Task<List<Models.FileInfo>> GetFilesFromServer(string date, string store, string fileType)
        {
            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("WStore", store),
                new KeyValuePair<string, string>("WDate", date),
                new KeyValuePair<string, string>("WFileType", fileType)
            });

            try
            {
                var response = await httpClient.PostAsync($"{BaseUrl}/MainIO_Hok.aspx", formContent);

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

            return new List<Models.FileInfo>();
        }

        /// <summary>
        /// פיענוח רשימת הקבצים מ-JSON
        /// </summary>
        protected virtual List<Models.FileInfo> ParseFilesList(string jsonContent)
        {
            var files = new List<Models.FileInfo>();

            try
            {
                using JsonDocument doc = JsonDocument.Parse(jsonContent);
                foreach (JsonElement element in doc.RootElement.EnumerateArray())
                {
                    var fileInfo = new Models.FileInfo
                    {
                        FileName = element.TryGetProperty("FileNm", out var fileName) ?
                            fileName.GetString() ?? "" : "",
                        Company = element.TryGetProperty("Company", out var comp) ?
                            comp.GetString() ?? "" : "",
                        Store = element.TryGetProperty("Store", out var store) ?
                            store.GetString() ?? "" : "",
                        TypeFile = element.TryGetProperty("TypeFile", out var type) ?
                            type.GetString() ?? "" : "",
                        DateFile = element.TryGetProperty("DateFile", out var date) ?
                            date.GetString() ?? "" : ""
                    };

                    files.Add(fileInfo);
                }
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"⚠️ שגיאה בפיענוח JSON: {ex.Message}");
                return new List<Models.FileInfo>();
            }

            return files;
        }

        /// <summary>
        /// הורדה וחילוץ קובץ יחיד
        /// </summary>
        protected virtual async Task<DownloadedFileInfo?> DownloadAndExtractFile(Models.FileInfo fileInfo, string category)
        {
            try
            {
                // קבלת קישור ההורדה
                var downloadUrl = await GetDownloadUrl(fileInfo.FileName);
                if (string.IsNullOrEmpty(downloadUrl))
                {
                    Console.WriteLine($"❌ לא ניתן לקבל קישור הורדה עבור {fileInfo.FileName}");
                    return null;
                }

                // הורדת הקובץ
                var zipPath = Path.Combine(DownloadFolder, category, $"{fileInfo.FileName}.zip");
                Directory.CreateDirectory(Path.GetDirectoryName(zipPath)!);

                var response = await httpClient.GetAsync(downloadUrl);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"❌ כישלון בהורדת {fileInfo.FileName}");
                    return null;
                }

                await using var fileStream = File.Create(zipPath);
                await response.Content.CopyToAsync(fileStream);

                var fileSize = new System.IO.FileInfo(zipPath).Length;
                Console.WriteLine($"✅ הורד: {fileInfo.FileName} ({fileSize:N0} bytes)");

                // חילוץ הקובץ
                var extractedPath = await ExtractZipFile(zipPath, category);

                return new DownloadedFileInfo
                {
                    OriginalFileName = fileInfo.FileName,
                    LocalPath = zipPath,
                    FileSize = fileSize,
                    DownloadTime = DateTime.Now,
                    IsExtracted = !string.IsNullOrEmpty(extractedPath),
                    ExtractedPath = extractedPath,
                    StoreId = fileInfo.StoreId
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ שגיאה בהורדת {fileInfo.FileName}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// קבלת קישור הורדה לקובץ
        /// </summary>
        protected virtual async Task<string> GetDownloadUrl(string fileName)
        {
            try
            {
                var formContent = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("FileNm", fileName)
                });

                var response = await httpClient.PostAsync($"{BaseUrl}/Download.aspx?FileNm={fileName}", formContent);

                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync();
                    return ExtractDownloadUrl(jsonContent);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ שגיאה בקבלת קישור הורדה: {ex.Message}");
            }

            return "";
        }

        /// <summary>
        /// חילוץ קישור ההורדה מהמטא-דאטה
        /// </summary>
        protected virtual string ExtractDownloadUrl(string metaContent)
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
            catch (JsonException ex)
            {
                Console.WriteLine($"⚠️ שגיאה בחילוץ קישור הורדה: {ex.Message}");
            }

            return "";
        }

        /// <summary>
        /// חילוץ קובץ ZIP
        /// </summary>
        protected virtual async Task<string?> ExtractZipFile(string zipPath, string category)
        {
            try
            {
                var extractPath = Path.Combine(DownloadFolder, category, "extracted");
                Directory.CreateDirectory(extractPath);

                using var archive = ZipFile.OpenRead(zipPath);
                foreach (var entry in archive.Entries)
                {
                    if (entry.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                    {
                        var destinationPath = Path.Combine(extractPath, entry.Name);
                        entry.ExtractToFile(destinationPath, true);
                        Console.WriteLine($"📦 חולץ: {entry.Name}");
                        return destinationPath;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ שגיאה בחילוץ {zipPath}: {ex.Message}");
            }

            return null;
        }

        #endregion
    }
}
