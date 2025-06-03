using System;
using System.Collections.Generic;

namespace PriceComparison.Infrastructure.Models;

public partial class Chain
{
    public int Id { get; set; }

    public string ChainId { get; set; } = null!;

    public string ChainName { get; set; } = null!;

    public bool? IsActive { get; set; }

    public DateTime? CreatedDate { get; set; }

    public virtual ICollection<Store> Stores { get; set; } = new List<Store>();
}
