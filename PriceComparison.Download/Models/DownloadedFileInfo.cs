using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriceComparison.Download.Models
{
    public class DownloadedFileInfo
    {
        /// <summary>
        /// שם הקובץ המקורי
        /// </summary>
        public string OriginalFileName { get; set; } = string.Empty;

        /// <summary>
        /// נתיב הקובץ המקומי
        /// </summary>
        public string LocalPath { get; set; } = string.Empty;

        /// <summary>
        /// גודל הקובץ (בבתים)
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// זמן ההורדה
        /// </summary>
        public DateTime DownloadTime { get; set; }

        /// <summary>
        /// האם הקובץ חולץ מ-ZIP בהצלחה
        /// </summary>
        public bool IsExtracted { get; set; }

        /// <summary>
        /// נתיב הקובץ המחולץ (XML)
        /// </summary>
        public string? ExtractedPath { get; set; }

        /// <summary>
        /// מזהה סניף (אם רלוונטי)
        /// </summary>
        public string? StoreId { get; set; }
    }

}
