using Microsoft.EntityFrameworkCore;
using PriceComparison.Infrastructure.Models;

namespace PriceComparison.Infrastructure.Repositories
{
    public class ProductRepository : IProductRepository
    {
        private readonly PriceComparisonDbContext _context;

        public ProductRepository(PriceComparisonDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Product>> GetAllAsync()
        {
            return await _context.Products
                .Where(p => p.IsActive == true)
                .OrderBy(p => p.ProductName)
                .ToListAsync();
        }

        public async Task<Product?> GetByIdAsync(int id)
        {
            return await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.Id == id && p.IsActive == true);
        }

        public async Task<Product?> GetByBarcodeAsync(string barcode)
        {
            return await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.Barcode == barcode && p.IsActive == true);
        }

        public async Task<IEnumerable<Product>> GetByCategoryAsync(int categoryId)
        {
            return await _context.Products
                .Include(p => p.Category)
                .Where(p => p.CategoryId == categoryId && p.IsActive == true)
                .OrderBy(p => p.ProductName)
                .ToListAsync();
        }

        public async Task<IEnumerable<Product>> SearchByNameAsync(string productName)
        {
            return await _context.Products
                .Include(p => p.Category)
                .Where(p => p.ProductName.Contains(productName) && p.IsActive == true)
                .OrderBy(p => p.ProductName)
                .ToListAsync();
        }

        public async Task<Product?> GetByProductIdAsync(string productId)
        {
            return await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.ProductId == productId && p.IsActive == true);
        }

        public async Task<Product> AddAsync(Product product)
        {
            _context.Products.Add(product);
            await _context.SaveChangesAsync();
            return product;
        }

        public async Task<Product> UpdateAsync(Product product)
        {
            _context.Products.Update(product);
            await _context.SaveChangesAsync();
            return product;
        }
        public async Task<IEnumerable<Product>> GetProductsWithPricesAsync()
        {
            return await _context.Products
                .Include(p => p.StorePrices)
                .ThenInclude(sp => sp.Store)
                .Where(p => p.IsActive)
                .ToListAsync();
        }

        public async Task<Product?> GetProductWithCategoryAsync(int id)
        {
            return await _context.Products
                .Include(p => p.Category)
                .Include(p => p.StorePrices)
                .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);
        }

        public async Task<bool> ExistsAsync(string barcode)
        {
            return await _context.Products
                .AnyAsync(p => p.Barcode == barcode && p.IsActive);
        }
    }
    //public async Task<bool> SoftDeleteAsync(int id)
    //{
    //    var product = await _context.Products.FindAsync(id);
    //    if (product == null) return false;

    //    product.IsActive = false;
    //    await _context.SaveChangesAsync();
    //    return true;
    //}
}