using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO.Compression;
using System.Xml;
using PriceComparison.Download.Models;
using PriceComparison.Download.Exceptions;

namespace PriceComparison.Download.Services
{
    /// <summary>
    /// שירות לעיבוד ואימות קבצים שהורדו
    /// מטפל בחילוץ, אימות תקינות וניתוח תוכן XML
    /// </summary>
    public class FileProcessingService
    {
        #region Fields & Properties

        /// <summary>
        /// הגדרות עיבוד קבצים
        /// </summary>
        private readonly FileProcessingSettings _settings;

        /// <summary>
        /// מטמון תוצאות אימות
        /// </summary>
        private readonly Dictionary<string, ValidationResult> _validationCache;

        /// <summary>
        /// מנעול לגישה thread-safe למטמון
        /// </summary>
        private readonly object _cacheLock = new object();

        #endregion

        #region Constructors

        /// <summary>
        /// בנאי עם הגדרות
        /// </summary>
        /// <param name="settings">הגדרות עיבוד קבצים</param>
        public FileProcessingService(FileProcessingSettings? settings = null)
        {
            _settings = settings ?? FileProcessingSettings.Default();
            _validationCache = new Dictionary<string, ValidationResult>();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// עיבוד תוצאות הורדה מלאות
        /// </summary>
        /// <param name="downloadResult">תוצאות ההורדה</param>
        /// <returns>תוצאות העיבוד</returns>
        public async Task<ProcessingResult> ProcessDownloadResultAsync(DownloadResult downloadResult)
        {
            var result = new ProcessingResult
            {
                ChainName = downloadResult.ChainName,
                StartTime = DateTime.Now
            };

            try
            {
                Console.WriteLine($"⚙️ מעבד קבצים עבור {downloadResult.ChainName}...");

                // עיבוד קבצי StoresFull
                if (downloadResult.StoresFullResult.DownloadedFiles.Any())
                {
                    var storesProcessing = await ProcessFileCategory(downloadResult.StoresFullResult, "StoresFull");
                    result.CategoriesProcessed.Add("StoresFull", storesProcessing);
                }

                // עיבוד קבצי PriceFull
                if (downloadResult.PriceFullResult.DownloadedFiles.Any())
                {
                    var priceProcessing = await ProcessFileCategory(downloadResult.PriceFullResult, "PriceFull");
                    result.CategoriesProcessed.Add("PriceFull", priceProcessing);
                }

                // עיבוד קבצי PromoFull
                if (downloadResult.PromoFullResult.DownloadedFiles.Any())
                {
                    var promoProcessing = await ProcessFileCategory(downloadResult.PromoFullResult, "PromoFull");
                    result.CategoriesProcessed.Add("PromoFull", promoProcessing);
                }

                // חישוב סיכום
                result.TotalFilesProcessed = result.CategoriesProcessed.Values.Sum(c => c.FilesProcessed);
                result.TotalValidFiles = result.CategoriesProcessed.Values.Sum(c => c.ValidFiles);
                result.TotalInvalidFiles = result.CategoriesProcessed.Values.Sum(c => c.InvalidFiles);
                result.IsSuccess = result.TotalValidFiles > 0;

                // יצירת דוח מפורט
                if (_settings.GenerateDetailedReport)
                {
                    await GenerateProcessingReportAsync(result);
                }

                Console.WriteLine($"✅ עיבוד הושלם עבור {downloadResult.ChainName}: {result.TotalValidFiles} קבצים תקינים");
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"שגיאה בעיבוד עבור {downloadResult.ChainName}: {ex.Message}";
                Console.WriteLine($"❌ {result.ErrorMessage}");
            }
            finally
            {
                result.EndTime = DateTime.Now;
            }

            return result;
        }

        /// <summary>
        /// חילוץ וטיפול בקובץ ZIP יחיד
        /// </summary>
        /// <param name="zipFilePath">נתיב קובץ ה-ZIP</param>
        /// <param name="extractToPath">תיקיית יעד לחילוץ</param>
        /// <returns>תוצאת החילוץ</returns>
        public async Task<ExtractionResult> ExtractZipFileAsync(string zipFilePath, string? extractToPath = null)
        {
            var result = new ExtractionResult
            {
                ZipFilePath = zipFilePath,
                StartTime = DateTime.Now
            };

            try
            {
                if (!File.Exists(zipFilePath))
                {
                    throw new FileNotFoundException($"קובץ ZIP לא נמצא: {zipFilePath}");
                }

                // יצירת תיקיית יעד
                extractToPath ??= Path.Combine(Path.GetDirectoryName(zipFilePath)!, "extracted");
                Directory.CreateDirectory(extractToPath);
                result.ExtractedPath = extractToPath;

                // חילוץ הקובץ
                using var archive = ZipFile.OpenRead(zipFilePath);

                foreach (var entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue;

                    var destinationPath = Path.Combine(extractToPath, entry.Name);

                    // הגנה מפני path traversal
                    if (!destinationPath.StartsWith(extractToPath))
                    {
                        Console.WriteLine($"⚠️ דילג על קובץ חשוד: {entry.Name}");
                        continue;
                    }

                    // חילוץ הקובץ
                    entry.ExtractToFile(destinationPath, overwrite: true);
                    result.ExtractedFiles.Add(new ExtractedFileInfo
                    {
                        OriginalName = entry.Name,
                        ExtractedPath = destinationPath,
                        Size = entry.Length,
                        CompressedSize = entry.CompressedLength
                    });
                }

                result.IsSuccess = result.ExtractedFiles.Any();
                result.TotalExtracted = result.ExtractedFiles.Count;

                if (_settings.ValidateAfterExtraction)
                {
                    await ValidateExtractedFilesAsync(result);
                }

                Console.WriteLine($"📦 חולצו {result.TotalExtracted} קבצים מ-{Path.GetFileName(zipFilePath)}");
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"שגיאה בחילוץ {zipFilePath}: {ex.Message}";
                Console.WriteLine($"❌ {result.ErrorMessage}");
            }
            finally
            {
                result.EndTime = DateTime.Now;
            }

            return result;
        }

        /// <summary>
        /// אימות תקינות קובץ XML
        /// </summary>
        /// <param name="xmlFilePath">נתיב קובץ ה-XML</param>
        /// <returns>תוצאת האימות</returns>
        public async Task<ValidationResult> ValidateXmlFileAsync(string xmlFilePath)
        {
            // בדיקה במטמון
            var cacheKey = $"{xmlFilePath}:{new FileInfo(xmlFilePath).LastWriteTime:yyyyMMddHHmmss}";
            lock (_cacheLock)
            {
                if (_validationCache.ContainsKey(cacheKey))
                {
                    return _validationCache[cacheKey];
                }
            }

            var result = new ValidationResult
            {
                FilePath = xmlFilePath,
                StartTime = DateTime.Now
            };

            try
            {
                if (!File.Exists(xmlFilePath))
                {
                    throw new FileNotFoundException($"קובץ XML לא נמצא: {xmlFilePath}");
                }

                // בדיקה בסיסית - גודל קובץ
                var fileInfo = new FileInfo(xmlFilePath);
                result.FileSize = fileInfo.Length;

                if (fileInfo.Length == 0)
                {
                    result.Errors.Add("קובץ ריק");
                    result.IsValid = false;
                    return result;
                }

                if (fileInfo.Length > _settings.MaxFileSizeBytes)
                {
                    result.Warnings.Add($"קובץ גדול מהמקסימום המותר ({_settings.MaxFileSizeBytes:N0} bytes)");
                }

                // בדיקת תקינות XML
                await ValidateXmlStructureAsync(result);

                // בדיקת תוכן ספציפי לפי סוג הקובץ
                if (result.IsValid)
                {
                    await ValidateBusinessLogicAsync(result);
                }

                // שמירה במטמון
                lock (_cacheLock)
                {
                    _validationCache[cacheKey] = result;
                }

                if (result.IsValid)
                {
                    Console.WriteLine($"✅ {Path.GetFileName(xmlFilePath)} תקין");
                }
                else
                {
                    Console.WriteLine($"❌ {Path.GetFileName(xmlFilePath)} לא תקין: {string.Join(", ", result.Errors)}");
                }
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.Errors.Add($"שגיאה באימות: {ex.Message}");
                Console.WriteLine($"❌ שגיאה באימות {xmlFilePath}: {ex.Message}");
            }
            finally
            {
                result.EndTime = DateTime.Now;
            }

            return result;
        }

        /// <summary>
        /// ניתוח מהיר של תוכן קבצי XML
        /// </summary>
        /// <param name="xmlFilePath">נתיב הקובץ</param>
        /// <returns>סיכום התוכן</returns>
        public async Task<ContentSummary> AnalyzeXmlContentAsync(string xmlFilePath)
        {
            var summary = new ContentSummary
            {
                FilePath = xmlFilePath,
                AnalysisTime = DateTime.Now
            };

            try
            {
                if (!File.Exists(xmlFilePath))
                {
                    summary.ErrorMessage = "קובץ לא נמצא";
                    return summary;
                }

                var doc = new XmlDocument();
                doc.Load(xmlFilePath);

                // זיהוי סוג הקובץ
                summary.FileType = DetermineFileType(doc);

                // ספירת רכיבים
                switch (summary.FileType.ToLower())
                {
                    case "stores":
                        summary.RecordCount = doc.SelectNodes("//STORE")?.Count ?? 0;
                        summary.AdditionalInfo["stores"] = summary.RecordCount.ToString();
                        break;

                    case "prices":
                        summary.RecordCount = doc.SelectNodes("//ITEM")?.Count ?? 0;
                        var itemsWithPrices = doc.SelectNodes("//ITEM[PRICE]")?.Count ?? 0;
                        summary.AdditionalInfo["items_with_prices"] = itemsWithPrices.ToString();
                        break;

                    case "promos":
                        summary.RecordCount = doc.SelectNodes("//PROMOTION")?.Count ?? 0;
                        var activePromos = doc.SelectNodes("//PROMOTION[@IsActive='true']")?.Count ?? 0;
                        summary.AdditionalInfo["active_promotions"] = activePromos.ToString();
                        break;

                    default:
                        summary.RecordCount = doc.DocumentElement?.ChildNodes.Count ?? 0;
                        break;
                }

                // מידע כללי
                summary.AdditionalInfo["root_element"] = doc.DocumentElement?.Name ?? "unknown";
                summary.AdditionalInfo["file_size"] = new FileInfo(xmlFilePath).Length.ToString();

                summary.IsSuccessful = true;
            }
            catch (Exception ex)
            {
                summary.IsSuccessful = false;
                summary.ErrorMessage = $"שגיאה בניתוח: {ex.Message}";
            }

            return summary;
        }

        /// <summary>
        /// ניקוי קבצים זמניים ישנים
        /// </summary>
        /// <param name="basePath">נתיב בסיס לניקוי</param>
        /// <param name="olderThanHours">מספר שעות</param>
        /// <returns>מספר קבצים שנמחקו</returns>
        public async Task<int> CleanupTemporaryFilesAsync(string basePath, int olderThanHours = 24)
        {
            var deletedCount = 0;

            try
            {
                if (!Directory.Exists(basePath))
                {
                    return 0;
                }

                var cutoffTime = DateTime.Now.AddHours(-olderThanHours);
                Console.WriteLine($"🧹 מנקה קבצים זמניים ישנים מ-{cutoffTime:yyyy-MM-dd HH:mm}...");

                var filesToDelete = Directory.GetFiles(basePath, "*", SearchOption.AllDirectories)
                    .Where(file => File.GetLastWriteTime(file) < cutoffTime)
                    .ToList();

                foreach (var file in filesToDelete)
                {
                    try
                    {
                        File.Delete(file);
                        deletedCount++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ לא ניתן למחוק {file}: {ex.Message}");
                    }
                }

                // ניקוי תיקיות ריקות
                var emptyDirectories = Directory.GetDirectories(basePath, "*", SearchOption.AllDirectories)
                    .Where(dir => !Directory.EnumerateFileSystemEntries(dir).Any())
                    .ToList();

                foreach (var dir in emptyDirectories)
                {
                    try
                    {
                        Directory.Delete(dir);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ לא ניתן למחוק תיקיה {dir}: {ex.Message}");
                    }
                }

                Console.WriteLine($"🧹 נוקו {deletedCount} קבצים זמניים");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ שגיאה בניקוי קבצים זמניים: {ex.Message}");
            }

            return deletedCount;
        }

        /// <summary>
        /// יצירת דוח איכות נתונים
        /// </summary>
        /// <param name="extractedFiles">רשימת קבצים מחולצים</param>
        /// <returns>דוח איכות</returns>
        public async Task<QualityReport> GenerateQualityReportAsync(List<string> extractedFiles)
        {
            var report = new QualityReport
            {
                GeneratedTime = DateTime.Now,
                TotalFilesAnalyzed = extractedFiles.Count
            };

            try
            {
                Console.WriteLine($"📊 יוצר דוח איכות עבור {extractedFiles.Count} קבצים...");

                var tasks = extractedFiles.Select(async file =>
                {
                    var validation = await ValidateXmlFileAsync(file);
                    var content = await AnalyzeXmlContentAsync(file);

                    return new FileQuality
                    {
                        FilePath = file,
                        IsValid = validation.IsValid,
                        FileType = content.FileType,
                        RecordCount = content.RecordCount,
                        ErrorCount = validation.Errors.Count,
                        WarningCount = validation.Warnings.Count,
                        FileSize = validation.FileSize
                    };
                });

                var qualities = await Task.WhenAll(tasks);
                report.FileQualities.AddRange(qualities);

                // חישוב סטטיסטיקות
                report.ValidFilesCount = qualities.Count(q => q.IsValid);
                report.InvalidFilesCount = qualities.Count(q => !q.IsValid);
                report.TotalRecords = qualities.Sum(q => q.RecordCount);
                report.AverageFileSize = qualities.Average(q => q.FileSize);

                // חלוקה לפי סוגי קבצים
                report.FileTypeBreakdown = qualities
                    .GroupBy(q => q.FileType)
                    .ToDictionary(g => g.Key, g => g.Count());

                report.IsSuccessful = true;
                Console.WriteLine($"📊 דוח איכות הושלם: {report.ValidFilesCount}/{report.TotalFilesAnalyzed} קבצים תקינים");
            }
            catch (Exception ex)
            {
                report.IsSuccessful = false;
                report.ErrorMessage = $"שגיאה ביצירת דוח איכות: {ex.Message}";
                Console.WriteLine($"❌ {report.ErrorMessage}");
            }

            return report;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// עיבוד קטגוריית קבצים
        /// </summary>
        /// <param name="processingResult">תוצאות העיבוד הבסיסי</param>
        /// <param name="category">שם הקטגוריה</param>
        /// <returns>תוצאות עיבוד הקטגוריה</returns>
        private async Task<CategoryProcessingResult> ProcessFileCategory(
            Models.ProcessingResult processingResult,
            string category)
        {
            var result = new CategoryProcessingResult
            {
                Category = category,
                FilesProcessed = processingResult.DownloadedFiles.Count
            };

            foreach (var file in processingResult.DownloadedFiles)
            {
                try
                {
                    // חילוץ אם עדיין לא חולץ
                    if (string.IsNullOrEmpty(file.ExtractedPath) && File.Exists(file.LocalPath))
                    {
                        var extraction = await ExtractZipFileAsync(file.LocalPath);
                        if (extraction.IsSuccess && extraction.ExtractedFiles.Any())
                        {
                            file.ExtractedPath = extraction.ExtractedFiles.First().ExtractedPath;
                            file.IsExtracted = true;
                        }
                    }

                    // אימות קובץ XML
                    if (!string.IsNullOrEmpty(file.ExtractedPath) && File.Exists(file.ExtractedPath))
                    {
                        var validation = await ValidateXmlFileAsync(file.ExtractedPath);
                        if (validation.IsValid)
                        {
                            result.ValidFiles++;
                        }
                        else
                        {
                            result.InvalidFiles++;
                            result.Errors.AddRange(validation.Errors);
                        }
                    }
                    else
                    {
                        result.InvalidFiles++;
                        result.Errors.Add($"קובץ XML לא נמצא עבור {file.OriginalFileName}");
                    }
                }
                catch (Exception ex)
                {
                    result.InvalidFiles++;
                    result.Errors.Add($"שגיאה בעיבוד {file.OriginalFileName}: {ex.Message}");
                }
            }

            return result;
        }

        /// <summary>
        /// אימות מבנה XML
        /// </summary>
        /// <param name="result">תוצאת האימות</param>
        private async Task ValidateXmlStructureAsync(ValidationResult result)
        {
            try
            {
                var doc = new XmlDocument();
                doc.Load(result.FilePath);

                // בדיקות בסיסיות
                if (doc.DocumentElement == null)
                {
                    result.Errors.Add("אין אלמנט שורש");
                    result.IsValid = false;
                    return;
                }

                // בדיקת encoding
                if (doc.FirstChild is XmlDeclaration declaration)
                {
                    result.AdditionalInfo["encoding"] = declaration.Encoding ?? "unknown";
                }

                // ספירת אלמנטים
                result.ElementCount = CountElements(doc.DocumentElement);
                result.AdditionalInfo["total_elements"] = result.ElementCount.ToString();
                result.AdditionalInfo["root_element"] = doc.DocumentElement.Name;

                result.IsValid = true;
            }
            catch (XmlException ex)
            {
                result.IsValid = false;
                result.Errors.Add($"XML לא תקין: {ex.Message}");
            }
        }

        /// <summary>
        /// אימות לוגיקה עסקית
        /// </summary>
        /// <param name="result">תוצאת האימות</param>
        private async Task ValidateBusinessLogicAsync(ValidationResult result)
        {
            try
            {
                var doc = new XmlDocument();
                doc.Load(result.FilePath);

                var fileType = DetermineFileType(doc);
                result.AdditionalInfo["file_type"] = fileType;

                switch (fileType.ToLower())
                {
                    case "stores":
                        ValidateStoresFile(doc, result);
                        break;
                    case "prices":
                        ValidatePricesFile(doc, result);
                        break;
                    case "promos":
                        ValidatePromosFile(doc, result);
                        break;
                    default:
                        result.Warnings.Add($"סוג קובץ לא מזוהה: {fileType}");
                        break;
                }
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"שגיאה באימות לוגיקה עסקית: {ex.Message}");
            }
        }

        /// <summary>
        /// אימות קובץ סניפים
        /// </summary>
        private void ValidateStoresFile(XmlDocument doc, ValidationResult result)
        {
            var stores = doc.SelectNodes("//STORE");
            if (stores == null || stores.Count == 0)
            {
                result.Errors.Add("לא נמצאו סניפים בקובץ");
                result.IsValid = false;
                return;
            }

            var storeIds = new HashSet<string>();
            foreach (XmlNode store in stores)
            {
                var storeId = store.SelectSingleNode("STOREID")?.InnerText;
                if (string.IsNullOrEmpty(storeId))
                {
                    result.Warnings.Add("סניף ללא מזהה");
                    continue;
                }

                if (!storeIds.Add(storeId))
                {
                    result.Warnings.Add($"מזהה סניף כפול: {storeId}");
                }
            }

            result.AdditionalInfo["stores_count"] = stores.Count.ToString();
            result.AdditionalInfo["unique_store_ids"] = storeIds.Count.ToString();
        }

        /// <summary>
        /// אימות קובץ מחירים
        /// </summary>
        private void ValidatePricesFile(XmlDocument doc, ValidationResult result)
        {
            var items = doc.SelectNodes("//ITEM");
            if (items == null || items.Count == 0)
            {
                result.Errors.Add("לא נמצאו פריטים בקובץ מחירים");
                result.IsValid = false;
                return;
            }

            int itemsWithPrices = 0;
            int itemsWithBarcodes = 0;

            foreach (XmlNode item in items)
            {
                var price = item.SelectSingleNode("PRICE")?.InnerText;
                var barcode = item.SelectSingleNode("ITEMCODE")?.InnerText;

                if (!string.IsNullOrEmpty(price))
                    itemsWithPrices++;

                if (!string.IsNullOrEmpty(barcode))
                    itemsWithBarcodes++;
            }

            result.AdditionalInfo["items_count"] = items.Count.ToString();
            result.AdditionalInfo["items_with_prices"] = itemsWithPrices.ToString();
            result.AdditionalInfo["items_with_barcodes"] = itemsWithBarcodes.ToString();

            if (itemsWithPrices == 0)
            {
                result.Warnings.Add("אין פריטים עם מחירים");
            }
        }

        /// <summary>
        /// אימות קובץ מבצעים
        /// </summary>
        private void ValidatePromosFile(XmlDocument doc, ValidationResult result)
        {
            var promotions = doc.SelectNodes("//PROMOTION");
            if (promotions == null || promotions.Count == 0)
            {
                result.AdditionalInfo["promotions_count"] = "0";
                return; // לא שגיאה - יכול להיות שאין מבצעים
            }

            int activePromotions = 0;
            foreach (XmlNode promo in promotions)
            {
                var isActive = promo.Attributes?["IsActive"]?.Value;
                if (isActive == "true" || isActive == "1")
                {
                    activePromotions++;
                }
            }

            result.AdditionalInfo["promotions_count"] = promotions.Count.ToString();
            result.AdditionalInfo["active_promotions"] = activePromotions.ToString();
        }

        /// <summary>
        /// זיהוי סוג קובץ
        /// </summary>
        private string DetermineFileType(XmlDocument doc)
        {
            if (doc.DocumentElement == null)
                return "unknown";

            var rootName = doc.DocumentElement.Name.ToLower();

            if (rootName.Contains("store") || doc.SelectSingleNode("//STORE") != null)
                return "stores";

            if (rootName.Contains("promo") || doc.SelectSingleNode("//PROMOTION") != null)
                return "promos";

            if (rootName.Contains("price") || doc.SelectSingleNode("//ITEM") != null)
                return "prices";

            return rootName;
        }

        /// <summary>
        /// ספירת אלמנטים ב-XML
        /// </summary>
        private int CountElements(XmlNode node)
        {
            int count = 1; // האלמנט הנוכחי
            foreach (XmlNode child in node.ChildNodes)
            {
                if (child.NodeType == XmlNodeType.Element)
                {
                    count += CountElements(child);
                }
            }
            return count;
        }

        /// <summary>
        /// אימות קבצים מחולצים
        /// </summary>
        private async Task ValidateExtractedFilesAsync(ExtractionResult result)
        {
            foreach (var file in result.ExtractedFiles)
            {
                try
                {
                    var validation = await ValidateXmlFileAsync(file.ExtractedPath);
                    file.IsValid = validation.IsValid;
                    if (!validation.IsValid)
                    {
                        file.ValidationErrors.AddRange(validation.Errors);
                    }
                }
                catch (Exception ex)
                {
                    file.IsValid = false;
                    file.ValidationErrors.Add($"שגיאה באימות: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// יצירת דוח עיבוד מפורט
        /// </summary>
        private async Task GenerateProcessingReportAsync(ProcessingResult result)
        {
            try
            {
                var reportPath = Path.Combine(_settings.ReportsPath,
                    $"processing_report_{result.ChainName}_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

                Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);

                var lines = new List<string>
                {
                    $"דוח עיבוד קבצים - {result.ChainName}",
                    $"תאריך: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                    $"משך זמן: {result.Duration}",
                    "",
                    $"סיכום:",
                    $"  קבצים שעובדו: {result.TotalFilesProcessed}",
                    $"  קבצים תקינים: {result.TotalValidFiles}",
                    $"  קבצים לא תקינים: {result.TotalInvalidFiles}",
                    ""
                };

                foreach (var category in result.CategoriesProcessed)
                {
                    lines.Add($"קטגוריה: {category.Key}");
                    lines.Add($"  קבצים: {category.Value.FilesProcessed}");
                    lines.Add($"  תקינים: {category.Value.ValidFiles}");
                    lines.Add($"  לא תקינים: {category.Value.InvalidFiles}");

                    if (category.Value.Errors.Any())
                    {
                        lines.Add("  שגיאות:");
                        lines.AddRange(category.Value.Errors.Select(e => $"    - {e}"));
                    }

                    lines.Add("");
                }

                await File.WriteAllLinesAsync(reportPath, lines);
                result.ReportPath = reportPath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ שגיאה ביצירת דוח: {ex.Message}");
            }
        }

        #endregion
    }

    #region Support Classes & Settings

    /// <summary>
    /// הגדרות עיבוד קבצים
    /// </summary>
    public class FileProcessingSettings
    {
        /// <summary>
        /// גודל קובץ מקסימלי (בבתים)
        /// </summary>
        public long MaxFileSizeBytes { get; set; } = 100 * 1024 * 1024; // 100MB

        /// <summary>
        /// האם לבצע אימות לאחר חילוץ
        /// </summary>
        public bool ValidateAfterExtraction { get; set; } = true;

        /// <summary>
        /// האם ליצור דוח מפורט
        /// </summary>
        public bool GenerateDetailedReport { get; set; } = true;

        /// <summary>
        /// נתיב לשמירת דוחות
        /// </summary>
        public string ReportsPath { get; set; } = "Reports";

        /// <summary>
        /// האם לשמור קבצי ZIP לאחר חילוץ
        /// </summary>
        public bool KeepZipFiles { get; set; } = false;

        /// <summary>
        /// זמן שמירה במטמון (בדקות)
        /// </summary>
        public int CacheExpirationMinutes { get; set; } = 60;

        /// <summary>
        /// הגדרות ברירת מחדל
        /// </summary>
        public static FileProcessingSettings Default() => new FileProcessingSettings();
    }

    /// <summary>
    /// תוצאת עיבוד כללית
    /// </summary>
    public class ProcessingResult
    {
        public string ChainName { get; set; } = "";
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public TimeSpan Duration => EndTime?.Subtract(StartTime) ?? TimeSpan.Zero;

        public int TotalFilesProcessed { get; set; }
        public int TotalValidFiles { get; set; }
        public int TotalInvalidFiles { get; set; }
        public string? ReportPath { get; set; }

        public Dictionary<string, CategoryProcessingResult> CategoriesProcessed { get; set; } = new();
    }

    /// <summary>
    /// תוצאת עיבוד קטגוריה
    /// </summary>
    public class CategoryProcessingResult
    {
        public string Category { get; set; } = "";
        public int FilesProcessed { get; set; }
        public int ValidFiles { get; set; }
        public int InvalidFiles { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    /// <summary>
    /// תוצאת חילוץ קובץ
    /// </summary>
    public class ExtractionResult
    {
        public bool IsSuccess { get; set; }
        public string ZipFilePath { get; set; } = "";
        public string? ExtractedPath { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public TimeSpan Duration => EndTime?.Subtract(StartTime) ?? TimeSpan.Zero;

        public int TotalExtracted { get; set; }
        public List<ExtractedFileInfo> ExtractedFiles { get; set; } = new();
    }

    /// <summary>
    /// מידע על קובץ מחולץ
    /// </summary>
    public class ExtractedFileInfo
    {
        public string OriginalName { get; set; } = "";
        public string ExtractedPath { get; set; } = "";
        public long Size { get; set; }
        public long CompressedSize { get; set; }
        public bool IsValid { get; set; }
        public List<string> ValidationErrors { get; set; } = new();

        public double CompressionRatio => CompressedSize > 0 ? (double)Size / CompressedSize : 0;
    }

    /// <summary>
    /// תוצאת אימות קובץ
    /// </summary>
    public class ValidationResult
    {
        public string FilePath { get; set; } = "";
        public bool IsValid { get; set; }
        public long FileSize { get; set; }
        public int ElementCount { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public TimeSpan Duration => EndTime?.Subtract(StartTime) ?? TimeSpan.Zero;

        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public Dictionary<string, string> AdditionalInfo { get; set; } = new();
    }

    /// <summary>
    /// סיכום תוכן קובץ
    /// </summary>
    public class ContentSummary
    {
        public string FilePath { get; set; } = "";
        public string FileType { get; set; } = "";
        public int RecordCount { get; set; }
        public bool IsSuccessful { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime AnalysisTime { get; set; }
        public Dictionary<string, string> AdditionalInfo { get; set; } = new();
    }

    /// <summary>
    /// איכות קובץ יחיד
    /// </summary>
    public class FileQuality
    {
        public string FilePath { get; set; } = "";
        public bool IsValid { get; set; }
        public string FileType { get; set; } = "";
        public int RecordCount { get; set; }
        public int ErrorCount { get; set; }
        public int WarningCount { get; set; }
        public long FileSize { get; set; }
    }

    /// <summary>
    /// דוח איכות כללי
    /// </summary>
    public class QualityReport
    {
        public DateTime GeneratedTime { get; set; }
        public bool IsSuccessful { get; set; }
        public string? ErrorMessage { get; set; }

        public int TotalFilesAnalyzed { get; set; }
        public int ValidFilesCount { get; set; }
        public int InvalidFilesCount { get; set; }
        public int TotalRecords { get; set; }
        public double AverageFileSize { get; set; }

        public Dictionary<string, int> FileTypeBreakdown { get; set; } = new();
        public List<FileQuality> FileQualities { get; set; } = new();

        public double ValidityPercentage => TotalFilesAnalyzed > 0 ?
            (double)ValidFilesCount / TotalFilesAnalyzed * 100 : 0;
    }

    #endregion
}
