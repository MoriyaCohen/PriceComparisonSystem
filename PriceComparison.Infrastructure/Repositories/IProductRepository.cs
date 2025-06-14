﻿using PriceComparison.Infrastructure.Models;

namespace PriceComparison.Infrastructure.Repositories
{
    public interface IProductRepository
    {
        Task<IEnumerable<Product>> GetAllAsync();
        Task<Product?> GetByIdAsync(int id);
        Task<Product?> GetByBarcodeAsync(string barcode);
        Task<IEnumerable<Product>> GetByCategoryAsync(int categoryId);
        Task<IEnumerable<Product>> SearchByNameAsync(string productName);
        Task<Product?> GetByProductIdAsync(string productId);
        Task<Product> AddAsync(Product product);
        Task<Product> UpdateAsync(Product product);
    }
}