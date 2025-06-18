using Microsoft.EntityFrameworkCore;
using PriceComparison.Infrastructure.Models;

namespace PriceComparison.Infrastructure.Repositories
{
    public class StorePriceRepository : IStorePriceRepository
    {
        private readonly PriceComparisonDbContext _context;

        public StorePriceRepository(PriceComparisonDbContext context)
        {
            _context = context;
        }

        public async Task<StorePrice?> GetByStoreAndProductAsync(int storeId, int productId)
        {
            return await _context.StorePrices
                .FirstOrDefaultAsync(sp => sp.StoreId == storeId && sp.ProductId == productId);
        }

        public async Task<IEnumerable<StorePrice>> GetPricesByBarcodeAsync(string barcode)
        {
            return await _context.StorePrices
                .Include(sp => sp.Store)
                    .ThenInclude(s => s.Chain)
                .Include(sp => sp.Product)
                .Where(sp => sp.Product.Barcode == barcode && sp.Product.IsActive == true)
                .OrderBy(sp => sp.CurrentPrice)
                .ToListAsync();
        }

        public async Task<IEnumerable<StorePrice>> GetPricesByProductIdAsync(int productId)
        {
            return await _context.StorePrices
                .Include(sp => sp.Store)
                    .ThenInclude(s => s.Chain)
                .Where(sp => sp.ProductId == productId)
                .OrderBy(sp => sp.CurrentPrice)
                .ToListAsync();
        }

        public async Task<StorePrice> AddAsync(StorePrice storePrice)
        {
            _context.StorePrices.Add(storePrice);
            await _context.SaveChangesAsync();
            return storePrice;
        }

        public async Task<StorePrice> UpdateAsync(StorePrice storePrice)
        {
            _context.StorePrices.Update(storePrice);
            await _context.SaveChangesAsync();
            return storePrice;
        }
        public async Task<IEnumerable<StorePrice>> GetLatestPricesAsync()
        {
            return await _context.StorePrices
                .Include(sp => sp.Store)
                .Include(sp => sp.Product)
                .Where(sp => sp.LastUpdated >= DateTime.Now.AddDays(-7))
                .OrderByDescending(sp => sp.LastUpdated)
                .ToListAsync();
        }

        public async Task<StorePrice?> GetLowestPriceForProductAsync(int productId)
        {
            return await _context.StorePrices
                .Include(sp => sp.Store)
                .Where(sp => sp.ProductId == productId)
                .OrderBy(sp => sp.CurrentPrice)
                .FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<StorePrice>> GetPriceComparisonAsync(string barcode)
        {
            return await _context.StorePrices
                .Include(sp => sp.Store)
                .ThenInclude(s => s.Chain)
                .Include(sp => sp.Product)
                .Where(sp => sp.Product.Barcode == barcode)
                .OrderBy(sp => sp.CurrentPrice)
                .ToListAsync();
        }
    }
}