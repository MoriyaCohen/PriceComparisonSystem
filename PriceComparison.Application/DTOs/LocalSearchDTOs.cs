// החלף כל התוכן בקובץ: PriceComparison.Application/DTOs/LocalSearchDTOs.cs

namespace PriceComparison.Application.DTOs
{
    /// <summary>
    /// DTO חדש שהפרונטאנד שולח לחיפוש מקומי
    /// </summary>
    public class BarcodeSearchRequestDto
    {
        /// <summary>
        /// ברקוד המוצר לחיפוש מקומי
        /// </summary>
        public string Barcode { get; set; } = string.Empty;
    }

    /// <summary>
    /// מצב נתוני XML המקומיים
    /// </summary>
    public class LocalDataStatusDto
    {
        /// <summary>
        /// מספר רשתות טעונות
        /// </summary>
        public int LoadedChains { get; set; }

        /// <summary>
        /// מספר סניפים טעונים
        /// </summary>
        public int LoadedStores { get; set; }

        /// <summary>
        /// מספר מוצרים עם ברקוד
        /// </summary>
        public int TotalProducts { get; set; }

        /// <summary>
        /// זמן טעינה אחרונה
        /// </summary>
        public DateTime LastRefresh { get; set; }

        /// <summary>
        /// האם הנתונים זמינים
        /// </summary>
        public bool IsDataAvailable { get; set; }

        /// <summary>
        /// הודעת מצב
        /// </summary>
        public string StatusMessage { get; set; } = string.Empty;
    }

    /// <summary>
    /// פרטי מוצר מקומי לחיפוש
    /// </summary>
    public class LocalProductDto
    {
        public string Barcode { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string ManufacturerName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal? UnitPrice { get; set; }
        public string UnitOfMeasure { get; set; } = string.Empty;
        public bool IsWeighted { get; set; }
        public string ChainName { get; set; } = string.Empty;
        public string StoreName { get; set; } = string.Empty;
        public string StoreAddress { get; set; } = string.Empty;
        public string StoreId { get; set; } = string.Empty;
        public DateTime PriceUpdateDate { get; set; }
    }

    /// <summary>
    /// מידע על קובץ XML שנטען
    /// </summary>
    public class LoadedFileInfoDto
    {
        public string FileName { get; set; } = string.Empty;
        public string FileType { get; set; } = string.Empty; // StoreFull, PriceFull
        public string ChainName { get; set; } = string.Empty;
        public string StoreId { get; set; } = string.Empty;
        public DateTime FileDate { get; set; }
        public int ItemCount { get; set; }
        public bool LoadedSuccessfully { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }
}