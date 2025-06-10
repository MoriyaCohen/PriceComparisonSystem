using Microsoft.EntityFrameworkCore;
using PriceComparison.Infrastructure.Models;

namespace PriceComparison.Infrastructure.Repositories
{
    public class ChainRepository : IChainRepository
    {
        private readonly PriceComparisonDbContext _context;

        public ChainRepository(PriceComparisonDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Chain>> GetAllAsync()
        {
            return await _context.Chains
                .Where(c => c.IsActive == true)
                .OrderBy(c => c.ChainName)
                .ToListAsync();
        }

        public async Task<Chain?> GetByIdAsync(int id)
        {
            return await _context.Chains
                .FirstOrDefaultAsync(c => c.Id == id && c.IsActive == true);
        }

        public async Task<Chain?> GetByChainIdAsync(string chainId)
        {
            return await _context.Chains
                .FirstOrDefaultAsync(c => c.ChainId == chainId && c.IsActive == true);
        }

        public async Task<Chain> AddAsync(Chain chain)
        {
            _context.Chains.Add(chain);
            await _context.SaveChangesAsync();
            return chain;
        }

        public async Task<Chain> UpdateAsync(Chain chain)
        {
            _context.Chains.Update(chain);
            await _context.SaveChangesAsync();
            return chain;
        }
    }
}