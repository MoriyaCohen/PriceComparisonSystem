using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriceComparison.Download.Models
{
    /// <summary>
    /// תוצאות עיבוד סוג קובץ ספציפי
    /// </summary>
    public class ProcessingResult
    {
        /// <summary>
        /// סוג הקובץ
        /// </summary>
        public string FileType { get; set; } = string.Empty;

        /// <summary>
        /// מספר קבצים שהורדו בהצלחה
        /// </summary>
        public int SuccessfulDownloads { get; set; }

        /// <summary>
        /// מספר קבצים שנכשלו
        /// </summary>
        public int FailedDownloads { get; set; }

        /// <summary>
        /// גודל כולל של הקבצים (בבתים)
        /// </summary>
        public long TotalSize { get; set; }

        /// <summary>
        /// רשימת שגיאות ספציפיות
        /// </summary>
        public List<string> Errors { get; set; } = new();

        /// <summary>
        /// רשימת הקבצים שהורדו בהצלחה
        /// </summary>
        public List<DownloadedFileInfo> DownloadedFiles { get; set; } = new();

        /// <summary>
        /// האם כל ההורדות הצליחו
        /// </summary>
        public bool IsFullySuccessful => FailedDownloads == 0 && SuccessfulDownloads > 0;
    } 
}
