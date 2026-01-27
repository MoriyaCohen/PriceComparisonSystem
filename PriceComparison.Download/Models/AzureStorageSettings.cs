using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriceComparison.Download.Models
{


    /// <summary>
    /// הגדרות Azure Storage
    /// </summary>
    public class AzureStorageSettings
    {
        /// <summary>
        /// מחרוזת התחברות
        /// </summary>
        public string ConnectionString { get; set; } = string.Empty;

        /// <summary>
        /// שם הקונטיינר
        /// </summary>
        public string ContainerName { get; set; } = "price-comparison-data";

        /// <summary>
        /// קידומת נתיב
        /// </summary>
        public string PathPrefix { get; set; } = string.Empty;
    }
}
