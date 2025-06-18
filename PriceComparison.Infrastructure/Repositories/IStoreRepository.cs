using PriceComparison.Infrastructure.Models;

namespace PriceComparison.Infrastructure.Repositories
{
    public interface IStoreRepository
    {
        Task<Store?> GetByIdAsync(int id);
        Task<Store?> GetByStoreIdAsync(int chainId, string storeId);
        Task<IEnumerable<Store>> GetByChainIdAsync(int chainId);
        Task<Store> AddAsync(Store store);
        Task<Store> UpdateAsync(Store store);
    }

}