//using Microsoft.EntityFrameworkCore;
//using PriceComparison.Infrastructure.Models;

//namespace PriceComparison.Infrastructure.Repositories
//{
//    public class ChainRepository : IChainRepository
//    {
//        private readonly PriceComparisonDbContext _context;

//        public ChainRepository(PriceComparisonDbContext context)
//        {
//            _context = context;
//        }

//        public async Task<IEnumerable<Chain>> GetAllAsync()
//        {
//            return await _context.Chains
//                .Where(c => c.IsActive == true)
//                .OrderBy(c => c.ChainName)
//                .ToListAsync();
//        }

//        public async Task<Chain?> GetByIdAsync(int id)
//        {
//            return await _context.Chains
//                .FirstOrDefaultAsync(c => c.Id == id && c.IsActive == true);
//        }

//        public async Task<Chain?> GetByChainIdAsync(string chainId)
//        {
//            return await _context.Chains
//                .FirstOrDefaultAsync(c => c.ChainId == chainId && c.IsActive == true);
//        }

//        public async Task<Chain> AddAsync(Chain chain)
//        {
//            _context.Chains.Add(chain);
//            await _context.SaveChangesAsync();
//            return chain;
//        }

//        public async Task<Chain> UpdateAsync(Chain chain)
//        {
//            _context.Chains.Update(chain);
//            await _context.SaveChangesAsync();
//            return chain;
//        }
//        public async Task<IEnumerable<Chain>> GetChainsWithStoresAsync()
//        {
//            return await _context.Chains
//                .Include(c => c.Stores.Where(s => s.IsActive))
//                .Where(c => c.IsActive)
//                .ToListAsync();
//        }

//        public async Task<Chain?> GetChainWithStoresAsync(int id)
//        {
//            return await _context.Chains
//                .Include(c => c.Stores)
//                .FirstOrDefaultAsync(c => c.Id == id && c.IsActive);
//        }
//    }
//}
using Microsoft.EntityFrameworkCore;
using PriceComparison.Infrastructure.Models;
using PriceComparison.Infrastructure.Repositories;

public class ChainRepository : IChainRepository
{
    private readonly PriceComparisonDbContext _context;

    public ChainRepository(PriceComparisonDbContext context)
    {
        _context = context;
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

    public async Task<IEnumerable<Chain>> GetAllAsync()
    {
        return await _context.Chains
            .Where(c => c.IsActive == true)
            .OrderBy(c => c.ChainName)
            .ToListAsync();
    }

    public async Task<IEnumerable<Chain>> GetActiveAsync()
    {
        return await _context.Chains
            .Where(c => c.IsActive == true)
            .OrderBy(c => c.ChainName)
            .ToListAsync();
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

    public async Task<bool> SoftDeleteAsync(int id)
    {
        var chain = await _context.Chains.FindAsync(id);
        if (chain == null) return false;

        chain.IsActive = false;
        await _context.SaveChangesAsync();
        return true;
    }

    // Methods הנוספים שיש לך
    public async Task<IEnumerable<Chain>> GetChainsWithStoresAsync()
    {
        return await _context.Chains
            .Include(c => c.Stores.Where(s => s.IsActive == true))
            .Where(c => c.IsActive == true)
            .ToListAsync();
    }

    public async Task<Chain?> GetChainWithStoresAsync(int id)
    {
        return await _context.Chains
            .Include(c => c.Stores)
            .FirstOrDefaultAsync(c => c.Id == id && c.IsActive == true);
    }
}

/// <summary>
/// רפוזיטורי לקטגוריות - תיקון שגיאות
/// </summary>