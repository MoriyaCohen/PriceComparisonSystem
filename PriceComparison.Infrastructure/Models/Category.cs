using System;
using System.Collections.Generic;

namespace PriceComparison.Infrastructure.Models;

public partial class Category
{
    public int Id { get; set; }

    public int? ParentId { get; set; }

    public string CategoryName { get; set; } = null!;

    public string? FullPath { get; set; }

    public int Level { get; set; }

    public bool? IsActive { get; set; }

    public virtual ICollection<Category> InverseParent { get; set; } = new List<Category>();

    public virtual Category? Parent { get; set; }

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}
