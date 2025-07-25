using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriceComparison.Download.Models
{
    /// <summary>
    /// מידע על רשת
    /// </summary>
    public class ChainInfo
    {
        /// <summary>
        /// שם הרשת
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// קוד הרשת (prefix)
        /// </summary>
        public string Prefix { get; set; } = string.Empty;

        /// <summary>
        /// כתובת בסיס
        /// </summary>
        public string BaseUrl { get; set; } = string.Empty;

        /// <summary>
        /// סוג הרשת (BinaProjects, PublishedPrices, Custom)
        /// </summary>
        public ChainType Type { get; set; } = ChainType.BinaProjects;

        /// <summary>
        /// פרטי התחברות (אם נדרשים)
        /// </summary>
        public LoginCredentials? Credentials { get; set; }

        /// <summary>
        /// האם הרשת פעילה
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// הערות נוספות
        /// </summary>
        public string? Notes { get; set; }
    }
}
