using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using PriceComparison.Download.Exceptions;
using PriceComparison.Download.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriceComparison.Download.Services
{
    /// <summary>
    /// שירות לניהול Azure Blob Storage
    /// מטפל בהעלאה, הורדה וארגון קבצים ב-Azure
    /// </summary>
    public class AzureStorageService
    {
        #region Fields & Properties

        /// <summary>
        /// לקוח Blob Storage
        /// </summary>
        private readonly BlobServiceClient _blobServiceClient;

        /// <summary>
        /// לקוח הקונטיינר
        /// </summary>
        private readonly BlobContainerClient _containerClient;

        /// <summary>
        /// הגדרות Azure Storage
        /// </summary>
        private readonly AzureStorageSettings _settings;

        /// <summary>
        /// האם השירות מאותחל
        /// </summary>
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// שם הקונטיינר
        /// </summary>
        public string ContainerName => _settings.ContainerName;

        #endregion

        #region Constructors

        /// <summary>
        /// בנאי עם הגדרות
        /// </summary>
        /// <param name="settings">הגדרות Azure Storage</param>
        public AzureStorageService(AzureStorageSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));

            if (string.IsNullOrEmpty(_settings.ConnectionString))
            {
                throw new ArgumentException("מחרוזת החיבור ל-Azure Storage לא יכולה להיות ריקה");
            }

            try
            {
                _blobServiceClient = new BlobServiceClient(_settings.ConnectionString);
                _containerClient = _blobServiceClient.GetBlobContainerClient(_settings.ContainerName);
            }
            catch (Exception ex)
            {
                throw new ConfigurationException($"שגיאה באתחול Azure Storage: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// בנאי עם מחרוזת חיבור
        /// </summary>
        /// <param name="connectionString">מחרוזת החיבור</param>
        /// <param name="containerName">שם הקונטיינר</param>
        public AzureStorageService(string connectionString, string containerName = "price-comparison-data")
            : this(new AzureStorageSettings
            {
                ConnectionString = connectionString,
                ContainerName = containerName
            })
        {
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// אתחול השירות - יצירת קונטיינר אם לא קיים
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                Console.WriteLine($"🔄 מאתחל Azure Storage - קונטיינר: {_settings.ContainerName}");

                // יצירת קונטיינר אם לא קיים
                await _containerClient.CreateIfNotExistsAsync(PublicAccessType.None);

                // בדיקת גישה
                var exists = await _containerClient.ExistsAsync();
                if (!exists)
                {
                    throw new ConfigurationException($"לא ניתן לגשת לקונטיינר {_settings.ContainerName}");
                }

                IsInitialized = true;
                Console.WriteLine($"✅ Azure Storage מוכן - קונטיינר: {_settings.ContainerName}");
            }
            catch (Exception ex)
            {
                IsInitialized = false;
                throw new ConfigurationException($"שגיאה באתחול Azure Storage: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// העלאת קובץ יחיד
        /// </summary>
        /// <param name="localFilePath">נתיב הקובץ המקומי</param>
        /// <param name="blobName">שם הקובץ ב-Azure</param>
        /// <param name="metadata">מטא-דאטה נוספת</param>
        /// <returns>פרטי ההעלאה</returns>
        public async Task<UploadResult> UploadFileAsync(string localFilePath, string blobName, Dictionary<string, string>? metadata = null)
        {
            ThrowIfNotInitialized();

            if (!File.Exists(localFilePath))
            {
                throw new FileNotFoundException($"קובץ לא נמצא: {localFilePath}");
            }

            var result = new UploadResult
            {
                LocalPath = localFilePath,
                BlobName = blobName,
                StartTime = DateTime.Now
            };

            try
            {
                var blobClient = _containerClient.GetBlobClient(blobName);

                // קריאת הקובץ והעלאה
                using var fileStream = File.OpenRead(localFilePath);
                var uploadResponse = await blobClient.UploadAsync(fileStream, overwrite: true);

                // הוספת מטא-דאטה
                if (metadata != null && metadata.Any())
                {
                    await blobClient.SetMetadataAsync(metadata);
                }

                result.IsSuccess = true;
                result.FileSize = new FileInfo(localFilePath).Length;
                result.BlobUrl = blobClient.Uri.ToString();
                result.ETag = uploadResponse.Value.ETag.ToString();

                Console.WriteLine($"☁️ הועלה: {Path.GetFileName(localFilePath)} → {blobName}");
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"שגיאה בהעלאת {localFilePath}: {ex.Message}";
                Console.WriteLine($"❌ {result.ErrorMessage}");
            }
            finally
            {
                result.EndTime = DateTime.Now;
            }

            return result;
        }

        /// <summary>
        /// העלאת מספר קבצים במקביל
        /// </summary>
        /// <param name="files">רשימת קבצים להעלאה</param>
        /// <param name="maxConcurrency">מספר מקסימלי של העלאות מקביליות</param>
        /// <returns>תוצאות ההעלאות</returns>
        public async Task<List<UploadResult>> UploadMultipleFilesAsync(
            IEnumerable<(string LocalPath, string BlobName)> files,
            int maxConcurrency = 5)
        {
            ThrowIfNotInitialized();

            var filesList = files.ToList();
            if (!filesList.Any())
            {
                return new List<UploadResult>();
            }

            Console.WriteLine($"☁️ מעלה {filesList.Count} קבצים ל-Azure Storage...");

            var semaphore = new SemaphoreSlim(maxConcurrency);
            var uploadTasks = filesList.Select(async file =>
            {
                await semaphore.WaitAsync();
                try
                {
                    return await UploadFileAsync(file.LocalPath, file.BlobName);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var results = await Task.WhenAll(uploadTasks);

            var successCount = results.Count(r => r.IsSuccess);
            Console.WriteLine($"📊 הושלמה העלאה: {successCount}/{filesList.Count} קבצים הועלו בהצלחה");

            return results.ToList();
        }

        /// <summary>
        /// העלאת תוצאות הורדה מרשת ספציפית
        /// </summary>
        /// <param name="downloadResult">תוצאות ההורדה</param>
        /// <param name="preserveStructure">האם לשמור על מבנה התיקיות</param>
        /// <returns>תוצאות ההעלאה</returns>
        public async Task<BatchUploadResult> UploadDownloadResultAsync(DownloadResult downloadResult, bool preserveStructure = true)
        {
            ThrowIfNotInitialized();

            var batchResult = new BatchUploadResult
            {
                ChainName = downloadResult.ChainName,
                StartTime = DateTime.Now
            };

            try
            {
                var filesToUpload = new List<(string LocalPath, string BlobName)>();

                // איסוף כל הקבצים מכל סוגי ההורדות
                CollectFilesFromProcessingResult(downloadResult.StoresFullResult, "StoresFull", filesToUpload, preserveStructure);
                CollectFilesFromProcessingResult(downloadResult.PriceFullResult, "PriceFull", filesToUpload, preserveStructure);
                CollectFilesFromProcessingResult(downloadResult.PromoFullResult, "PromoFull", filesToUpload, preserveStructure);

                if (!filesToUpload.Any())
                {
                    batchResult.ErrorMessage = "לא נמצאו קבצים להעלאה";
                    return batchResult;
                }

                // הוספת מטא-דאטה
                var metadata = new Dictionary<string, string>
                {
                    ["chain_name"] = downloadResult.ChainName,
                    ["download_date"] = downloadResult.StartTime.ToString("yyyy-MM-dd"),
                    ["upload_time"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };

                // העלאה
                Console.WriteLine($"☁️ מעלה {filesToUpload.Count} קבצים עבור {downloadResult.ChainName}");

                var uploadResults = await UploadMultipleFilesAsync(filesToUpload);
                batchResult.UploadResults.AddRange(uploadResults);

                batchResult.IsSuccess = uploadResults.Any(r => r.IsSuccess);
                batchResult.SuccessfulUploads = uploadResults.Count(r => r.IsSuccess);
                batchResult.FailedUploads = uploadResults.Count(r => !r.IsSuccess);
            }
            catch (Exception ex)
            {
                batchResult.IsSuccess = false;
                batchResult.ErrorMessage = $"שגיאה בהעלאה עבור {downloadResult.ChainName}: {ex.Message}";
                Console.WriteLine($"❌ {batchResult.ErrorMessage}");
            }
            finally
            {
                batchResult.EndTime = DateTime.Now;
            }

            return batchResult;
        }

        /// <summary>
        /// הורדת קובץ מ-Azure
        /// </summary>
        /// <param name="blobName">שם הקובץ ב-Azure</param>
        /// <param name="localFilePath">נתיב הקובץ המקומי</param>
        /// <returns>האם ההורדה הצליחה</returns>
        public async Task<bool> DownloadFileAsync(string blobName, string localFilePath)
        {
            ThrowIfNotInitialized();

            try
            {
                var blobClient = _containerClient.GetBlobClient(blobName);

                // יצירת תיקיות אם לא קיימות
                var directory = Path.GetDirectoryName(localFilePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await blobClient.DownloadToAsync(localFilePath);
                Console.WriteLine($"⬇️ הורד: {blobName} → {localFilePath}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ שגיאה בהורדת {blobName}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// רשימת כל הקבצים בקונטיינר
        /// </summary>
        /// <param name="prefix">קידומת לסינון</param>
        /// <returns>רשימת קבצים</returns>
        public async Task<List<BlobFileInfo>> ListFilesAsync(string? prefix = null)
        {
            ThrowIfNotInitialized();

            var files = new List<BlobFileInfo>();

            try
            {
                var blobs = _containerClient.GetBlobsAsync(prefix: prefix);

                await foreach (var blob in blobs)
                {
                    files.Add(new BlobFileInfo
                    {
                        Name = blob.Name,
                        Size = blob.Properties.ContentLength ?? 0,
                        LastModified = blob.Properties.LastModified?.DateTime,
                        ContentType = blob.Properties.ContentType,
                        ETag = blob.Properties.ETag?.ToString()
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ שגיאה ברשימת קבצים: {ex.Message}");
            }

            return files;
        }

        /// <summary>
        /// מחיקת קובץ מ-Azure
        /// </summary>
        /// <param name="blobName">שם הקובץ</param>
        /// <returns>האם המחיקה הצליחה</returns>
        public async Task<bool> DeleteFileAsync(string blobName)
        {
            ThrowIfNotInitialized();

            try
            {
                var blobClient = _containerClient.GetBlobClient(blobName);
                await blobClient.DeleteIfExistsAsync();
                Console.WriteLine($"🗑️ נמחק: {blobName}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ שגיאה במחיקת {blobName}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// ניקוי קבצים ישנים
        /// </summary>
        /// <param name="olderThanDays">מספר ימים</param>
        /// <param name="prefix">קידומת לסינון</param>
        /// <returns>מספר קבצים שנמחקו</returns>
        public async Task<int> CleanupOldFilesAsync(int olderThanDays, string? prefix = null)
        {
            ThrowIfNotInitialized();

            var cutoffDate = DateTime.Now.AddDays(-olderThanDays);
            var deletedCount = 0;

            try
            {
                Console.WriteLine($"🧹 מנקה קבצים ישנים מ-{cutoffDate:yyyy-MM-dd}...");

                var files = await ListFilesAsync(prefix);
                var filesToDelete = files.Where(f => f.LastModified < cutoffDate).ToList();

                foreach (var file in filesToDelete)
                {
                    if (await DeleteFileAsync(file.Name))
                    {
                        deletedCount++;
                    }
                }

                Console.WriteLine($"🧹 נוקו {deletedCount} קבצים ישנים");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ שגיאה בניקוי קבצים: {ex.Message}");
            }

            return deletedCount;
        }

        /// <summary>
        /// קבלת סטטיסטיקות הקונטיינר
        /// </summary>
        /// <returns>סטטיסטיקות</returns>
        public async Task<StorageStatistics> GetStorageStatisticsAsync()
        {
            ThrowIfNotInitialized();

            var stats = new StorageStatistics();

            try
            {
                var files = await ListFilesAsync();

                stats.TotalFiles = files.Count;
                stats.TotalSize = files.Sum(f => f.Size);
                stats.LastModified = files.Max(f => f.LastModified);

                // חלוקה לפי סוגי קבצים
                stats.FilesByType = files
                    .GroupBy(f => Path.GetExtension(f.Name).ToLower())
                    .ToDictionary(g => g.Key, g => g.Count());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ שגיאה בקבלת סטטיסטיקות: {ex.Message}");
            }

            return stats;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// בדיקה שהשירות מאותחל
        /// </summary>
        private void ThrowIfNotInitialized()
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("השירות לא אותחל - קרא ל-InitializeAsync() קודם");
            }
        }

        /// <summary>
        /// איסוף קבצים מתוצאת עיבוד
        /// </summary>
        private void CollectFilesFromProcessingResult(
            ProcessingResult result,
            string category,
            List<(string LocalPath, string BlobName)> filesToUpload,
            bool preserveStructure)
        {
            foreach (var file in result.DownloadedFiles)
            {
                // העלאת קובץ ZIP
                if (File.Exists(file.LocalPath))
                {
                    var blobName = GenerateBlobName(file, category, "zip", preserveStructure);
                    filesToUpload.Add((file.LocalPath, blobName));
                }

                // העלאת קובץ XML מחולץ
                if (!string.IsNullOrEmpty(file.ExtractedPath) && File.Exists(file.ExtractedPath))
                {
                    var blobName = GenerateBlobName(file, category, "xml", preserveStructure);
                    filesToUpload.Add((file.ExtractedPath, blobName));
                }
            }
        }

        /// <summary>
        /// יצירת שם blob
        /// </summary>
        private string GenerateBlobName(DownloadedFileInfo file, string category, string extension, bool preserveStructure)
        {
            var baseName = Path.GetFileNameWithoutExtension(file.OriginalFileName);
            var datePath = DateTime.Now.ToString("yyyy/MM/dd");

            if (preserveStructure)
            {
                return $"{_settings.PathPrefix}/{datePath}/{category}/{baseName}.{extension}".Trim('/');
            }
            else
            {
                return $"{_settings.PathPrefix}/{baseName}_{DateTime.Now:yyyyMMdd_HHmmss}.{extension}".Trim('/');
            }
        }

        #endregion
    }

    #region Support Classes

    /// <summary>
    /// תוצאת העלאה יחידה
    /// </summary>
    public class UploadResult
    {
        public bool IsSuccess { get; set; }
        public string LocalPath { get; set; } = "";
        public string BlobName { get; set; } = "";
        public string? BlobUrl { get; set; }
        public long FileSize { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string? ErrorMessage { get; set; }
        public string? ETag { get; set; }

        public TimeSpan Duration => EndTime?.Subtract(StartTime) ?? TimeSpan.Zero;
    }

    /// <summary>
    /// תוצאת העלאה מרובה
    /// </summary>
    public class BatchUploadResult
    {
        public bool IsSuccess { get; set; }
        public string ChainName { get; set; } = "";
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public List<UploadResult> UploadResults { get; set; } = new();
        public int SuccessfulUploads { get; set; }
        public int FailedUploads { get; set; }
        public string? ErrorMessage { get; set; }

        public TimeSpan Duration => EndTime?.Subtract(StartTime) ?? TimeSpan.Zero;
        public long TotalSizeUploaded => UploadResults.Where(r => r.IsSuccess).Sum(r => r.FileSize);
    }

    /// <summary>
    /// מידע על קובץ ב-blob storage
    /// </summary>
    public class BlobFileInfo
    {
        public string Name { get; set; } = "";
        public long Size { get; set; }
        public DateTime? LastModified { get; set; }
        public string? ContentType { get; set; }
        public string? ETag { get; set; }
    }

    /// <summary>
    /// סטטיסטיקות storage
    /// </summary>
    public class StorageStatistics
    {
        public int TotalFiles { get; set; }
        public long TotalSize { get; set; }
        public DateTime? LastModified { get; set; }
        public Dictionary<string, int> FilesByType { get; set; } = new();

        public string FormattedSize => FormatBytes(TotalSize);

        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }

    #endregion
}
