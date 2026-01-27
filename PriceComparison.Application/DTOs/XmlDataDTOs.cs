// החלף את כל התוכן בקובץ: PriceComparison.Application/DTOs/XmlDataDTOs.cs

namespace PriceComparison.Application.DTOs
{
    public class ProcessingResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public int ProcessedItems { get; set; }
        public int NewItems { get; set; }
        public int UpdatedItems { get; set; }
        public StoreInfoDto? StoreInfo { get; set; }
        public string TaskId { get; set; } = Guid.NewGuid().ToString();
        public DateTime ProcessedAt { get; set; } = DateTime.Now;
    }

    public class StoreInfoDto
    {
        public string ChainId { get; set; } = string.Empty;
        public string SubChainId { get; set; } = string.Empty;
        public string StoreId { get; set; } = string.Empty;
        public string BikoretNo { get; set; } = string.Empty;
        public string ChainName { get; set; } = string.Empty;
        public string StoreName { get; set; } = string.Empty;
    }

    public class ProductItemDto
    {
        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string ManufacturerName { get; set; } = string.Empty;
        public string ManufacturerCountry { get; set; } = string.Empty;
        public string ManufacturerItemDescription { get; set; } = string.Empty;
        public string UnitQty { get; set; } = string.Empty;
        public string UnitOfMeasure { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal ItemPrice { get; set; }
        public decimal UnitOfMeasurePrice { get; set; }
        public decimal QtyInPackage { get; set; }
        public bool IsWeighted { get; set; }
        public bool AllowDiscount { get; set; }
        public int ItemStatus { get; set; }
        public int ItemType { get; set; }
        public string PriceUpdateDate { get; set; } = string.Empty;
        public string? Barcode { get; set; }
    }

    public class XmlUploadRequest
    {
        public StoreInfoDto StoreInfo { get; set; } = new();
        public List<ProductItemDto> Items { get; set; } = new();
        public int TotalItems { get; set; }
    }
}