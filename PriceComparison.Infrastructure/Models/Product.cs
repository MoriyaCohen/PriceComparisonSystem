using PriceComparison.Infrastructure.Models;

public class Product
{
    public int Id { get; set; }
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string? Barcode { get; set; }

    public int? CategoryId { get; set; }
    public string? ManufacturerName { get; set; }
    public string? UnitOfMeasure { get; set; }
    public bool IsWeighted { get; set; } = false;
    public decimal? QtyInPackage { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; } = DateTime.Now;

    // Navigation properties
    public Category? Category { get; set; }
    public ICollection<StorePrice> StorePrices { get; set; } = new List<StorePrice>();
}