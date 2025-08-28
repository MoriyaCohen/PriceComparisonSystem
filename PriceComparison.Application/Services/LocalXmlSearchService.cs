using Microsoft.Extensions.Logging;
using PriceComparison.Application.DTOs;
using PriceComparison.Application.Services;
using System.Collections.Concurrent;

namespace PriceComparison.Application.Services;

public class LocalXmlSearchService : ILocalXmlSearchService
{
    private readonly ILogger<LocalXmlSearchService> _logger;
    private readonly IBarcodeValidationService _barcodeValidationService;
    private readonly XmlFileManager _xmlFileManager;

    // 🔧 מטמון נפרד עם נעילה
    private readonly ConcurrentDictionary<string, List<LocalProductDto>> _productCache = new();
    private readonly object _cacheLock = new object();
    private bool _isDataLoaded = false;
    private DateTime _lastLoadTime = DateTime.MinValue;

    public LocalXmlSearchService(
        ILogger<LocalXmlSearchService> logger,
        IBarcodeValidationService barcodeValidationService,
        XmlFileManager xmlFileManager)
    {
        _logger = logger;
        _barcodeValidationService = barcodeValidationService;
        _xmlFileManager = xmlFileManager;
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

            // מיון לפי מחיר (מהזול ליקר)
            var sortedProducts = products.OrderBy(p => p.Price).ToList();

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

            // המרה ל-ProductPriceInfoDto (מתאים למבנה הקיים שלך)
            var priceDetails = sortedProducts.Select((product, index) => new ProductPriceInfoDto
            {
                ProductId = 0, // ברירת מחדל אם אין ProductId ב-LocalProductDto
                ProductName = product.ProductName,
                ChainName = product.ChainName,
                StoreName = product.StoreName,
                StoreAddress = product.StoreAddress,
                CurrentPrice = product.Price,
                UnitPrice = product.UnitPrice,
                UnitOfMeasure = product.UnitOfMeasure,
                IsWeighted = product.IsWeighted,
                AllowDiscount = true, // ברירת מחדל אם אין AllowDiscount ב-LocalProductDto
                LastUpdated = DateTime.Now, // ברירת מחדל אם אין LastUpdated ב-LocalProductDto
                IsMinPrice = index == 0 // הזול ביותר
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
            ? $"נתונים זמינים - {uniqueBarcodes} ברקודים ייחודיים מ-{chains} רשתות"
            : "אין נתונים זמינים";

        _logger.LogInformation("מצב נתונים מקומיים: {IsDataAvailable}, {TotalProducts} מוצרים, {Chains} רשתות",
            _isDataLoaded && totalProducts > 0, totalProducts, chains);

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

    public async Task<bool> RefreshDataAsync()
    {
        _logger.LogInformation("מתחיל טעינה מחדש של נתוני XML");

        lock (_cacheLock)
        {
            _productCache.Clear();
            _isDataLoaded = false;
        }

        return LoadDataAsync();
    }

    // 🔧 מתודה פרטית לטעינת נתונים - מתוקנת למבנה הקיים שלך
    private bool LoadDataAsync()
    {
        try
        {
            _logger.LogInformation("מתחיל טעינת נתונים מ-XmlFileManager");

            // 🔧 שינוי: LoadAllXmlFiles מחזיר tuple, לא Dictionary
            var xmlDataResult = _xmlFileManager.LoadAllXmlFiles();

            if (xmlDataResult.products == null || !xmlDataResult.products.Any())
            {
                _logger.LogWarning("לא נטענו נתונים מקבצי XML");
                return false;
            }

            lock (_cacheLock)
            {
                // ריקון מטמון קיים
                _productCache.Clear();

                // העתקת נתונים למטמון
                foreach (var kvp in xmlDataResult.products)
                {
                    if (!string.IsNullOrEmpty(kvp.Key) && kvp.Value?.Any() == true)
                    {
                        _productCache.TryAdd(kvp.Key, kvp.Value);
                    }
                }

                _isDataLoaded = _productCache.Count > 0;
                _lastLoadTime = DateTime.UtcNow;
            }

            var totalProducts = _productCache.Values.SelectMany(p => p).Count();
            var uniqueBarcodes = _productCache.Keys.Count;
            var chains = _productCache.Values.SelectMany(p => p).Select(p => p.ChainName).Distinct().Count();
            var stores = _productCache.Values.SelectMany(p => p).Select(p => $"{p.ChainName}-{p.StoreName}").Distinct().Count();

            _logger.LogInformation("טעינה הושלמה: {TotalProducts} מוצרים, {UniqueBarcodes} ברקודים ייחודיים, {Chains} רשתות, {Stores} סניפים",
                totalProducts, uniqueBarcodes, chains, stores);

            return _isDataLoaded;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "שגיאה בטעינת נתונים מקומיים");
            return false;
        }
    }

    // 🔧 מתודה לוודא שהנתונים טעונים
    private Task EnsureDataLoadedAsync()
    {
        if (!_isDataLoaded || _productCache.IsEmpty)
        {
            _logger.LogWarning("אין נתונים זמינים לחיפוש. מנסה לטעון...");
            LoadDataAsync();
        }

        return Task.CompletedTask;
    }

    public Task<PriceStatisticsDto> GetStatisticsAsync(string barcode)
    {
        return Task.Run(async () =>
        {
            var validation = await _barcodeValidationService.ValidateBarcodeAsync(barcode);
            if (!validation.IsValid)
            {
                return new PriceStatisticsDto
                {
                    MinPrice = 0,
                    MaxPrice = 0,
                    AveragePrice = 0,
                    ChainCount = 0,
                    StoreCount = 0,
                    TotalResults = 0
                };
            }

            await EnsureDataLoadedAsync();

            var normalizedBarcode = validation.NormalizedBarcode;

            if (!string.IsNullOrEmpty(normalizedBarcode) &&
                _productCache.TryGetValue(normalizedBarcode, out var products) &&
                products?.Any() == true)
            {
                return new PriceStatisticsDto
                {
                    MinPrice = products.Min(p => p.Price),
                    MaxPrice = products.Max(p => p.Price),
                    AveragePrice = products.Average(p => p.Price),
                    ChainCount = products.Select(p => p.ChainName).Distinct().Count(),
                    StoreCount = products.Count,
                    TotalResults = products.Count
                };
            }

            return new PriceStatisticsDto
            {
                MinPrice = 0,
                MaxPrice = 0,
                AveragePrice = 0,
                ChainCount = 0,
                StoreCount = 0,
                TotalResults = 0
            };
        });
    }
}