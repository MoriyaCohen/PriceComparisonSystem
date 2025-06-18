using PriceComparison.Infrastructure.Models;

namespace PriceComparison.Infrastructure.Repositories
{
    public interface IStorePriceRepository
    {
        Task<StorePrice?> GetByStoreAndProductAsync(int storeId, int productId);
        Task<IEnumerable<StorePrice>> GetPricesByBarcodeAsync(string barcode);
        Task<IEnumerable<StorePrice>> GetPricesByProductIdAsync(int productId);
        Task<StorePrice> AddAsync(StorePrice storePrice);
        Task<StorePrice> UpdateAsync(StorePrice storePrice);
        Task<IEnumerable<StorePrice>> GetLatestPricesAsync();
        Task<StorePrice?> GetLowestPriceForProductAsync(int productId);
        Task<IEnumerable<StorePrice>> GetPriceComparisonAsync(string barcode);
    }
}