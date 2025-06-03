using System;
using System.Collections.Generic;

namespace PriceComparison.Infrastructure.Models;

public partial class StorePrice
{
    public int Id { get; set; }

    public int StoreId { get; set; }

    public int ProductId { get; set; }

    public string? ItemCode { get; set; }

    public decimal CurrentPrice { get; set; }

    public decimal? UnitPrice { get; set; }

    public string? StockQuantity { get; set; }

    public int? ItemStatus { get; set; }

    public bool? AllowDiscount { get; set; }

    public DateTime? FirstSeen { get; set; }

    public DateTime? LastUpdated { get; set; }

    public virtual Product Product { get; set; } = null!;

    public virtual Store Store { get; set; } = null!;
}
