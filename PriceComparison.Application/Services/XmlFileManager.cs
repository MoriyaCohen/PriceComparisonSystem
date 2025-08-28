using System.Xml.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using PriceComparison.Application.DTOs;

namespace PriceComparison.Application.Services
{
    /// <summary>
    /// מנהל קבצי XML מקומיים
    /// </summary>
    public class XmlFileManager
    {
        private readonly ILogger<XmlFileManager> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _xmlDataPath;

        public XmlFileManager(ILogger<XmlFileManager> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;

            // תיקון: שימוש ב-GetSection במקום GetValue
            _xmlDataPath = _configuration.GetSection("LocalXmlData:FolderPath").Value ??
                          Path.Combine(Directory.GetCurrentDirectory(), "LocalXmlData");
        }

        private readonly List<LoadedFileInfoDto> _loadedFiles = new();
        private readonly Dictionary<string, List<LocalProductDto>> _loadedData = new();

        /// <summary>
        /// קבלת רשימת הקבצים הטעונים
        /// </summary>
        public IReadOnlyList<LoadedFileInfoDto> GetLoadedFiles() => _loadedFiles.AsReadOnly();

        /// <summary>
        /// טעינת כל קבצי XML מהתיקייה
        /// </summary>
        public (Dictionary<string, List<LocalProductDto>> products, LoadedFileInfoDto[] fileInfos) LoadAllXmlFiles()
        {
            _logger.LogInformation("מתחיל טעינת קבצי XML מהתיקייה: {Path}", _xmlDataPath);

            if (!Directory.Exists(_xmlDataPath))
            {
                _logger.LogWarning("תיקיית XML לא קיימת: {Path}", _xmlDataPath);
                return (new Dictionary<string, List<LocalProductDto>>(), Array.Empty<LoadedFileInfoDto>());
            }

            var xmlFiles = Directory.GetFiles(_xmlDataPath, "*.xml", SearchOption.TopDirectoryOnly);
            _logger.LogInformation("נמצאו {Count} קבצי XML בתיקייה", xmlFiles.Length);

            _loadedFiles.Clear();
            _loadedData.Clear();

            var allProducts = new Dictionary<string, List<LocalProductDto>>();

            foreach (var filePath in xmlFiles)
            {
                try
                {
                    var fileName = Path.GetFileName(filePath);
                    _logger.LogDebug("מעבד קובץ: {FileName}", fileName);

                    // סינון לקבצי PriceFull בלבד
                    if (!fileName.Contains("PriceFull", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogDebug("מדלג על קובץ {FileName} - לא PriceFull", fileName);
                        continue;
                    }

                    var (products, fileInfo) = LoadXmlFile(filePath);

                    if (fileInfo.LoadedSuccessfully && products.Count > 0)
                    {
                        _loadedFiles.Add(fileInfo);

                        // איחוד המוצרים למילון הכללי
                        foreach (var product in products)
                        {
                            if (!string.IsNullOrEmpty(product.Barcode))
                            {
                                if (!allProducts.ContainsKey(product.Barcode))
                                {
                                    allProducts[product.Barcode] = new List<LocalProductDto>();
                                }
                                allProducts[product.Barcode].Add(product);
                            }
                        }

                        _logger.LogInformation("נטענו {Count} מוצרים עם ברקוד מקובץ {FileName}",
                            products.Count, fileName);
                    }
                    else
                    {
                        _logger.LogWarning("נכשל בטעינת קובץ {FileName}: {Error}",
                            fileName, fileInfo.ErrorMessage);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "שגיאה בעיבוד קובץ: {FilePath}", filePath);
                }
            }

            _logger.LogInformation("טעינה הושלמה: {ProductCount} ברקודים ייחודיים מ-{FileCount} קבצים",
                allProducts.Count, _loadedFiles.Count);

            return (allProducts, _loadedFiles.ToArray());
        }

        /// <summary>
        /// טעינת קובץ XML יחיד
        /// </summary>
        private (List<LocalProductDto> products, LoadedFileInfoDto fileInfo) LoadXmlFile(string filePath)
        {
            var fileName = Path.GetFileName(filePath);
            var fileInfo = new LoadedFileInfoDto
            {
                FileName = fileName,
                FileDate = File.GetLastWriteTime(filePath),
                LoadedSuccessfully = false
            };

            try
            {
                // ניתוח שם הקובץ להפקת מידע
                var match = Regex.Match(fileName, @"^(.*?)(\d{13})-(\d{3})-(\d{14})-(\d{3})\.xml$", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    fileInfo.FileType = match.Groups[1].Value; // PriceFull, StoreFull, etc.
                    fileInfo.ChainName = $"רשת {match.Groups[2].Value}";
                    fileInfo.StoreId = match.Groups[3].Value;
                }

                // טעינת המסמך
                var doc = XDocument.Load(filePath);
                var root = doc.Root;

                if (root == null)
                {
                    fileInfo.ErrorMessage = "קובץ XML לא תקין - חסר Root element";
                    return (new List<LocalProductDto>(), fileInfo);
                }

                // קבלת פרטי החנות
                var chainName = GetElementValue(root, "ChainName") ?? fileInfo.ChainName;
                var storeId = GetElementValue(root, "StoreId") ?? fileInfo.StoreId;
                var storeName = GetElementValue(root, "StoreName") ?? $"סניף {storeId}";

                // עיבוד הפריטים
                var products = new List<LocalProductDto>();
                var itemElements = root.Descendants("Item").ToArray();

                foreach (var itemElement in itemElements)
                {
                    try
                    {
                        var product = CreateProductFromXml(itemElement, chainName, storeName, storeId);
                        if (product != null && !string.IsNullOrEmpty(product.Barcode))
                        {
                            products.Add(product);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "שגיאה בעיבוד פריט בקובץ {FileName}", fileName);
                    }
                }

                fileInfo.ItemCount = products.Count;
                fileInfo.LoadedSuccessfully = true;

                _logger.LogDebug("נטענו {Count} פריטים מקובץ {FileName}", products.Count, fileName);
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
        /// יצירת מוצר מאלמנט XML
        /// </summary>
        private LocalProductDto? CreateProductFromXml(XElement itemElement, string chainName, string storeName, string storeId)
        {
            try
            {
                var itemCode = GetElementValue(itemElement, "ItemCode");
                var productName = GetElementValue(itemElement, "ItemNm");

                if (string.IsNullOrEmpty(itemCode) || string.IsNullOrEmpty(productName))
                {
                    return null;
                }

                // חיפוש ברקוד
                var barcode = FindBarcode(itemElement);
                if (string.IsNullOrEmpty(barcode))
                {
                    return null; // רק מוצרים עם ברקוד
                }

                return new LocalProductDto
                {
                    Barcode = barcode,
                    ItemCode = itemCode,
                    ProductName = productName,
                    ManufacturerName = GetElementValue(itemElement, "ManufacturerName") ?? "",
                    Price = ParseDecimal(GetElementValue(itemElement, "ItemPrice")),
                    UnitPrice = ParseDecimalNullable(GetElementValue(itemElement, "UnitOfMeasurePrice")),
                    UnitOfMeasure = GetElementValue(itemElement, "UnitOfMeasure") ?? "",
                    IsWeighted = GetElementValue(itemElement, "bIsWeighted") == "1",
                    ChainName = chainName,
                    StoreName = storeName,
                    StoreAddress = "", // לא זמין בקבצי מחירים
                    StoreId = storeId,
                    PriceUpdateDate = ParseDateTime(GetElementValue(itemElement, "PriceUpdateDate"))
                };
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "שגיאה ביצירת מוצר מ-XML");
                return null;
            }
        }

        /// <summary>
        /// חיפוש ברקוד באלמנט
        /// </summary>
        private string? FindBarcode(XElement itemElement)
        {
            // נסיון למצוא ברקוד בשדות שונים
            var possibleBarcodeFields = new[] { "ItemCode", "Barcode", "ItemId" };

            foreach (var fieldName in possibleBarcodeFields)
            {
                var value = GetElementValue(itemElement, fieldName);
                if (!string.IsNullOrEmpty(value) && IsValidBarcode(value))
                {
                    return value;
                }
            }

            return null;
        }

        /// <summary>
        /// בדיקה האם מחרוזת היא ברקוד תקין
        /// </summary>
        private bool IsValidBarcode(string value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   value.Length >= 8 &&
                   value.Length <= 13 &&
                   value.All(char.IsDigit);
        }

        /// <summary>
        /// קבלת ערך אלמנט
        /// </summary>
        private string? GetElementValue(XElement parent, string elementName)
        {
            return parent.Element(elementName)?.Value?.Trim();
        }

        /// <summary>
        /// המרת מחרוזת ל-decimal
        /// </summary>
        private decimal ParseDecimal(string? value)
        {
            return decimal.TryParse(value, out var result) ? result : 0m;
        }

        /// <summary>
        /// המרת מחרוזת ל-decimal nullable
        /// </summary>
        private decimal? ParseDecimalNullable(string? value)
        {
            return decimal.TryParse(value, out var result) ? result : null;
        }

        /// <summary>
        /// המרת מחרוזת לתאריך
        /// </summary>
        private DateTime ParseDateTime(string? value)
        {
            if (string.IsNullOrEmpty(value)) return DateTime.Now;

            // נסיון למצוא פורמט תאריך תקין
            var formats = new[]
            {
                "yyyyMMddHHmm",
                "yyyyMMdd",
                "yyyy-MM-dd HH:mm:ss",
                "yyyy-MM-dd"
            };

            foreach (var format in formats)
            {
                if (DateTime.TryParseExact(value, format, null, System.Globalization.DateTimeStyles.None, out var result))
                {
                    return result;
                }
            }

            return DateTime.Now;
        }
    }
}