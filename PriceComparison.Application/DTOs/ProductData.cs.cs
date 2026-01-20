using System;

namespace PriceComparison.Application.DTOs
{
    public class ProductData
    {
        public string? ChainId { get; set; }
        public string? StoreId { get; set; }

        // --- סניף ---
        public string? StoreLabel { get; set; }      // שם מלא + כתובת + עיר
        public string? StoreAddress { get; set; }    // כתובת בלבד
        public string? StoreCity { get; set; }       // עיר בלבד

        // --- מוצר ---
        public string? ItemCode { get; set; }
        public string? ItemName { get; set; }

        // --- שדות מחיר ---
        public decimal RegularPrice { get; set; }
        public decimal? PromoPrice { get; set; }

        // --- פרטי מבצע ---
        public bool HasPromo { get; set; }
        public string? PromoDescription { get; set; }
        public DateTime? PromoEndDate { get; set; }

        // --- סוגי מבצע ---
        public bool IsClubMemberPromo { get; set; }
        public bool IsCreditCardPromo { get; set; }

        // --- תצוגה סופית ---
        public decimal FinalCalculatedPrice { get; set; }
        public string? PriceTypeLabel { get; set; }
    }
}
