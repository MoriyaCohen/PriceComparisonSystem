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
    }
}