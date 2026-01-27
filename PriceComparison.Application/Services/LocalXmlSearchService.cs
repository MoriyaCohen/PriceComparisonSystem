using Microsoft.Extensions.Logging;
using PriceComparison.Application.DTOs;
using System.Collections.Concurrent;
using System.Xml.Linq;

namespace PriceComparison.Application.Services
{
    public class LocalXmlSearchService : ILocalXmlSearchService
    {
        private readonly ILogger<LocalXmlSearchService> _logger;
        private readonly IBarcodeValidationService _barcodeValidationService;
        private readonly string _dataPath;

        // מטמון נפרד עם נעילה
        private readonly ConcurrentDictionary<string, List<LocalProductDto>> _productCache = new();
        private readonly Dictionary<string, ChainStoreInfoDto> _storeInfoCache = new();
        private readonly object _cacheLock = new object();
        private bool _isDataLoaded = false;
        private DateTime _lastLoadTime = DateTime.MinValue;

        public LocalXmlSearchService(
            ILogger<LocalXmlSearchService> logger,
            IBarcodeValidationService barcodeValidationService)
        {
            _logger = logger;
            _barcodeValidationService = barcodeValidationService;

            // נתיב מתוקן לתיקיית LocalXmlData
            _dataPath = Path.Combine(Directory.GetCurrentDirectory(), "LocalXmlData");

            if (!Directory.Exists(_dataPath))
            {
                Directory.CreateDirectory(_dataPath);
                _logger.LogWarning("נוצרה תיקיית נתונים: {DataPath}", _dataPath);
            }
        }

        public async Task<PriceComparisonResponseDto> SearchByBarcodeAsync(string barcode)
        {
            _logger.LogInformation("מחפש ברקוד: {Barcode}", barcode);

            // בדיקת תקינות ברקוד
            var validation = await _barcodeValidationService.ValidateBarcodeAsync(barcode);
            if (!validation.IsValid)
            {
                return new PriceComparisonResponseDto
                {
                    Success = false,
                    ErrorMessage = $"ברקוד לא תקין: {validation.ErrorMessage}"
                };
            }

            // ודא שהנתונים טעונים
            await EnsureDataLoadedAsync();

            if (!_isDataLoaded || _productCache.IsEmpty)
            {
                _logger.LogWarning("אין נתונים זמינים לחיפוש אחרי טעינה");
                return new PriceComparisonResponseDto
                {
                    Success = false,
                    ErrorMessage = "אין נתונים זמינים במערכת"
                };
            }

            var normalizedBarcode = validation.NormalizedBarcode;

            // חיפוש במטמון עם null check
            if (!string.IsNullOrEmpty(normalizedBarcode) &&
                _productCache.TryGetValue(normalizedBarcode, out var products) &&
                products?.Any() == true)
            {
                _logger.LogInformation("נמצאו {Count} מוצרים עבור ברקוד: {Barcode}", products.Count, normalizedBarcode);

                // מיון לפי מחיר (מהזול ליקר) ולקיחת 4 תוצאות
                var sortedProducts = products.OrderBy(p => p.Price).Take(4).ToList();

                // יצירת סטטיסטיקות
                var statistics = new PriceStatisticsDto
                {
                    MinPrice = sortedProducts.Min(p => p.Price),
                    MaxPrice = sortedProducts.Max(p => p.Price),
                    AveragePrice = sortedProducts.Average(p => p.Price),
                    ChainCount = sortedProducts.Select(p => p.ChainName).Distinct().Count(),
                    StoreCount = sortedProducts.Count,
                    TotalResults = sortedProducts.Count
                };

                // המרה ל-ProductPriceInfoDto עם מיפוי מלא של רשתות
                var priceDetails = sortedProducts.Select((product, index) => new ProductPriceInfoDto
                {
                    ProductId = 0,
                    ProductName = product.ProductName,
                    ChainName = product.ChainName,
                    StoreName = product.StoreName,
                    StoreAddress = product.StoreAddress,
                    SubChainName = product.SubChainName,
                    CurrentPrice = product.Price,
                    UnitPrice = product.UnitPrice,
                    UnitOfMeasure = product.UnitOfMeasure,
                    IsWeighted = product.IsWeighted,
                    AllowDiscount = true,
                    LastUpdated = product.PriceUpdateDate,
                    IsMinPrice = index == 0 // רק הראשון (הזול ביותר) מסומן
                }).ToList();

                return new PriceComparisonResponseDto
                {
                    Success = true,
                    ProductInfo = new ProductInfoDto
                    {
                        ProductName = sortedProducts.First().ProductName,
                        Barcode = normalizedBarcode,
                        ManufacturerName = sortedProducts.First().ManufacturerName
                    },
                    Statistics = statistics,
                    PriceDetails = priceDetails
                };
            }

            _logger.LogInformation("מוצר לא נמצא עבור ברקוד: {Barcode}", normalizedBarcode);
            return new PriceComparisonResponseDto
            {
                Success = false,
                ErrorMessage = "מוצר לא נמצא במערכת"
            };
        }

        public async Task<LocalDataStatusDto> GetDataStatusAsync()
        {
            await EnsureDataLoadedAsync();

            var totalProducts = 0;
            var uniqueBarcodes = 0;
            var chains = 0;
            var stores = 0;

            lock (_cacheLock)
            {
                totalProducts = _productCache.Values.SelectMany(p => p).Count();
                uniqueBarcodes = _productCache.Keys.Count;
                chains = _productCache.Values.SelectMany(p => p).Select(p => p.ChainName).Distinct().Count();
                stores = _productCache.Values.SelectMany(p => p).Select(p => $"{p.ChainName}-{p.StoreName}").Distinct().Count();
            }

            var statusMessage = _isDataLoaded && totalProducts > 0
                ? $"המערכת פעילה - נטענו {totalProducts:N0} מוצרים מ-{stores} סניפים ב-{chains} רשתות"
                : "אין נתונים זמינים";

            return new LocalDataStatusDto
            {
                LoadedChains = chains,
                LoadedStores = stores,
                TotalProducts = totalProducts,
                LastRefresh = _lastLoadTime,
                IsDataAvailable = _isDataLoaded && totalProducts > 0,
                StatusMessage = statusMessage
            };
        }

        /// <summary>
        /// רענון נתונים מלא - מחזיר true אם הצליח
        /// </summary>
        public async Task<bool> RefreshDataAsync()
        {
            try
            {
                _logger.LogInformation("מתחיל רענון נתונים ידני");

                lock (_cacheLock)
                {
                    _productCache.Clear();
                    _storeInfoCache.Clear();
                    _isDataLoaded = false;
                }

                await EnsureDataLoadedAsync();

                _logger.LogInformation("רענון נתונים הושלם בהצלחה");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "שגיאה ברענון נתונים");
                return false;
            }
        }

        /// <summary>
        /// ודא שהנתונים טעונים ועדכניים
        /// </summary>
        private async Task EnsureDataLoadedAsync()
        {
            // טען מחדש כל 30 דקות
            if (_isDataLoaded && DateTime.Now.Subtract(_lastLoadTime).TotalMinutes < 30)
                return;

            _logger.LogInformation("מתחיל טעינת נתונים מקומיים...");

            lock (_cacheLock)
            {
                _productCache.Clear();
                _storeInfoCache.Clear();
            }

            // טען קודם נתוני רשתות וסניפים
            await LoadStoreInfoAsync();

            // לאחר מכן טען נתוני מוצרים
            await LoadAllXmlFilesAsync();

            _isDataLoaded = true;
            _lastLoadTime = DateTime.Now;

            _logger.LogInformation("טעינת נתונים הושלמה בהצלחה");
        }

        /// <summary>
        /// טעינת נתוני רשתות וסניפים מקבצי StoresFull
        /// </summary>
        private async Task LoadStoreInfoAsync()
        {
            try
            {
                var storeFullFiles = Directory.GetFiles(_dataPath, "*StoresFull*.xml", SearchOption.TopDirectoryOnly);

                _logger.LogInformation("נמצאו {Count} קבצי StoresFull", storeFullFiles.Length);

                foreach (var file in storeFullFiles)
                {
                    var storeData = LoadStoresFullData(file);
                    lock (_cacheLock)
                    {
                        foreach (var kvp in storeData)
                        {
                            _storeInfoCache[kvp.Key] = kvp.Value;
                        }
                    }
                }

                _logger.LogInformation("נטענו פרטי {Count} סניפים", _storeInfoCache.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "שגיאה בטעינת נתוני רשתות");
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// טעינת נתוני רשתות וסניפים מקובץ StoresFull בודד
        /// </summary>
        private Dictionary<string, ChainStoreInfoDto> LoadStoresFullData(string filePath)
        {
            var storeMapping = new Dictionary<string, ChainStoreInfoDto>();

            try
            {
                var doc = XDocument.Load(filePath);
                var root = doc.Root;

                if (root == null) return storeMapping;

                var chainName = GetElementValue(root, "ChainName") ?? "לא זמין";
                var chainId = GetElementValue(root, "ChainId") ?? "";

                _logger.LogDebug("טוען נתוני רשת: {ChainName} (ID: {ChainId})", chainName, chainId);

                // חיפוש כל הסניפים בתוך SubChains
                var stores = root.Descendants("Store");
                foreach (var store in stores)
                {
                    var storeId = GetElementValue(store, "StoreId");
                    var storeName = GetElementValue(store, "StoreName");
                    var address = GetElementValue(store, "Address");
                    var city = GetElementValue(store, "City");

                    if (!string.IsNullOrEmpty(storeId))
                    {
                        var key = $"{chainId}-{storeId}";
                        var fullAddress = BuildFullAddress(address, city);

                        storeMapping[key] = new ChainStoreInfoDto
                        {
                            ChainId = chainId,
                            ChainName = chainName,
                            StoreId = storeId,
                            StoreName = storeName ?? "לא זמין",
                            Address = fullAddress,
                            SubChainName = GetSubChainName(chainName, storeName)
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "שגיאה בטעינת קובץ StoresFull: {FilePath}", filePath);
            }

            return storeMapping;
        }

        /// <summary>
        /// בניית כתובת מלאה - עם תיקון Null Reference
        /// </summary>
        private string BuildFullAddress(string? address, string? city)
        {
            var parts = new List<string>();

            if (!string.IsNullOrEmpty(address) && address != "unknown" && address != "0")
                parts.Add(address);

            if (!string.IsNullOrEmpty(city))
                parts.Add(city);

            return parts.Count > 0 ? string.Join(", ", parts) : "";
        }

        /// <summary>
        /// זיהוי תת-רשת לפי שם הרשת והסניף - עם תיקון Null Reference
        /// </summary>
        private string GetSubChainName(string? chainName, string? storeName)
        {
            if (string.IsNullOrEmpty(chainName) || string.IsNullOrEmpty(storeName))
                return "";

            // רשת שופרסל - זיהוי BE
            if (chainName.Contains("שופרסל") && storeName.Contains("BE"))
                return "BE";

            // רשת רמי לוי - זיהוי סופר קופיקס
            if (chainName.Contains("רמי לוי") && storeName.ToLower().Contains("קופיקס"))
                return "סופר קופיקס";

            // רשת ויקטורי - זיהוי מחסני השוק וח.כהן
            if (chainName.Contains("ויקטורי"))
            {
                if (storeName.Contains("מחסני"))
                    return "מחסני השוק";
                if (storeName.Contains("כהן"))
                    return "ח.כהן";
            }

            // אלמשהדאוי קינג סטור - זיהוי מיני קינג
            if (chainName.Contains("אלמשהדאוי") && storeName.Contains("מיני"))
                return "מיני קינג סטור";

            return "";
        }

        /// <summary>
        /// טעינת כל קבצי ה-XML (מחירים ומבצעים)
        /// </summary>
        private async Task LoadAllXmlFilesAsync()
        {
            try
            {
                var xmlFiles = Directory.GetFiles(_dataPath, "*.xml", SearchOption.TopDirectoryOnly)
                    .Where(f => !Path.GetFileName(f).Contains("StoresFull"))
                    .ToArray();

                _logger.LogInformation("נמצאו {Count} קבצי נתונים", xmlFiles.Length);

                var tasks = xmlFiles.Select(LoadXmlFileAsync).ToArray();
                await Task.WhenAll(tasks);

                var totalProducts = _productCache.Values.SelectMany(p => p).Count();
                _logger.LogInformation("טעינה הושלמה: {Count} מוצרים מ-{FileCount} קבצים", totalProducts, xmlFiles.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "שגיאה כללית בטעינת קבצים");
            }
        }

        /// <summary>
        /// טעינת קובץ XML בודד - עם מיפוי מלא של רשתות
        /// </summary>
        private async Task LoadXmlFileAsync(string filePath)
        {
            try
            {
                var (products, fileInfo) = await LoadXmlFileDirectlyAsync(filePath);

                if (!fileInfo.LoadedSuccessfully)
                {
                    _logger.LogWarning("נכשל בטעינת קובץ: {FileName} - {Error}",
                        fileInfo.FileName, fileInfo.ErrorMessage);
                    return;
                }

                if (products?.Any() == true)
                {
                    lock (_cacheLock)
                    {
                        foreach (var product in products)
                        {
                            if (!string.IsNullOrEmpty(product.Barcode))
                            {
                                if (!_productCache.ContainsKey(product.Barcode))
                                {
                                    _productCache[product.Barcode] = new List<LocalProductDto>();
                                }
                                _productCache[product.Barcode].Add(product);
                            }
                        }
                    }

                    _logger.LogDebug("נטענו {Count} מוצרים מקובץ: {FileName}",
                        products.Count, fileInfo.FileName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "שגיאה בטעינת קובץ: {FilePath}", filePath);
            }
        }

        /// <summary>
        /// טעינת קובץ XML ישירות עם מיפוי מלא של רשתות וסניפים
        /// </summary>
        private async Task<(List<LocalProductDto>, LoadedFileInfoDto)> LoadXmlFileDirectlyAsync(string filePath)
        {
            var fileInfo = new LoadedFileInfoDto
            {
                FileName = Path.GetFileName(filePath),
                FileDate = File.GetLastWriteTime(filePath),
                LoadedSuccessfully = false
            };

            try
            {
                var doc = XDocument.Load(filePath);
                var root = doc.Root;

                if (root == null)
                {
                    fileInfo.ErrorMessage = "קובץ XML לא תקין - חסר Root element";
                    return (new List<LocalProductDto>(), fileInfo);
                }

                // קריאת נתוני הסניף מה-header של הקובץ
                var chainId = GetElementValue(root, "ChainId") ?? "";
                var storeId = GetElementValue(root, "StoreId") ?? "";
                var subChainId = GetElementValue(root, "SubChainId") ?? "";

                _logger.LogDebug("קורא קובץ: ChainId={ChainId}, StoreId={StoreId}", chainId, storeId);

                // חיפוש פרטי הסניף במטמון StoresFull
                var storeKey = $"{chainId}-{storeId}";
                ChainStoreInfoDto? storeInfo = null;

                lock (_cacheLock)
                {
                    _storeInfoCache.TryGetValue(storeKey, out storeInfo);
                }

                // קביעת נתוני הסניף
                var chainName = storeInfo?.ChainName ?? "לא זמין";
                var storeName = storeInfo?.StoreName ?? $"סניף {storeId}";
                var storeAddress = storeInfo?.Address ?? "";
                var subChainName = storeInfo?.SubChainName ?? "";

                _logger.LogDebug("מידע סניף: {ChainName} - {StoreName}, כתובת: {Address}",
                    chainName, storeName, storeAddress);

                var products = new List<LocalProductDto>();
                var itemElements = root.Descendants("Item").ToArray();

                foreach (var itemElement in itemElements)
                {
                    try
                    {
                        var product = CreateProductFromXml(itemElement, chainName, storeName, storeId);
                        if (product != null && !string.IsNullOrEmpty(product.Barcode))
                        {
                            // הוספת נתוני רשת וסניף מלאים
                            product.StoreAddress = storeAddress;
                            product.SubChainName = subChainName;
                            products.Add(product);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "שגיאה בעיבוד פריט בקובץ {FileName}", fileInfo.FileName);
                    }
                }

                fileInfo.ItemCount = products.Count;
                fileInfo.LoadedSuccessfully = true;
                fileInfo.ChainName = chainName;
                fileInfo.StoreId = storeId;

                _logger.LogDebug("נטענו {Count} מוצרים מקובץ {FileName} (רשת: {ChainName})",
                    products.Count, fileInfo.FileName, chainName);

                await Task.CompletedTask;
                return (products, fileInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "שגיאה בטעינת קובץ XML: {FilePath}", filePath);
                fileInfo.ErrorMessage = ex.Message;
                return (new List<LocalProductDto>(), fileInfo);
            }
        }

        /// <summary>
        /// יצירת מוצר מאלמנט XML עם נתוני רשת מלאים
        /// </summary>
        private LocalProductDto? CreateProductFromXml(XElement itemElement, string chainName, string storeName, string storeId)
        {
            try
            {
                var barcode = GetElementValue(itemElement, "ItemCode");
                var productName = GetElementValue(itemElement, "ItemNm");
                var manufacturerName = GetElementValue(itemElement, "ManufacturerName");
                var priceText = GetElementValue(itemElement, "ItemPrice");
                var unitPriceText = GetElementValue(itemElement, "UnitOfMeasurePrice");

                if (string.IsNullOrEmpty(barcode) || string.IsNullOrEmpty(productName))
                    return null;

                if (!decimal.TryParse(priceText, out decimal price))
                    return null;

                decimal? unitPrice = null;
                if (decimal.TryParse(unitPriceText, out decimal parsedUnitPrice))
                    unitPrice = parsedUnitPrice;

                // פרסור תאריך עדכון
                var priceUpdateDate = DateTime.Now;
                var priceUpdateText = GetElementValue(itemElement, "PriceUpdateDate");
                if (!string.IsNullOrEmpty(priceUpdateText))
                {
                    if (DateTime.TryParse(priceUpdateText, out DateTime parsedDate))
                        priceUpdateDate = parsedDate;
                }

                return new LocalProductDto
                {
                    Barcode = barcode,
                    ProductName = productName,
                    ManufacturerName = manufacturerName ?? "",
                    Price = price,
                    UnitPrice = unitPrice,
                    UnitOfMeasure = GetElementValue(itemElement, "UnitOfMeasure") ?? "",
                    IsWeighted = GetElementValue(itemElement, "bIsWeighted") == "1",
                    ChainName = chainName,
                    StoreName = storeName,
                    StoreId = storeId,
                    StoreAddress = "", // יוגדר מאוחר יותר
                    SubChainName = "", // יוגדר מאוחר יותר
                    PriceUpdateDate = priceUpdateDate
                };
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "שגיאה ביצירת מוצר מ-XML");
                return null;
            }
        }

        /// <summary>
        /// קבלת ערך מאלמנט XML
        /// </summary>
        private string? GetElementValue(XElement parent, string elementName)
        {
            return parent?.Element(elementName)?.Value?.Trim();
        }
    }
}