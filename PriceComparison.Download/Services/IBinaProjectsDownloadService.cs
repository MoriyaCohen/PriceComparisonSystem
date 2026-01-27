//using System;
//using System.Collections.Generic;
//using System.Threading.Tasks;
//using PriceComparison.Download.Models;

//namespace PriceComparison.Download.Services
//{
//    /// <summary>
//    /// שירות הורדה מרשתות BinaProjects - ממשק נקי
//    /// </summary>
//    public interface IBinaProjectsDownloadService
//    {
//        #region פונקציות בסיסיות - קיימות

//        /// <summary>
//        /// קבלת כל הרשתות הפעילות
//        /// </summary>
//        /// <returns>רשימת הרשתות הפעילות</returns>
//        Task<List<BinaProjectsNetworkInfo>> GetActiveNetworksAsync();

//        /// <summary>
//        /// קבלת מידע על רשת ספציפית
//        /// </summary>
//        /// <param name="networkId">מזהה הרשת</param>
//        /// <returns>מידע על הרשת או null אם לא נמצא</returns>
//        Task<BinaProjectsNetworkInfo?> GetNetworkInfoAsync(string networkId);

//        /// <summary>
//        /// קבלת רשימת כל הקבצים הזמינים לרשת ספציפית בתאריך נתון
//        /// </summary>
//        /// <param name="networkId">מזהה הרשת</param>
//        /// <param name="date">תאריך לחיפוש</param>
//        /// <returns>רשימת הקבצים הזמינים</returns>
//        Task<List<BinaProjectsFileInfo>> GetAvailableFilesAsync(string networkId, DateTime date);

//        /// <summary>
//        /// הורדת קובץ StoresFull העדכני ביותר לרשת ספציפית
//        /// </summary>
//        /// <param name="networkId">מזהה הרשת</param>
//        /// <param name="date">תאריך לחיפוש</param>
//        /// <returns>תוצאת ההורדה</returns>
//        Task<DownloadResult> DownloadLatestStoresFullAsync(string networkId, DateTime date);

//        /// <summary>
//        /// הורדת קבצי PriceFull עבור כל הסניפים ברשת ספציפית
//        /// </summary>
//        /// <param name="networkId">מזהה הרשת</param>
//        /// <param name="date">תאריך לחיפוש</param>
//        /// <param name="storeIds">רשימת מזהי סניפים (אם ריק - כל הסניפים)</param>
//        /// <returns>רשימת תוצאות ההורדות</returns>
//        Task<List<DownloadResult>> DownloadLatestPriceFullForStoresAsync(
//            string networkId,
//            DateTime date,
//            List<string>? storeIds = null);

//        /// <summary>
//        /// הורדת קבצי PromoFull עבור כל הסניפים ברשת ספציפית
//        /// </summary>
//        /// <param name="networkId">מזהה הרשת</param>
//        /// <param name="date">תאריך לחיפוש</param>
//        /// <param name="storeIds">רשימת מזהי סניפים (אם ריק - כל הסניפים)</param>
//        /// <returns>רשימת תוצאות ההורדות</returns>
//        Task<List<DownloadResult>> DownloadLatestPromoFullForStoresAsync(
//            string networkId,
//            DateTime date,
//            List<string>? storeIds = null);

//        /// <summary>
//        /// הורדה מלאה לרשת ספציפית - כל הקבצים הנדרשים
//        /// </summary>
//        /// <param name="networkId">מזהה הרשת</param>
//        /// <param name="date">תאריך לחיפוש</param>
//        /// <param name="includeStoresFull">האם לכלול StoresFull</param>
//        /// <param name="includePriceFull">האם לכלול PriceFull</param>
//        /// <param name="includePromoFull">האם לכלול PromoFull</param>
//        /// <returns>רשימת כל תוצאות ההורדות</returns>
//        Task<List<DownloadResult>> DownloadCompleteNetworkDataAsync(
//            string networkId,
//            DateTime date,
//            bool includeStoresFull = true,
//            bool includePriceFull = true,
//            bool includePromoFull = true);

//        /// <summary>
//        /// הורדת נתונים לכל הרשתות הפעילות
//        /// </summary>
//        /// <param name="date">תאריך לחיפוש</param>
//        /// <param name="includeStoresFull">האם לכלול StoresFull</param>
//        /// <param name="includePriceFull">האם לכלול PriceFull</param>
//        /// <param name="includePromoFull">האם לכלול PromoFull</param>
//        /// <returns>מילון של תוצאות לפי רשת</returns>
//        Task<Dictionary<string, List<DownloadResult>>> DownloadAllNetworksDataAsync(
//            DateTime date,
//            bool includeStoresFull = true,
//            bool includePriceFull = true,
//            bool includePromoFull = true);

//        /// <summary>
//        /// חילוץ קובץ XML מתוך ZIP
//        /// </summary>
//        /// <param name="downloadResult">תוצאת ההורדה המכילה קובץ ZIP</param>
//        /// <returns>תוצאת החילוץ</returns>
//        Task<ExtractionResult> ExtractXmlFromZipAsync(DownloadResult downloadResult);

//        /// <summary>
//        /// קבלת רשימת הסניפים הזמינים לרשת מתוך קובץ StoresFull
//        /// </summary>
//        /// <param name="networkId">מזהה הרשת</param>
//        /// <param name="storesFullXmlContent">תוכן XML של StoresFull</param>
//        /// <returns>רשימת מזהי הסניפים</returns>
//        Task<List<string>> GetAvailableStoreIdsAsync(string networkId, string storesFullXmlContent);

//        /// <summary>
//        /// ביטול כל פעולות ההורדה הפעילות
//        /// </summary>
//        Task CancelAllDownloadsAsync();

//        #endregion

//        #region פונקציות מתקדמות - חדשות 🆕

//        /// <summary>
//        /// 🆕 מוצא ומוריד את הקובץ העדכני ביותר עבור רשת ספציפית
//        /// </summary>
//        /// <param name="networkId">מזהה הרשת</param>
//        /// <param name="fileType">סוג הקובץ (StoresFull, PriceFull וכו')</param>
//        /// <returns>תוצאת ההורדה של הקובץ העדכני ביותר</returns>
//        Task<DownloadResult> DownloadLatestAvailableFileAsync(string networkId, string fileType = "StoresFull");

//        /// <summary>
//        /// 🆕 מוצא ומוריד את כל הקבצים העדכניים ביותר עבור רשת ספציפית
//        /// </summary>
//        /// <param name="networkId">מזהה הרשת</param>
//        /// <returns>רשימת תוצאות הורדה של כל הקבצים העדכניים</returns>
//        Task<List<DownloadResult>> DownloadAllLatestFilesForNetworkAsync(string networkId);

//        /// <summary>
//        /// 🆕 מוצא ומוריד את הקבצים העדכניים ביותר מכל הרשתות
//        /// </summary>
//        /// <returns>מילון של תוצאות הורדה לכל רשת</returns>
//        Task<Dictionary<string, List<DownloadResult>>> DownloadLatestFromAllNetworksAsync();

//        /// <summary>
//        /// 🆕 בדיקת חיבור לכל הרשתות
//        /// </summary>
//        /// <returns>מילון עם סטטוס החיבור לכל רשת</returns>
//        Task<Dictionary<string, string>> TestNetworkConnectionsAsync();

//        /// <summary>
//        /// 🆕 חיפוש הקבצים העדכניים ביותר ברשת ללא הורדה
//        /// </summary>
//        /// <param name="networkId">מזהה הרשת</param>
//        /// <param name="daysBack">כמה ימים אחורה לחפש (ברירת מחדל: 7)</param>
//        /// <returns>רשימת הקבצים העדכניים שנמצאו</returns>
//        Task<List<BinaProjectsFileInfo>> FindLatestFilesAsync(string networkId, int daysBack = 7);

//        /// <summary>
//        /// 🆕 קבלת סטטיסטיקות מפורטות על רשת ספציפית
//        /// </summary>
//        /// <param name="networkId">מזהה הרשת</param>
//        /// <param name="daysBack">כמה ימים אחורה לבדוק</param>
//        /// <returns>סטטיסטיקות מפורטות</returns>
//        Task<NetworkStatistics> GetNetworkStatisticsAsync(string networkId, int daysBack = 30);

//        #endregion

//        #region אירועים

//        /// <summary>
//        /// אירוע התקדמות הורדה
//        /// </summary>
//        event EventHandler<DownloadProgressEventArgs> DownloadProgress;

//        /// <summary>
//        /// אירוע השלמת הורדה
//        /// </summary>
//        event EventHandler<DownloadCompletedEventArgs> DownloadCompleted;

//        #endregion
//    }

//    #region מודלים נוספים

//    /// <summary>
//    /// סטטיסטיקות מפורטות של רשת
//    /// </summary>
//    public class NetworkStatistics
//    {
//        public string NetworkId { get; set; } = string.Empty;
//        public string NetworkName { get; set; } = string.Empty;
//        public int TotalFilesFound { get; set; }
//        public int StoresCount { get; set; }
//        public DateTime? LatestFileDate { get; set; }
//        public Dictionary<string, int> FileTypesCounts { get; set; } = new();
//        public bool IsOnline { get; set; }
//        public string? LastError { get; set; }
//    }

//    /// <summary>
//    /// נתוני התקדמות הורדה
//    /// </summary>
//    public class DownloadProgressEventArgs : EventArgs
//    {
//        public string NetworkId { get; set; } = string.Empty;
//        public string FileName { get; set; } = string.Empty;
//        public long BytesReceived { get; set; }
//        public long TotalBytesToReceive { get; set; }
//        public int ProgressPercentage => TotalBytesToReceive > 0
//            ? (int)((BytesReceived * 100) / TotalBytesToReceive)
//            : 0;
//    }

//    /// <summary>
//    /// נתוני השלמת הורדה
//    /// </summary>
//    public class DownloadCompletedEventArgs : EventArgs
//    {
//        public string NetworkId { get; set; } = string.Empty;
//        public DownloadResult Result { get; set; } = new();
//        public bool WasCancelled { get; set; }
//    }

//    #endregion
//}