using PriceComparison.Application.DTOs;

namespace PriceComparison.Application.Services
{
    public interface IAuthService
    {
        Task<LoginResponse> LoginAsync(LoginRequest request);
        Task<AuthResult> RegisterAsync(RegisterRequest request);
        string HashPassword(string password);
        bool VerifyPassword(string password, string hash);
    }
}