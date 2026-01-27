using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriceComparison.Download.Models
{

    /// <summary>
    /// סוגי רשתות
    /// </summary>
    public enum ChainType
    {
        /// <summary>
        /// רשתות בינה פרוגקט
        /// </summary>
        BinaProjects,

        /// <summary>
        /// רשתות PublishedPrices
        /// </summary>
        PublishedPrices,

        /// <summary>
        /// רשתות עם מימוש מותאם אישית
        /// </summary>
        Custom
    }
}
