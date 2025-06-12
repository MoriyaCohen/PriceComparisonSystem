using PriceComparison.Infrastructure.Models;

namespace PriceComparison.Infrastructure.Repositories
{
    public interface IUserRepository
    {
        Task<User?> GetByLoginIdentifierAsync(string identifier);
        Task<User?> GetByIdAsync(int id);
        Task<User> CreateAsync(User user);
        Task UpdateLastLoginAsync(int userId);
        Task<bool> PhoneExistsAsync(string phone);
        Task<bool> EmailExistsAsync(string email);
        Task DeactivateUserAsync(int userId);
    }
}