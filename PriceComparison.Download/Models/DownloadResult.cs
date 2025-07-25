using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriceComparison.Download.Models
{
    /// <summary>
    /// תוצאות הורדה מרשת ספציפית
    /// </summary>
    public class DownloadResult
    {
        /// <summary>
        /// שם הרשת
        /// </summary>
        public string ChainName { get; set; } = string.Empty;

        /// <summary>
        /// האם ההורדה הצליחה
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// הודעת שגיאה (אם קיימת)
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// זמן תחילת ההורדה
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// זמן סיום ההורדה
        /// </summary>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// משך זמן ההורדה
        /// </summary>
        public TimeSpan Duration => EndTime?.Subtract(StartTime) ?? TimeSpan.Zero;

        /// <summary>
        /// תוצאות הורדת StoresFull
        /// </summary>
        public ProcessingResult StoresFullResult { get; set; } = new();

        /// <summary>
        /// תוצאות הורדת PriceFull
        /// </summary>
        public ProcessingResult PriceFullResult { get; set; } = new();

        /// <summary>
        /// תוצאות הורדת PromoFull
        /// </summary>
        public ProcessingResult PromoFullResult { get; set; } = new();

        /// <summary>
        /// מספר כולל של קבצים שהורדו בהצלחה
        /// </summary>
        public int TotalDownloadedFiles =>
            StoresFullResult.SuccessfulDownloads +
            PriceFullResult.SuccessfulDownloads +
            PromoFullResult.SuccessfulDownloads;

        /// <summary>
        /// גודל כולל של הקבצים שהורדו (בבתים)
        /// </summary>
        public long TotalDownloadedSize =>
            StoresFullResult.TotalSize +
            PriceFullResult.TotalSize +
            PromoFullResult.TotalSize;

        /// <summary>
        /// רשימת כל השגיאות שהתרחשו
        /// </summary>
        public List<string> AllErrors
        {
            get
            {
                var errors = new List<string>();
                if (!string.IsNullOrEmpty(ErrorMessage))
                    errors.Add(ErrorMessage);
                errors.AddRange(StoresFullResult.Errors);
                errors.AddRange(PriceFullResult.Errors);
                errors.AddRange(PromoFullResult.Errors);
                return errors;
            }
        }
    }
}
