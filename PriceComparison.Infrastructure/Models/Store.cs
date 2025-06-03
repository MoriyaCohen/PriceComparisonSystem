using System;
using System.Collections.Generic;

namespace PriceComparison.Infrastructure.Models;

public partial class Store
{
    public int Id { get; set; }

    public int ChainId { get; set; }

    public string StoreId { get; set; } = null!;

    public string StoreName { get; set; } = null!;

    public string? SubChainId { get; set; }

    public string? SubChainName { get; set; }

    public string? BikoretNo { get; set; }

    public string? Address { get; set; }

    public bool? IsActive { get; set; }

    public DateTime? CreatedDate { get; set; }

    public virtual Chain Chain { get; set; } = null!;

    public virtual ICollection<StorePrice> StorePrices { get; set; } = new List<StorePrice>();
}
