using PriceComparison.Infrastructure.Models;

public class Chain
{
    public int Id { get; set; }
    public string ChainId { get; set; } = string.Empty;
    public string ChainName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; } = DateTime.Now;

    // Navigation properties
    public ICollection<Store> Stores { get; set; } = new List<Store>();

}
