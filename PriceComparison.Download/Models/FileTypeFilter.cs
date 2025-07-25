using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriceComparison.Download.Models
{

    /// <summary>
    /// סינון סוגי קבצים
    /// </summary>
    [Flags]
    public enum FileTypeFilter
    {
        /// <summary>
        /// ללא קבצים
        /// </summary>
        None = 0,

        /// <summary>
        /// קבצי StoresFull
        /// </summary>
        StoresFull = 1,

        /// <summary>
        /// קבצי PriceFull
        /// </summary>
        PriceFull = 2,

        /// <summary>
        /// קבצי PromoFull
        /// </summary>
        PromoFull = 4,

        /// <summary>
        /// קבצי Price
        /// </summary>
        Price = 8,

        /// <summary>
        /// קבצי Promo
        /// </summary>
        Promo = 16,

        /// <summary>
        /// כל הקבצים
        /// </summary>
        All = StoresFull | PriceFull | PromoFull | Price | Promo
    }
}
