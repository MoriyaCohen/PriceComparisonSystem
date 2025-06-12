using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PriceComparison.Infrastructure.Models;

namespace PriceComparison.Infrastructure.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly PriceComparisonDbContext _context;
        private readonly ILogger<UserRepository> _logger;

        public UserRepository(PriceComparisonDbContext context, ILogger<UserRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<User?> GetByLoginIdentifierAsync(string identifier)
        {
            try
            {
                _logger.LogDebug("🔍 Searching for user with identifier: {Identifier}", identifier);

                var user = await _context.Users
                    .Where(u => u.IsActive &&
                               ((u.Phone != null && u.Phone == identifier) ||
                                (u.Email != null && u.Email == identifier)))
                    .FirstOrDefaultAsync();

                _logger.LogDebug(user != null ? "✅ User found" : "❌ User not found");
                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error searching for user with identifier: {Identifier}", identifier);
                throw;
            }
        }

        public async Task<User?> GetByIdAsync(int id)
        {
            return await _context.Users
                .Where(u => u.Id == id && u.IsActive)
                .FirstOrDefaultAsync();
        }

        public async Task<User> CreateAsync(User user)
        {
            try
            {
                _logger.LogDebug("📝 Creating new user: {FullName}", user.FullName);

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ User created successfully with ID: {UserId}", user.Id);
                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error creating user: {FullName}", user.FullName);
                throw;
            }
        }

        public async Task UpdateLastLoginAsync(int userId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user != null)
                {
                    user.LastLogin = DateTime.Now;
                    await _context.SaveChangesAsync();
                    _logger.LogDebug("🕒 Updated last login for user ID: {UserId}", userId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error updating last login for user ID: {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> PhoneExistsAsync(string phone)
        {
            if (string.IsNullOrEmpty(phone)) return false;

            return await _context.Users
                .AnyAsync(u => u.Phone == phone);
        }

        public async Task<bool> EmailExistsAsync(string email)
        {
            if (string.IsNullOrEmpty(email)) return false;

            return await _context.Users
                .AnyAsync(u => u.Email == email);
        }

        public async Task DeactivateUserAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                user.IsActive = false;
                await _context.SaveChangesAsync();
                _logger.LogInformation("🚫 User deactivated: {UserId}", userId);
            }
        }
    }
}