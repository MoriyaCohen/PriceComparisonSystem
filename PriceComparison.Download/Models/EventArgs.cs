using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriceComparison.Download.Models
{
    using System;

    namespace PriceComparison.Download.Models
    {
        /// <summary>
        /// אירוע התקדמות הורדה
        /// </summary>
        public class DownloadProgressEventArgs : EventArgs
        {
            public string ChainName { get; set; } = string.Empty;
            public double ProgressPercentage { get; set; }
            public string CurrentOperation { get; set; } = string.Empty;
            public int CompletedFiles { get; set; }
            public int TotalFiles { get; set; }
            public long DownloadedBytes { get; set; }
            public long TotalBytes { get; set; }
        }

        /// <summary>
        /// אירוע השלמת משימה
        /// </summary>
        public class TaskCompletedEventArgs : EventArgs
        {
            public string ChainName { get; set; } = string.Empty;
            public bool IsSuccess { get; set; }
            public TimeSpan Duration { get; set; }
            public string? ErrorMessage { get; set; }
            public int FilesDownloaded { get; set; }
            public long TotalSize { get; set; }
        }

        /// <summary>
        /// אירוע הרצה מתוזמנת
        /// </summary>
        public class ScheduledRunEventArgs : EventArgs
        {
            public DateTime StartTime { get; set; }
            public DateTime? EndTime { get; set; }
            public bool IsSuccess { get; set; }
            public TimeSpan Duration => EndTime?.Subtract(StartTime) ?? TimeSpan.Zero;
            public string? ErrorMessage { get; set; }
            public int ChainsProcessed { get; set; }
            public int SuccessfulChains { get; set; }
        }
    }
}
