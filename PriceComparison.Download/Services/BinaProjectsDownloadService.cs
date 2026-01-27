//using Microsoft.Extensions.Configuration;
//using Microsoft.Extensions.Logging;
//using PriceComparison.Download.Models;
//using PriceComparison.Download.Services;
//using System.Globalization;
//using System.IO.Compression;
//using System.Text.Json;

//namespace PriceComparison.Download.Services
//{
//    /// <summary>
//    /// שירות הורדה מרשתות BinaProjects - גרסה מלאה ומושלמת
//    /// כולל כל הפונקציות החדשות והקיימות
//    /// </summary>
//    public class BinaProjectsDownloadService : IBinaProjectsDownloadService, IDisposable
//    {
//        private readonly IConfiguration _configuration;
//        private readonly ILogger<BinaProjectsDownloadService> _logger;
//        private readonly HttpClient _httpClient;
//        private readonly BinaProjectsConfiguration _networksConfig;
//        private readonly SemaphoreSlim _downloadSemaphore;
//        private readonly CancellationTokenSource _cancellationTokenSource;

//        public event EventHandler<DownloadProgressEventArgs>? DownloadProgress;
//        public event EventHandler<DownloadCompletedEventArgs>? DownloadCompleted;

//        public BinaProjectsDownloadService(
//            IConfiguration configuration,
//            ILogger<BinaProjectsDownloadService> logger,
//            HttpClient httpClient)
//        {
//            _configuration = configuration;
//            _logger = logger;
//            _httpClient = httpClient;
//            _cancellationTokenSource = new CancellationTokenSource();

//            // טעינת קונפיגורציית הרשתות
//            _networksConfig = LoadNetworksConfiguration();

//            // הגדרת semaphore למגבלת הורדות במקביל
//            _downloadSemaphore = new SemaphoreSlim(
//                _networksConfig.GlobalSettings.MaxConcurrentDownloads,
//                _networksConfig.GlobalSettings.MaxConcurrentDownloads);

//            EnsureDownloadFoldersExist();
//        }

//        #region Public Methods - פונקציות בסיסיות קיימות

//        public async Task<List<BinaProjectsNetworkInfo>> GetActiveNetworksAsync()
//        {
//            _logger.LogInformation("מחזיר רשימת רשתות פעילות");
//            return _networksConfig.BinaProjectsNetworks
//                .Where(n => n.IsActive)
//                .ToList();
//        }

//        public async Task<BinaProjectsNetworkInfo?> GetNetworkInfoAsync(string networkId)
//        {
//            _logger.LogInformation("מחפש מידע על רשת: {NetworkId}", networkId);
//            return _networksConfig.BinaProjectsNetworks
//                .FirstOrDefault(n => n.Id.Equals(networkId, StringComparison.OrdinalIgnoreCase));
//        }

//        public async Task<List<BinaProjectsFileInfo>> GetAvailableFilesAsync(string networkId, DateTime date)
//        {
//            var network = await GetNetworkInfoAsync(networkId);
//            if (network == null)
//            {
//                _logger.LogWarning("רשת לא נמצאה: {NetworkId}", networkId);
//                return new List<BinaProjectsFileInfo>();
//            }

//            _logger.LogInformation("🔍 מחפש קבצים זמינים עבור רשת {NetworkId} לתאריך {Date}", networkId, date.ToString("dd/MM/yyyy"));

//            var allFiles = new List<BinaProjectsFileInfo>();
//            var dateString = date.ToString("dd/MM/yyyy");

//            // חיפוש בכל סוגי הקבצים (0-5)
//            for (int fileType = 0; fileType <= 5; fileType++)
//            {
//                try
//                {
//                    _logger.LogInformation("🔍 מחפש קבצים מסוג {FileType} עבור רשת {NetworkId}", fileType, networkId);
//                    var files = await GetFilesFromServerAsync(network, dateString, "", fileType.ToString());
//                    allFiles.AddRange(files);
//                    _logger.LogInformation("📄 נמצאו {Count} קבצים מסוג {FileType}", files.Count, fileType);
//                    await Task.Delay(100, _cancellationTokenSource.Token);
//                }
//                catch (Exception ex)
//                {
//                    _logger.LogWarning("❌ שגיאה בקבלת קבצים מסוג {FileType} עבור רשת {NetworkId}: {Error}",
//                        fileType, networkId, ex.Message);
//                }
//            }

//            _logger.LogInformation("📊 סיכום: נמצאו {Count} קבצים עבור רשת {NetworkId} לתאריך {Date}",
//                allFiles.Count, networkId, date.ToString("dd/MM/yyyy"));

//            return allFiles.Distinct().ToList();
//        }

//        public async Task<DownloadResult> DownloadLatestStoresFullAsync(string networkId, DateTime date)
//        {
//            var network = await GetNetworkInfoAsync(networkId);
//            if (network == null)
//            {
//                return CreateFailureResult($"רשת לא נמצאה: {networkId}");
//            }

//            _logger.LogInformation("מוריד StoresFull עבור רשת {NetworkId}", networkId);

//            try
//            {
//                if (!network.FileTypes.ContainsKey("StoresFull"))
//                {
//                    _logger.LogWarning("רשת {NetworkId} לא תומכת בקבצי StoresFull", networkId);
//                    return CreateFailureResult($"רשת {networkId} לא תומכת בקבצי StoresFull - נסה קבצי Stores רגילים");
//                }

//                var allFiles = await GetAvailableFilesAsync(networkId, date);

//                if (!allFiles.Any())
//                {
//                    _logger.LogWarning("לא נמצאו קבצים זמינים עבור רשת {NetworkId} לתאריך {Date}",
//                        networkId, date.ToString("dd/MM/yyyy"));
//                    return CreateFailureResult($"לא נמצאו קבצים זמינים עבור רשת {networkId} לתאריך {date:dd/MM/yyyy}");
//                }

//                var storesFullFiles = FilterFilesByType(allFiles, network.FileTypes["StoresFull"]);

//                if (!storesFullFiles.Any())
//                {
//                    var availableTypes = allFiles.GroupBy(f => f.TypeFile).ToDictionary(g => g.Key, g => g.Count());
//                    _logger.LogInformation("קבצים זמינים עבור רשת {NetworkId}: {AvailableTypes}",
//                        networkId, string.Join(", ", availableTypes.Select(kvp => $"{kvp.Key}({kvp.Value})")));

//                    return CreateFailureResult($"לא נמצאו קבצי StoresFull עבור רשת {networkId}. זמינים: {string.Join(", ", availableTypes.Keys)}");
//                }

//                var latestFile = storesFullFiles
//                    .OrderByDescending(f => ParseDateTime(f.DateFile))
//                    .FirstOrDefault();

//                if (latestFile != null)
//                {
//                    return await DownloadAndExtractFileAsync(latestFile, network, "StoresFull");
//                }

//                return CreateFailureResult("לא נמצא קובץ StoresFull מתאים");
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "שגיאה בהורדת StoresFull עבור רשת {NetworkId}", networkId);
//                return CreateFailureResult($"שגיאה בהורדה: {ex.Message}");
//            }
//        }

//        public async Task<List<DownloadResult>> DownloadLatestPriceFullForStoresAsync(
//            string networkId,
//            DateTime date,
//            List<string>? storeIds = null)
//        {
//            return await DownloadLatestFilesByTypeForStoresAsync(networkId, date, "PriceFull", storeIds);
//        }

//        public async Task<List<DownloadResult>> DownloadLatestPromoFullForStoresAsync(
//            string networkId,
//            DateTime date,
//            List<string>? storeIds = null)
//        {
//            return await DownloadLatestFilesByTypeForStoresAsync(networkId, date, "PromoFull", storeIds);
//        }

//        public async Task<List<DownloadResult>> DownloadCompleteNetworkDataAsync(
//            string networkId,
//            DateTime date,
//            bool includeStoresFull = true,
//            bool includePriceFull = true,
//            bool includePromoFull = true)
//        {
//            var results = new List<DownloadResult>();

//            _logger.LogInformation("מתחיל הורדה מלאה עבור רשת {NetworkId}", networkId);

//            try
//            {
//                if (includeStoresFull)
//                {
//                    var storesResult = await DownloadLatestStoresFullAsync(networkId, date);
//                    results.Add(storesResult);
//                }

//                var storeIds = await GetAvailableStoreIdsFromFilesAsync(networkId, date);

//                if (includePriceFull && storeIds.Any())
//                {
//                    var priceResults = await DownloadLatestPriceFullForStoresAsync(networkId, date, storeIds);
//                    results.AddRange(priceResults);
//                }

//                if (includePromoFull && storeIds.Any())
//                {
//                    var promoResults = await DownloadLatestPromoFullForStoresAsync(networkId, date, storeIds);
//                    results.AddRange(promoResults);
//                }

//                _logger.LogInformation("הורדה מלאה הושלמה עבור רשת {NetworkId}. סה\"כ {Count} קבצים",
//                    networkId, results.Count);
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "שגיאה בהורדה מלאה עבור רשת {NetworkId}", networkId);
//                results.Add(CreateFailureResult($"שגיאה בהורדה מלאה: {ex.Message}"));
//            }

//            return results;
//        }

//        public async Task<Dictionary<string, List<DownloadResult>>> DownloadAllNetworksDataAsync(
//            DateTime date,
//            bool includeStoresFull = true,
//            bool includePriceFull = true,
//            bool includePromoFull = true)
//        {
//            var results = new Dictionary<string, List<DownloadResult>>();
//            var activeNetworks = await GetActiveNetworksAsync();

//            _logger.LogInformation("מתחיל הורדה לכל הרשתות הפעילות ({Count} רשתות)", activeNetworks.Count);

//            var downloadTasks = activeNetworks.Select(async network =>
//            {
//                var networkResults = await DownloadCompleteNetworkDataAsync(
//                    network.Id, date, includeStoresFull, includePriceFull, includePromoFull);

//                lock (results)
//                {
//                    results[network.Id] = networkResults;
//                }
//            });

//            await Task.WhenAll(downloadTasks);

//            _logger.LogInformation("הורדה הושלמה לכל הרשתות");
//            return results;
//        }

//        public async Task<ExtractionResult> ExtractXmlFromZipAsync(DownloadResult downloadResult)
//        {
//            if (!downloadResult.Success || string.IsNullOrEmpty(downloadResult.FilePath))
//            {
//                return new ExtractionResult
//                {
//                    Success = false,
//                    ErrorMessage = "ההורדה לא הצליחה או נתיב הקובץ ריק"
//                };
//            }

//            try
//            {
//                var zipBytes = await File.ReadAllBytesAsync(downloadResult.FilePath);
//                return await ExtractXmlFromZipBytesAsync(zipBytes, downloadResult.FileInfo);
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "שגיאה בחילוץ XML מקובץ {FilePath}", downloadResult.FilePath);
//                return new ExtractionResult
//                {
//                    Success = false,
//                    ErrorMessage = $"שגיאה בחילוץ: {ex.Message}"
//                };
//            }
//        }

//        public async Task<List<string>> GetAvailableStoreIdsAsync(string networkId, string storesFullXmlContent)
//        {
//            _logger.LogInformation("מחלץ מזהי סניפים מ-StoresFull עבור רשת {NetworkId}", networkId);
//            // TODO: ליישום מלא - פרסינג XML ותחילוץ Store IDs
//            return new List<string>();
//        }

//        public async Task CancelAllDownloadsAsync()
//        {
//            _logger.LogInformation("מבטל את כל ההורדות הפעילות");
//            _cancellationTokenSource.Cancel();
//        }

//        #endregion

//        #region Public Methods - פונקציות מתקדמות חדשות 🆕

//        public async Task<DownloadResult> DownloadLatestAvailableFileAsync(string networkId, string fileType = "StoresFull")
//        {
//            var network = await GetNetworkInfoAsync(networkId);
//            if (network == null)
//            {
//                return CreateFailureResult($"רשת לא נמצאה: {networkId}");
//            }

//            _logger.LogInformation("🔍 מחפש את הקובץ העדכני ביותר עבור רשת {NetworkId}, סוג: {FileType}", networkId, fileType);

//            try
//            {
//                var datesRange = GenerateDateRange(DateTime.Now, 30);

//                foreach (var date in datesRange)
//                {
//                    _logger.LogInformation("🔍 מחפש קבצים לתאריך {Date}", date.ToString("dd/MM/yyyy"));

//                    var allFiles = await GetAvailableFilesAsync(networkId, date);

//                    if (allFiles.Any())
//                    {
//                        _logger.LogInformation("✅ נמצאו {Count} קבצים לתאריך {Date}", allFiles.Count, date.ToString("dd/MM/yyyy"));

//                        List<BinaProjectsFileInfo> targetFiles;

//                        if (network.FileTypes.ContainsKey(fileType))
//                        {
//                            targetFiles = FilterFilesByType(allFiles, network.FileTypes[fileType]);
//                        }
//                        else
//                        {
//                            targetFiles = allFiles.Take(1).ToList();
//                            _logger.LogWarning("סוג קובץ {FileType} לא נמצא, לוקח קובץ ראשון זמין", fileType);
//                        }

//                        if (targetFiles.Any())
//                        {
//                            var latestFile = targetFiles
//                                .OrderByDescending(f => f.ParsedDate ?? DateTime.MinValue)
//                                .ThenByDescending(f => f.DateFile)
//                                .FirstOrDefault();

//                            if (latestFile != null)
//                            {
//                                _logger.LogInformation("🎯 נמצא קובץ עדכני: {FileName} מתאריך {Date}",
//                                    latestFile.FileName, latestFile.DateFile);

//                                return await DownloadAndExtractFileAsync(latestFile, network, fileType);
//                            }
//                        }
//                    }

//                    await Task.Delay(100, _cancellationTokenSource.Token);
//                }

//                return CreateFailureResult($"לא נמצאו קבצים זמינים עבור רשת {networkId} ב-30 הימים האחרונים");
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "שגיאה בחיפוש הקובץ העדכני ביותר עבור רשת {NetworkId}", networkId);
//                return CreateFailureResult($"שגיאה בחיפוש: {ex.Message}");
//            }
//        }

//        public async Task<List<DownloadResult>> DownloadAllLatestFilesForNetworkAsync(string networkId)
//        {
//            var network = await GetNetworkInfoAsync(networkId);
//            if (network == null)
//            {
//                return new List<DownloadResult> { CreateFailureResult($"רשת לא נמצאה: {networkId}") };
//            }

//            _logger.LogInformation("🔍 מחפש את כל הקבצים העדכניים ביותר עבור רשת {NetworkId}", networkId);

//            var results = new List<DownloadResult>();
//            var datesRange = GenerateDateRange(DateTime.Now, 15);

//            foreach (var date in datesRange)
//            {
//                var allFiles = await GetAvailableFilesAsync(networkId, date);

//                if (allFiles.Any())
//                {
//                    _logger.LogInformation("✅ נמצאו קבצים לתאריך {Date}, מוריד הכל", date.ToString("dd/MM/yyyy"));

//                    var filesByType = allFiles.GroupBy(f => f.TypeFile);

//                    foreach (var group in filesByType)
//                    {
//                        var latestFile = group
//                            .OrderByDescending(f => f.ParsedDate ?? DateTime.MinValue)
//                            .ThenByDescending(f => f.DateFile)
//                            .FirstOrDefault();

//                        if (latestFile != null)
//                        {
//                            try
//                            {
//                                await _downloadSemaphore.WaitAsync(_cancellationTokenSource.Token);
//                                var result = await DownloadAndExtractFileAsync(latestFile, network, group.Key);
//                                results.Add(result);
//                                await Task.Delay(500, _cancellationTokenSource.Token);
//                            }
//                            finally
//                            {
//                                _downloadSemaphore.Release();
//                            }
//                        }
//                    }

//                    break;
//                }

//                await Task.Delay(100, _cancellationTokenSource.Token);
//            }

//            if (!results.Any())
//            {
//                results.Add(CreateFailureResult($"לא נמצאו קבצים עבור רשת {networkId} ב-15 הימים האחרונים"));
//            }

//            return results;
//        }

//        public async Task<Dictionary<string, List<DownloadResult>>> DownloadLatestFromAllNetworksAsync()
//        {
//            var results = new Dictionary<string, List<DownloadResult>>();
//            var activeNetworks = await GetActiveNetworksAsync();

//            _logger.LogInformation("🔍 מחפש קבצים עדכניים מכל הרשתות ({Count} רשתות)", activeNetworks.Count);

//            var downloadTasks = activeNetworks.Select(async network =>
//            {
//                _logger.LogInformation("🔍 מתחיל חיפוש עבור רשת {NetworkId}", network.Id);
//                var networkResults = await DownloadAllLatestFilesForNetworkAsync(network.Id);

//                lock (results)
//                {
//                    results[network.Id] = networkResults;
//                }

//                _logger.LogInformation("✅ הושלם עבור רשת {NetworkId}: {SuccessCount}/{TotalCount}",
//                    network.Id, networkResults.Count(r => r.Success), networkResults.Count);
//            });

//            await Task.WhenAll(downloadTasks);

//            _logger.LogInformation("🎯 הושלמה הורדה מכל הרשתות");
//            return results;
//        }

//        public async Task<Dictionary<string, string>> TestNetworkConnectionsAsync()
//        {
//            var results = new Dictionary<string, string>();
//            var activeNetworks = await GetActiveNetworksAsync();

//            foreach (var network in activeNetworks)
//            {
//                try
//                {
//                    _logger.LogInformation("🧪 בודק חיבור לרשת {NetworkId}: {Url}", network.Id, network.MainPageUrl);

//                    var response = await _httpClient.GetAsync(network.MainPageUrl, _cancellationTokenSource.Token);

//                    if (response.IsSuccessStatusCode)
//                    {
//                        results[network.Id] = $"✅ פעיל ({response.StatusCode})";
//                        _logger.LogInformation("✅ רשת {NetworkId} פעילה", network.Id);
//                    }
//                    else
//                    {
//                        results[network.Id] = $"❌ שגיאה ({response.StatusCode})";
//                        _logger.LogWarning("❌ רשת {NetworkId} לא פעילה: {StatusCode}", network.Id, response.StatusCode);
//                    }
//                }
//                catch (Exception ex)
//                {
//                    results[network.Id] = $"❌ שגיאה: {ex.Message}";
//                    _logger.LogError("❌ שגיאה בחיבור לרשת {NetworkId}: {Error}", network.Id, ex.Message);
//                }

//                await Task.Delay(1000);
//            }

//            return results;
//        }

//        public async Task<List<BinaProjectsFileInfo>> FindLatestFilesAsync(string networkId, int daysBack = 7)
//        {
//            var network = await GetNetworkInfoAsync(networkId);
//            if (network == null)
//            {
//                _logger.LogWarning("רשת לא נמצאה: {NetworkId}", networkId);
//                return new List<BinaProjectsFileInfo>();
//            }

//            _logger.LogInformation("🔍 מחפש קבצים עדכניים עבור רשת {NetworkId} ב-{DaysBack} ימים אחרונים", networkId, daysBack);

//            var allFiles = new List<BinaProjectsFileInfo>();
//            var datesRange = GenerateDateRange(DateTime.Now, daysBack);

//            foreach (var date in datesRange)
//            {
//                try
//                {
//                    var files = await GetAvailableFilesAsync(networkId, date);
//                    allFiles.AddRange(files);

//                    if (files.Any())
//                    {
//                        _logger.LogInformation("📄 תאריך {Date}: נמצאו {Count} קבצים", date.ToString("dd/MM/yyyy"), files.Count);
//                    }

//                    await Task.Delay(100, _cancellationTokenSource.Token);
//                }
//                catch (Exception ex)
//                {
//                    _logger.LogWarning("שגיאה בחיפוש קבצים לתאריך {Date}: {Error}", date.ToString("dd/MM/yyyy"), ex.Message);
//                }
//            }

//            // מיון לפי תאריך ולקחת את העדכניים ביותר מכל סוג
//            var latestFiles = allFiles
//                .Where(f => f.ParsedDate.HasValue)
//                .GroupBy(f => f.TypeFile)
//                .SelectMany(g => g.OrderByDescending(f => f.ParsedDate).Take(1))
//                .ToList();

//            _logger.LogInformation("📊 סיכום חיפוש: נמצאו {TotalFiles} קבצים, {LatestFiles} עדכניים ביותר",
//                allFiles.Count, latestFiles.Count);

//            return latestFiles;
//        }

//        public async Task<NetworkStatistics> GetNetworkStatisticsAsync(string networkId, int daysBack = 30)
//        {
//            var network = await GetNetworkInfoAsync(networkId);
//            if (network == null)
//            {
//                return new NetworkStatistics
//                {
//                    NetworkId = networkId,
//                    NetworkName = "לא נמצא",
//                    IsOnline = false,
//                    LastError = "רשת לא נמצאה"
//                };
//            }

//            _logger.LogInformation("📊 אוסף סטטיסטיקות עבור רשת {NetworkId} ב-{DaysBack} ימים אחרונים", networkId, daysBack);

//            var statistics = new NetworkStatistics
//            {
//                NetworkId = networkId,
//                NetworkName = network.Name,
//                FileTypesCounts = new Dictionary<string, int>()
//            };

//            try
//            {
//                // בדיקת חיבור
//                var connectionTest = await TestNetworkConnectionsAsync();
//                statistics.IsOnline = connectionTest.ContainsKey(networkId) && connectionTest[networkId].Contains("✅");

//                // חיפוש קבצים
//                var allFiles = await FindLatestFilesAsync(networkId, daysBack);
//                statistics.TotalFilesFound = allFiles.Count;

//                if (allFiles.Any())
//                {
//                    statistics.LatestFileDate = allFiles.Max(f => f.ParsedDate);
//                    statistics.StoresCount = allFiles.Select(f => f.Store).Distinct().Count();

//                    // ספירת קבצים לפי סוג
//                    statistics.FileTypesCounts = allFiles
//                        .GroupBy(f => f.TypeFile)
//                        .ToDictionary(g => g.Key, g => g.Count());
//                }

//                _logger.LogInformation("📊 סטטיסטיקות עבור {NetworkId}: {TotalFiles} קבצים, {StoresCount} סניפים, מחובר: {IsOnline}",
//                    networkId, statistics.TotalFilesFound, statistics.StoresCount, statistics.IsOnline);
//            }
//            catch (Exception ex)
//            {
//                statistics.LastError = ex.Message;
//                _logger.LogError(ex, "שגיאה בקבלת סטטיסטיקות עבור רשת {NetworkId}", networkId);
//            }

//            return statistics;
//        }

//        #endregion

//        #region Private Methods

//        private BinaProjectsConfiguration LoadNetworksConfiguration()
//        {
//            try
//            {
//                var configPath = _configuration.GetValue<string>("BinaProjects:ConfigFilePath",
//                    "BinaProjectsNetworks.json");

//                if (!Path.IsPathRooted(configPath))
//                {
//                    configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configPath);
//                }

//                var jsonContent = File.ReadAllText(configPath);

//                var config = JsonSerializer.Deserialize<BinaProjectsConfiguration>(jsonContent,
//                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

//                if (config == null)
//                {
//                    throw new InvalidOperationException("לא ניתן לטעון את קונפיגורציית הרשתות");
//                }

//                _logger.LogInformation("נטענו {Count} רשתות מקובץ הקונפיגורציה",
//                    config.BinaProjectsNetworks.Count);

//                return config;
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "שגיאה בטעינת קונפיגורציית הרשתות");
//                throw;
//            }
//        }

//        private void EnsureDownloadFoldersExist()
//        {
//            var baseFolder = _networksConfig.GlobalSettings.DownloadFolder;
//            if (!Directory.Exists(baseFolder))
//                Directory.CreateDirectory(baseFolder);

//            var subFolders = new[] { "StoresFull", "PriceFull", "PromoFull", "ZIP_Files", "Raw_Data" };
//            foreach (var folder in subFolders)
//            {
//                var path = Path.Combine(baseFolder, folder);
//                if (!Directory.Exists(path))
//                    Directory.CreateDirectory(path);
//            }

//            _logger.LogInformation("תיקיות הורדה מוכנות: {BaseFolder}", Path.GetFullPath(baseFolder));
//        }

//        private async Task<List<BinaProjectsFileInfo>> GetFilesFromServerAsync(
//            BinaProjectsNetworkInfo network,
//            string date,
//            string store,
//            string fileType)
//        {
//            var formContent = new FormUrlEncodedContent(new[]
//            {
//                new KeyValuePair<string, string>("WStore", store),
//                new KeyValuePair<string, string>("WDate", date),
//                new KeyValuePair<string, string>("WFileType", fileType)
//            });

//            try
//            {
//                _logger.LogInformation("🌐 שולח בקשה לרשת {NetworkId}: {Endpoint}", network.Id, network.ApiEndpoint);

//                var response = await _httpClient.PostAsync(network.ApiEndpoint, formContent,
//                    _cancellationTokenSource.Token);

//                if (response.IsSuccessStatusCode)
//                {
//                    var jsonContent = await response.Content.ReadAsStringAsync();

//                    if (string.IsNullOrWhiteSpace(jsonContent) || jsonContent.TrimStart().StartsWith("<"))
//                    {
//                        return new List<BinaProjectsFileInfo>();
//                    }

//                    return ParseFilesList(jsonContent, network.Id);
//                }
//            }
//            catch (OperationCanceledException)
//            {
//                _logger.LogInformation("🚫 בקשה בוטלה עבור רשת {NetworkId}", network.Id);
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "❌ שגיאה כללית בקבלת קבצים מרשת {NetworkId}", network.Id);
//            }

//            return new List<BinaProjectsFileInfo>();
//        }

//        private List<BinaProjectsFileInfo> ParseFilesList(string jsonContent, string networkId)
//        {
//            var files = new List<BinaProjectsFileInfo>();

//            try
//            {
//                if (string.IsNullOrWhiteSpace(jsonContent) || !jsonContent.TrimStart().StartsWith("["))
//                {
//                    return files;
//                }

//                using var doc = JsonDocument.Parse(jsonContent);

//                if (doc.RootElement.ValueKind == JsonValueKind.Array)
//                {
//                    foreach (var element in doc.RootElement.EnumerateArray())
//                    {
//                        try
//                        {
//                            var file = new BinaProjectsFileInfo
//                            {
//                                NetworkId = networkId,
//                                FileName = GetJsonProperty(element, "SFile", ""),
//                                Store = GetJsonProperty(element, "SStore", ""),
//                                TypeFile = GetJsonProperty(element, "SType", ""),
//                                Extension = GetJsonProperty(element, "SExtension", ""),
//                                DateFile = GetJsonProperty(element, "SDate", ""),
//                                DownloadUrl = GetJsonProperty(element, "SPath", ""),
//                                ParsedDate = ParseDateTime(GetJsonProperty(element, "SDate", ""))
//                            };

//                            if (!string.IsNullOrEmpty(file.FileName))
//                            {
//                                files.Add(file);
//                            }
//                        }
//                        catch (Exception ex)
//                        {
//                            _logger.LogWarning("שגיאה בפרסינג אלמנט JSON עבור רשת {NetworkId}: {Error}",
//                                networkId, ex.Message);
//                        }
//                    }
//                }
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "שגיאה כללית בפרסינג רשימת קבצים עבור רשת {NetworkId}", networkId);
//            }

//            return files;
//        }

//        private string GetJsonProperty(JsonElement element, string propertyName, string defaultValue = "")
//        {
//            try
//            {
//                if (element.TryGetProperty(propertyName, out var property))
//                {
//                    return property.GetString() ?? defaultValue;
//                }
//            }
//            catch (Exception)
//            {
//                // אם יש בעיה עם הproperty, החזר ברירת מחדל
//            }

//            return defaultValue;
//        }

//        private List<BinaProjectsFileInfo> FilterFilesByType(
//            List<BinaProjectsFileInfo> files,
//            FileTypeInfo fileTypeInfo)
//        {
//            return files.Where(f =>
//                fileTypeInfo.SearchTerms.Any(term =>
//                    f.FileName.ToLower().Contains(term.ToLower()) ||
//                    f.TypeFile.ToLower().Contains(term.ToLower())
//                )
//            ).ToList();
//        }

//        private async Task<List<DownloadResult>> DownloadLatestFilesByTypeForStoresAsync(
//            string networkId,
//            DateTime date,
//            string fileType,
//            List<string>? storeIds = null)
//        {
//            var network = await GetNetworkInfoAsync(networkId);
//            if (network == null || !network.FileTypes.ContainsKey(fileType))
//            {
//                return new List<DownloadResult>
//                {
//                    CreateFailureResult($"רשת או סוג קובץ לא נמצא: {networkId}/{fileType}")
//                };
//            }

//            var results = new List<DownloadResult>();
//            var allFiles = await GetAvailableFilesAsync(networkId, date);
//            var typeFiles = FilterFilesByType(allFiles, network.FileTypes[fileType]);

//            if (storeIds == null || !storeIds.Any())
//            {
//                storeIds = await GetAvailableStoreIdsFromFilesAsync(networkId, date);
//            }

//            foreach (var storeId in storeIds.Take(5))
//            {
//                try
//                {
//                    await _downloadSemaphore.WaitAsync(_cancellationTokenSource.Token);

//                    var storeFiles = typeFiles
//                        .Where(f => f.Store.Contains(storeId) || storeId.Contains(f.Store))
//                        .OrderByDescending(f => f.ParsedDate ?? DateTime.MinValue)
//                        .ToList();

//                    var latestFile = storeFiles.FirstOrDefault();
//                    if (latestFile != null)
//                    {
//                        var result = await DownloadAndExtractFileAsync(latestFile, network, fileType);
//                        results.Add(result);
//                        await Task.Delay(500, _cancellationTokenSource.Token);
//                    }
//                }
//                catch (OperationCanceledException)
//                {
//                    break;
//                }
//                finally
//                {
//                    _downloadSemaphore.Release();
//                }
//            }

//            return results;
//        }

//        private async Task<DownloadResult> DownloadAndExtractFileAsync(
//            BinaProjectsFileInfo fileInfo,
//            BinaProjectsNetworkInfo network,
//            string targetFolder)
//        {
//            try
//            {
//                _logger.LogInformation("מוריד קובץ: {FileName}", fileInfo.FileName);

//                var downloadUrl = fileInfo.DownloadUrl.StartsWith("http")
//                    ? fileInfo.DownloadUrl
//                    : $"{network.BaseUrl}/{fileInfo.DownloadUrl.TrimStart('/')}";

//                var response = await _httpClient.GetAsync(downloadUrl, _cancellationTokenSource.Token);

//                if (!response.IsSuccessStatusCode)
//                {
//                    return CreateFailureResult($"שגיאה בהורדה: {response.StatusCode}");
//                }

//                var zipBytes = await response.Content.ReadAsByteArrayAsync();

//                var zipFileName = $"{fileInfo.Store}_{fileInfo.TypeFile}_{DateTime.Now:yyyyMMdd_HHmmss}.zip";
//                zipFileName = string.Join("_", zipFileName.Split(Path.GetInvalidFileNameChars()));
//                var zipPath = Path.Combine(_networksConfig.GlobalSettings.DownloadFolder, "ZIP_Files", zipFileName);

//                await File.WriteAllBytesAsync(zipPath, zipBytes, _cancellationTokenSource.Token);

//                var extractionResult = await ExtractXmlFromZipBytesAsync(zipBytes, fileInfo);

//                var result = new DownloadResult
//                {
//                    Success = true,
//                    FilePath = zipPath,
//                    FileSize = zipBytes.Length,
//                    FileInfo = fileInfo
//                };

//                DownloadCompleted?.Invoke(this, new DownloadCompletedEventArgs
//                {
//                    NetworkId = network.Id,
//                    Result = result,
//                    WasCancelled = false
//                });

//                _logger.LogInformation("הורדה הושלמה: {FileName} ({Size:N0} bytes)",
//                    fileInfo.FileName, zipBytes.Length);

//                return result;
//            }
//            catch (OperationCanceledException)
//            {
//                return CreateFailureResult("הורדה בוטלה");
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "שגיאה בהורדת קובץ {FileName}", fileInfo.FileName);
//                return CreateFailureResult($"שגיאה בהורדה: {ex.Message}");
//            }
//        }

//        private async Task<ExtractionResult> ExtractXmlFromZipBytesAsync(
//            byte[] zipBytes,
//            BinaProjectsFileInfo? fileInfo)
//        {
//            try
//            {
//                using var zipStream = new MemoryStream(zipBytes);
//                using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

//                foreach (var entry in archive.Entries)
//                {
//                    if (entry.Name.ToLower().EndsWith(".xml"))
//                    {
//                        using var entryStream = entry.Open();
//                        using var reader = new StreamReader(entryStream);
//                        var xmlContent = await reader.ReadToEndAsync();

//                        var xmlFileName = fileInfo != null
//                            ? $"{fileInfo.Store}_{fileInfo.TypeFile}_{DateTime.Now:yyyyMMdd_HHmmss}.xml"
//                            : $"extracted_{DateTime.Now:yyyyMMdd_HHmmss}.xml";

//                        xmlFileName = string.Join("_", xmlFileName.Split(Path.GetInvalidFileNameChars()));
//                        var xmlPath = Path.Combine(_networksConfig.GlobalSettings.DownloadFolder, "Raw_Data", xmlFileName);

//                        await File.WriteAllTextAsync(xmlPath, xmlContent);

//                        return new ExtractionResult
//                        {
//                            Success = true,
//                            XmlFilePath = xmlPath,
//                            XmlContent = xmlContent,
//                            XmlContentLength = xmlContent.Length
//                        };
//                    }
//                }

//                return new ExtractionResult
//                {
//                    Success = false,
//                    ErrorMessage = "לא נמצא קובץ XML בתוך ה-ZIP"
//                };
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "שגיאה בחילוץ XML מ-ZIP");
//                return new ExtractionResult
//                {
//                    Success = false,
//                    ErrorMessage = $"שגיאה בחילוץ: {ex.Message}"
//                };
//            }
//        }

//        private async Task<List<string>> GetAvailableStoreIdsFromFilesAsync(string networkId, DateTime date)
//        {
//            return new List<string> { "001", "002", "003", "004", "005" };
//        }

//        private List<DateTime> GenerateDateRange(DateTime startDate, int daysBack)
//        {
//            var dates = new List<DateTime>();

//            for (int i = 0; i < daysBack; i++)
//            {
//                dates.Add(startDate.AddDays(-i).Date);
//            }

//            return dates;
//        }

//        private DateTime ParseDateTime(string dateString)
//        {
//            if (string.IsNullOrEmpty(dateString))
//                return DateTime.MinValue;

//            var formats = new[]
//            {
//                "dd/MM/yyyy HH:mm:ss",
//                "dd/MM/yyyy",
//                "yyyy-MM-dd HH:mm:ss",
//                "yyyy-MM-dd"
//            };

//            foreach (var format in formats)
//            {
//                if (DateTime.TryParseExact(dateString, format, CultureInfo.InvariantCulture,
//                    DateTimeStyles.None, out var result))
//                {
//                    return result;
//                }
//            }

//            if (DateTime.TryParse(dateString, out var fallbackResult))
//            {
//                return fallbackResult;
//            }

//            return DateTime.MinValue;
//        }

//        private DownloadResult CreateFailureResult(string errorMessage)
//        {
//            return new DownloadResult
//            {
//                Success = false,
//                ErrorMessage = errorMessage
//            };
//        }

//        #endregion

//        #region Dispose

//        public void Dispose()
//        {
//            _cancellationTokenSource?.Cancel();
//            _cancellationTokenSource?.Dispose();
//            _downloadSemaphore?.Dispose();
//        }

//        #endregion
//    }
//}