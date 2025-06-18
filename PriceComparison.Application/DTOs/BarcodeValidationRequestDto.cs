namespace PriceComparison.Application.DTOs
{
    /// <summary>
    /// בקשת בדיקת תקינות ברקוד
    /// </summary>
    public class BarcodeValidationRequestDto
    {
        /// <summary>
        /// מספר הברקוד לבדיקה
        /// </summary>
        public string Barcode { get; set; } = string.Empty;
    }

    /// <summary>
    /// תגובת בדיקת תקינות ברקוד
    /// </summary>
    public class BarcodeValidationResponseDto
    {
        /// <summary>
        /// האם הברקוד תקין
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// הודעת שגיאה במקרה שהברקוד לא תקין
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// הברקוד המנורמל (לאחר ניקוי ופורמט)
        /// </summary>
        public string? NormalizedBarcode { get; set; }
    }

    /// <summary>
    /// בקשת השוואת מחירים
    /// </summary>
    public class PriceComparisonRequestDto
    {
        /// <summary>
        /// ברקוד המוצר לחיפוש
        /// </summary>
        public string Barcode { get; set; } = string.Empty;
    }

    /// <summary>
    /// מידע על מוצר
    /// </summary>
    public class ProductInfoDto
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
    }

    /// <summary>
    /// מידע על מחיר מוצר בסניף ספציפי
    /// </summary>
    public class ProductPriceInfoDto
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
        /// מחיר ליחידה
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
        /// האם מותר הנחה
        /// </summary>
        public bool AllowDiscount { get; set; }

        /// <summary>
        /// תאריך עדכון אחרון
        /// </summary>
        public DateTime LastUpdated { get; set; }

        /// <summary>
        /// האם זהו המחיר הזול ביותר
        /// </summary>
        public bool IsMinPrice { get; set; }
    }

    /// <summary>
    /// סטטיסטיקות מחירים
    /// </summary>
    public class PriceStatisticsDto
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
    }

    /// <summary>
    /// תגובת השוואת מחירים
    /// </summary>
    public class PriceComparisonResponseDto
    {
        /// <summary>
        /// האם החיפוש היה מוצלח
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// הודעת שגיאה במקרה של כישלון
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// פרטי המוצר שנמצא
        /// </summary>
        public ProductInfoDto? ProductInfo { get; set; }

        /// <summary>
        /// סטטיסטיקות המחירים
        /// </summary>
        public PriceStatisticsDto? Statistics { get; set; }

        /// <summary>
        /// רשימת כל המחירים שנמצאו, ממוינת לפי מחיר
        /// </summary>
        public List<ProductPriceInfoDto> PriceDetails { get; set; } = new();
    }
}