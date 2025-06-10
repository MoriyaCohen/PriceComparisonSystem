using Microsoft.EntityFrameworkCore;
using PriceComparison.Infrastructure.Models;

namespace PriceComparison.Infrastructure.Repositories
{
    public class StoreRepository : IStoreRepository
    {
        private readonly PriceComparisonDbContext _context;

        public StoreRepository(PriceComparisonDbContext context)
        {
            _context = context;
        }

        public async Task<Store?> GetByIdAsync(int id)
        {
            return await _context.Stores
                .Include(s => s.Chain)
                .FirstOrDefaultAsync(s => s.Id == id && s.IsActive == true);
        }

        public async Task<Store?> GetByStoreIdAsync(int chainId, string storeId)
        {
            return await _context.Stores
                .FirstOrDefaultAsync(s => s.ChainId == chainId && s.StoreId == storeId && s.IsActive == true);
        }

        public async Task<IEnumerable<Store>> GetByChainIdAsync(int chainId)
        {
            return await _context.Stores
                .Where(s => s.ChainId == chainId && s.IsActive == true)
                .OrderBy(s => s.StoreName)
                .ToListAsync();
        }

        public async Task<Store> AddAsync(Store store)
        {
            _context.Stores.Add(store);
            await _context.SaveChangesAsync();
            return store;
        }

        public async Task<Store> UpdateAsync(Store store)
        {
            _context.Stores.Update(store);
            await _context.SaveChangesAsync();
            return store;
        }
    }
}