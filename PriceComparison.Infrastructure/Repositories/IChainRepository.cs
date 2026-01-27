using PriceComparison.Infrastructure.Models;

namespace PriceComparison.Infrastructure.Repositories
{
    public interface IChainRepository
    {
        Task<IEnumerable<Chain>> GetAllAsync();
        Task<Chain?> GetByIdAsync(int id);
        Task<Chain?> GetByChainIdAsync(string chainId);
        Task<Chain> AddAsync(Chain chain);
        Task<Chain> UpdateAsync(Chain chain);
        Task<IEnumerable<Chain>> GetChainsWithStoresAsync();
        Task<Chain?> GetChainWithStoresAsync(int id);
        Task<IEnumerable<Chain>> GetActiveAsync();
        Task<bool> SoftDeleteAsync(int id);
    }
  
}