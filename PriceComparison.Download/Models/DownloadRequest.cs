using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriceComparison.Download.Models
{
    /// <summary>
    /// בקשה להורדת קבצים מרשת ספציפית
    /// </summary>
    public class DownloadRequest
    {
        /// <summary>
        /// תאריך הקבצים הרצויים בפורמט dd/MM/yyyy
        /// </summary>
        public string Date { get; set; } = DateTime.Now.ToString("dd/MM/yyyy");

        /// <summary>
        /// שם הרשת
        /// </summary>
        public string ChainName { get; set; } = string.Empty;

        /// <summary>
        /// רשימת סניפים ספציפיים להורדה (ריק = כל הסניפים)
        /// </summary>
        public List<string> SpecificStores { get; set; } = new();

        /// <summary>
        /// סוגי קבצים להורדה
        /// </summary>
        public FileTypeFilter FileTypes { get; set; } = FileTypeFilter.All;

        /// <summary>
        /// האם לכלול גם גרסאות קודמות של אותו יום
        /// </summary>
        public bool IncludeOlderVersions { get; set; } = false;

        /// <summary>
        /// נתיב יעד לשמירת הקבצים
        /// </summary>
        public string? TargetPath { get; set; }

        /// <summary>
        /// הגדרות Azure Storage (אם רלוונטי)
        /// </summary>
        public AzureStorageSettings? AzureSettings { get; set; }
    }
}
