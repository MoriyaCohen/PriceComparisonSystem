using System;
using System.Collections.Generic;

namespace PriceComparison.Infrastructure.Models;

public partial class Product
{
    public int Id { get; set; }

    public string ProductId { get; set; } = null!;

    public string ProductName { get; set; } = null!;

    public string? Barcode { get; set; }

    public int? CategoryId { get; set; }

    public string? ManufacturerName { get; set; }

    public string? UnitOfMeasure { get; set; }

    public bool? IsWeighted { get; set; }

    public decimal? QtyInPackage { get; set; }

    public bool? IsActive { get; set; }

    public DateTime? CreatedDate { get; set; }

    public virtual Category? Category { get; set; }

    public virtual ICollection<StorePrice> StorePrices { get; set; } = new List<StorePrice>();
}
