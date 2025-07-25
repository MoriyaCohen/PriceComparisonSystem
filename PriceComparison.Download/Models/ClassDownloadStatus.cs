using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriceComparison.Download.Models
{
    /// <summary>
    /// מצבי הורדה
    /// </summary>
    public enum DownloadStatus
    {
        /// <summary>
        /// ממתין להתחלה
        /// </summary>
        Pending,

        /// <summary>
        /// בתהליך הורדה
        /// </summary>
        InProgress,

        /// <summary>
        /// הושלם בהצלחה
        /// </summary>
        Completed,

        /// <summary>
        /// הושלם עם שגיאות
        /// </summary>
        CompletedWithErrors,

        /// <summary>
        /// נכשל
        /// </summary>
        Failed,

        /// <summary>
        /// בוטל
        /// </summary>
        Cancelled
    }
}
