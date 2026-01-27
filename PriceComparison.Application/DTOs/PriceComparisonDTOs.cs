using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriceComparison.Application.DTOs
{

        /// <summary>
        /// בקשה להשוואת מחירים
        /// </summary>
        public class PriceComparisonRequest
        {
            /// <summary>
            /// ברקוד המוצר לחיפוש
            /// </summary>
            [Required(ErrorMessage = "ברקוד הוא שדה חובה")]
            [StringLength(13, MinimumLength = 8, ErrorMessage = "ברקוד חייב להיות באורך 8-13 ספרות")]
            [RegularExpression(@"^\d+$", ErrorMessage = "ברקוד חייב להכיל ספרות בלבד")]
            public string Barcode { get; set; } = string.Empty;
        }

        /// <summary>
        /// תגובת השוואת מחירים
        /// </summary>
        public class PriceComparisonResponse
        {
            /// <summary>
            /// האם החיפוש הצליח
            /// </summary>
            public bool Success { get; set; }

            /// <summary>
            /// הודעת שגיאה במקרה של כישלון
            /// </summary>
            public string? ErrorMessage { get; set; }

            /// <summary>
            /// פרטי המוצר שנמצא
            /// </summary>
            public ProductInfo? ProductInfo { get; set; }

            /// <summary>
            /// סטטיסטיקות המחירים
            /// </summary>
            public PriceStatistics? Statistics { get; set; }

            /// <summary>
            /// רשימת מחירים מפורטת, ממוינת לפי מחיר
            /// </summary>
            public List<ProductPriceInfo> PriceDetails { get; set; } = new();

            /// <summary>
            /// יצירת תגובה מוצלחת
            /// </summary>
            public static PriceComparisonResponse CreateSuccessResponse(
                ProductInfo productInfo,
                List<ProductPriceInfo> priceDetails,
                PriceStatistics statistics)
            {
                return new PriceComparisonResponse
                {
                    Success = true,
                    ProductInfo = productInfo,
                    PriceDetails = priceDetails.OrderBy(p => p.CurrentPrice).ToList(),
                    Statistics = statistics
                };
            }

            /// <summary>
            /// יצירת תגובת שגיאה
            /// </summary>
            public static PriceComparisonResponse Error(string errorMessage)
            {
                return new PriceComparisonResponse
                {
                    Success = false,
                    ErrorMessage = errorMessage,
                    PriceDetails = new List<ProductPriceInfo>()
                };
            }
        }

        /// <summary>
        /// מידע על מוצר
        /// </summary>
        public class ProductInfo
        {
            /// <summary>
            /// שם המוצר
            /// </summary>
            public string ProductName { get; set; } = string.Empty;

            /// <summary>
            /// ברקוד המוצר
            /// </summary>
            public string Barcode { get; set; } = string.Empty;

            /// <summary>
            /// שם היצרן
            /// </summary>
            public string? ManufacturerName { get; set; }

            /// <summary>
            /// יחידת מידה
            /// </summary>
            public string? UnitOfMeasure { get; set; }

            /// <summary>
            /// האם מוצר שקיל
            /// </summary>
            public bool IsWeighted { get; set; }
        }

        /// <summary>
        /// מידע על מוצר ומחיר בסניף ספציפי
        /// </summary>
        public class ProductPriceInfo
        {
            /// <summary>
            /// מזהה המוצר
            /// </summary>
            public int ProductId { get; set; }

            /// <summary>
            /// שם המוצר
            /// </summary>
            public string ProductName { get; set; } = string.Empty;

            /// <summary>
            /// שם הרשת
            /// </summary>
            public string ChainName { get; set; } = string.Empty;

            /// <summary>
            /// שם הסניף
            /// </summary>
            public string StoreName { get; set; } = string.Empty;

            /// <summary>
            /// כתובת הסניף
            /// </summary>
            public string? StoreAddress { get; set; }

            /// <summary>
            /// מחיר נוכחי
            /// </summary>
            public decimal CurrentPrice { get; set; }

            /// <summary>
            /// מחיר ליחידה (אם רלוונטי)
            /// </summary>
            public decimal? UnitPrice { get; set; }

            /// <summary>
            /// יחידת מידה
            /// </summary>
            public string? UnitOfMeasure { get; set; }

            /// <summary>
            /// האם מוצר שקיל
            /// </summary>
            public bool IsWeighted { get; set; }

            /// <summary>
            /// האם מותרת הנחה
            /// </summary>
            public bool AllowDiscount { get; set; }

            /// <summary>
            /// תאריך עדכון אחרון
            /// </summary>
            public DateTime LastUpdated { get; set; }

            /// <summary>
            /// מספר ביקורת הסניף
            /// </summary>
            public string? BikoretNo { get; set; }

            /// <summary>
            /// מזהה רשת מחרוזת
            /// </summary>
            public string? ChainId { get; set; }

            /// <summary>
            /// מזהה סניף מחרוזת
            /// </summary>
            public string? StoreId { get; set; }
        }

        /// <summary>
        /// סטטיסטיקות מחירים
        /// </summary>
        public class PriceStatistics
        {
            /// <summary>
            /// מחיר זול ביותר
            /// </summary>
            public decimal MinPrice { get; set; }

            /// <summary>
            /// מחיר יקר ביותר
            /// </summary>
            public decimal MaxPrice { get; set; }

            /// <summary>
            /// מחיר ממוצע
            /// </summary>
            public decimal AveragePrice { get; set; }

            /// <summary>
            /// כמות סניפים שנמצאו
            /// </summary>
            public int StoreCount { get; set; }

            /// <summary>
            /// כמות רשתות שנמצאו
            /// </summary>
            public int ChainCount { get; set; }

            /// <summary>
            /// סטיית תקן של המחירים
            /// </summary>
            public decimal? StandardDeviation { get; set; }

            /// <summary>
            /// חיסכון מקסימלי אפשרי
            /// </summary>
            public decimal PotentialSavings => MaxPrice - MinPrice;

            /// <summary>
            /// אחוז החיסכון המקסימלי
            /// </summary>
            public decimal PotentialSavingsPercentage => MaxPrice > 0 ? (PotentialSavings / MaxPrice) * 100 : 0;

            /// <summary>
            /// חישוב סטטיסטיקות מרשימת מחירים
            /// </summary>
            public static PriceStatistics Calculate(List<ProductPriceInfo> prices)
            {
                if (!prices.Any())
                {
                    return new PriceStatistics();
                }

                var priceValues = prices.Select(p => p.CurrentPrice).ToList();
                var uniqueChains = prices.Select(p => p.ChainName).Distinct().Count();

                var statistics = new PriceStatistics
                {
                    MinPrice = priceValues.Min(),
                    MaxPrice = priceValues.Max(),
                    AveragePrice = priceValues.Average(),
                    StoreCount = prices.Count,
                    ChainCount = uniqueChains
                };

                // חישוב סטיית תקן
                if (priceValues.Count > 1)
                {
                    var mean = (double)statistics.AveragePrice;
                    var variance = priceValues.Select(p => Math.Pow((double)p - mean, 2)).Average();
                    statistics.StandardDeviation = (decimal)Math.Sqrt(variance);
                }

                return statistics;
            }
        }

        /// <summary>
        /// אפשרויות מיון תוצאות
        /// </summary>
        public enum SortOption
        {
            /// <summary>
            /// מיון לפי מחיר - מהזול ליקר
            /// </summary>
            PriceAsc,

            /// <summary>
            /// מיון לפי מחיר - מהיקר לזול
            /// </summary>
            PriceDesc,

            /// <summary>
            /// מיון לפי שם רשת
            /// </summary>
            ChainName,

            /// <summary>
            /// מיון לפי שם סניף
            /// </summary>
            StoreName,

            /// <summary>
            /// מיון לפי תאריך עדכון אחרון
            /// </summary>
            LastUpdated
        }

        /// <summary>
        /// קודי שגיאה להשוואת מחירים
        /// </summary>
        public static class PriceComparisonErrorCodes
        {
            public const string BARCODE_NOT_FOUND = "BARCODE_NOT_FOUND";
            public const string NO_PRICES_FOUND = "NO_PRICES_FOUND";
            public const string DATABASE_ERROR = "DATABASE_ERROR";
            public const string INVALID_BARCODE = "INVALID_BARCODE";
        }
  
}
