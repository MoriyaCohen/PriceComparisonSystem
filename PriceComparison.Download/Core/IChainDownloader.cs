using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using PriceComparison.Download.Models;

namespace PriceComparison.Download.Core
{
    /// <summary>
    /// ממשק אחיד לכל מודולי ההורדה של הרשתות השונות
    /// מגדיר את הפעולות הבסיסיות שכל רשת צריכה לתמוך בהן
    /// </summary>
    public interface IChainDownloader
    {
        /// <summary>
        /// שם הרשת
        /// </summary>
        string ChainName { get; }

        /// <summary>
        /// קוד הרשת (prefix)
        /// </summary>
        string ChainPrefix { get; }

        /// <summary>
        /// כתובת בסיס של הרשת
        /// </summary>
        string BaseUrl { get; }

        /// <summary>
        /// הורדת כל הקבצים הנדרשים לתאריך נתון
        /// </summary>
        /// <param name="request">פרטי הבקשה להורדה</param>
        /// <returns>תוצאות ההורדה</returns>
        Task<DownloadResult> DownloadAllFilesAsync(DownloadRequest request);

        /// <summary>
        /// הורדת קובץ StoresFull העדכני ביותר
        /// </summary>
        /// <param name="date">התאריך בפורמט dd/MM/yyyy</param>
        /// <returns>תוצאת ההורדה</returns>
        Task<ProcessingResult> DownloadLatestStoresFullAsync(string date);

        /// <summary>
        /// הורדת קבצי PriceFull לכל הסניפים
        /// </summary>
        /// <param name="date">התאריך בפורמט dd/MM/yyyy</param>
        /// <param name="storeIds">רשימת מזהי סניפים</param>
        /// <returns>תוצאת ההורדה</returns>
        Task<ProcessingResult> DownloadLatestPriceFullForAllStoresAsync(string date, List<string> storeIds);

        /// <summary>
        /// הורדת קבצי PromoFull לכל הסניפים
        /// </summary>
        /// <param name="date">התאריך בפורמט dd/MM/yyyy</param>
        /// <param name="storeIds">רשימת מזהי סניפים</param>
        /// <returns>תוצאת ההורדה</returns>
        Task<ProcessingResult> DownloadLatestPromoFullForAllStoresAsync(string date, List<string> storeIds);

        /// <summary>
        /// קבלת רשימת כל הסניפים הזמינים לתאריך נתון
        /// </summary>
        /// <param name="date">התאריך בפורמט dd/MM/yyyy</param>
        /// <returns>רשימת מזהי סניפים</returns>
        Task<List<string>> GetAllAvailableStoresAsync(string date);

        /// <summary>
        /// בדיקת זמינות השירות
        /// </summary>
        /// <returns>true אם השירות זמין</returns>
        Task<bool> IsServiceAvailableAsync();

        /// <summary>
        /// קבלת סטטיסטיקות על הקבצים הזמינים
        /// </summary>
        /// <param name="date">התאריך בפורמט dd/MM/yyyy</param>
        /// <returns>מידע סטטיסטי</returns>
        Task<DownloadStatistics> GetDownloadStatisticsAsync(string date);
    }

    /// <summary>
    /// סטטיסטיקות הורדה
    /// </summary>
    public class DownloadStatistics
    {
        /// <summary>
        /// מספר סניפים זמינים
        /// </summary>
        public int AvailableStores { get; set; }

        /// <summary>
        /// מספר קבצי StoresFull זמינים
        /// </summary>
        public int StoresFullCount { get; set; }

        /// <summary>
        /// מספר קבצי PriceFull זמינים
        /// </summary>
        public int PriceFullCount { get; set; }

        /// <summary>
        /// מספר קבצי PromoFull זמינים
        /// </summary>
        public int PromoFullCount { get; set; }

        /// <summary>
        /// תאריך העדכון האחרון
        /// </summary>
        public DateTime? LastUpdateTime { get; set; }

        /// <summary>
        /// גודל כולל משוער (בבתים)
        /// </summary>
        public long EstimatedTotalSize { get; set; }
    }
}
