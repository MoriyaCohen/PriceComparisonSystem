using PriceComparison.Infrastructure.Models;
using PriceComparison.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

//public class CategoryRepository : ICategoryRepository
//{
//    private readonly PriceComparisonDbContext _context;

//    public CategoryRepository(PriceComparisonDbContext context)
//    {
//        _context = context;
//    }

//    public async Task<Category?> GetByIdAsync(int id)
//    {
//        return await _context.Categories
//            .Include(c => c.Children)
//            .Include(c => c.Parent)
//            .FirstOrDefaultAsync(c => c.Id == id);
//    }

//    public async Task<IEnumerable<Category>> GetAllAsync()
//    {
//        return await _context.Categories
//            .Where(c => c.IsActive)
//            .Include(c => c.Children)
//            .ToListAsync();
//    }

//    public async Task<IEnumerable<Category>> GetRootCategoriesAsync()
//    {
//        return await _context.Categories
//            .Where(c => c.ParentId == null && c.IsActive)
//            .Include(c => c.Children)
//            .ToListAsync();
//    }

//    public async Task<IEnumerable<Category>> GetSubCategoriesAsync(int parentId)
//    {
//        return await _context.Categories
//            .Where(c => c.ParentId == parentId && c.IsActive)
//            .Include(c => c.Children)
//            .ToListAsync();
//    }

//    public async Task<Category> AddAsync(Category category)
//    {
//        _context.Categories.Add(category);
//        await _context.SaveChangesAsync();
//        return category;
//    }

//    public async Task<Category> UpdateAsync(Category category)
//    {
//        _context.Categories.Update(category);
//        await _context.SaveChangesAsync();
//        return category;
//    }

//    public async Task<bool> SoftDeleteAsync(int id)
//    {
//        var category = await GetByIdAsync(id);
//        if (category == null) return false;

//        category.IsActive = false;
//        await _context.SaveChangesAsync();
//        return true;
//    }
//}
public class CategoryRepository : ICategoryRepository
{
    private readonly PriceComparisonDbContext _context;

    public CategoryRepository(PriceComparisonDbContext context)
    {
        _context = context;
    }

    public async Task<Category?> GetByIdAsync(int id)
    {
        return await _context.Categories
            .Include(c => c.Parent)
            .Include(c => c.InverseParent) // זה תחליף ל-Children
            .FirstOrDefaultAsync(c => c.Id == id && c.IsActive == true);
    }

    public async Task<IEnumerable<Category>> GetAllAsync()
    {
        return await _context.Categories
            .Include(c => c.Parent)
            .Where(c => c.IsActive == true)
            .OrderBy(c => c.Level)
            .ThenBy(c => c.CategoryName)
            .ToListAsync();
    }

    public async Task<IEnumerable<Category>> GetRootCategoriesAsync()
    {
        return await _context.Categories
            .Where(c => c.ParentId == null && c.IsActive == true)
            .OrderBy(c => c.CategoryName)
            .ToListAsync();
    }

    public async Task<IEnumerable<Category>> GetSubCategoriesAsync(int parentId)
    {
        return await _context.Categories
            .Where(c => c.ParentId == parentId && c.IsActive == true)
            .OrderBy(c => c.CategoryName)
            .ToListAsync();
    }

    public async Task<Category> AddAsync(Category category)
    {
        _context.Categories.Add(category);
        await _context.SaveChangesAsync();
        return category;
    }

    public async Task<Category> UpdateAsync(Category category)
    {
        _context.Categories.Update(category);
        await _context.SaveChangesAsync();
        return category;
    }

    public async Task<bool> SoftDeleteAsync(int id)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category == null) return false;

        category.IsActive = false;
        await _context.SaveChangesAsync();
        return true;
    }
}
