using Microsoft.Extensions.Logging;
using PriceComparison.Application.DTOs;
using PriceComparison.Infrastructure.Models;
using PriceComparison.Infrastructure.Repositories;
using System.Text.RegularExpressions;

namespace PriceComparison.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly ILogger<AuthService> _logger;

        public AuthService(IUserRepository userRepository, ILogger<AuthService> logger)
        {
            _userRepository = userRepository;
            _logger = logger;
        }

        public async Task<LoginResponse> LoginAsync(LoginRequest request)
        {
            try
            {
                _logger.LogInformation("🔐 Login attempt for: {Identifier}", request.LoginIdentifier);

                var user = await _userRepository.GetByLoginIdentifierAsync(request.LoginIdentifier);

                if (user == null)
                {
                    _logger.LogWarning("❌ User not found for identifier: {Identifier}", request.LoginIdentifier);
                    return new LoginResponse
                    {
                        Success = false,
                        Message = "פרטי התחברות שגויים"
                    };
                }

                if (!VerifyPassword(request.Password, user.PasswordHash))
                {
                    _logger.LogWarning("❌ Invalid password for user ID: {UserId}", user.Id);
                    return new LoginResponse
                    {
                        Success = false,
                        Message = "פרטי התחברות שגויים"
                    };
                }

                // עדכון זמן כניסה אחרון
                await _userRepository.UpdateLastLoginAsync(user.Id);

                _logger.LogInformation("✅ Successful login for user ID: {UserId}", user.Id);

                return new LoginResponse
                {
                    Success = true,
                    Message = "התחברות בוצעה בהצלחה",
                    User = new UserInfo
                    {
                        Id = user.Id,
                        Phone = user.Phone ?? "",
                        Email = user.Email ?? "",
                        FullName = user.FullName,
                        CreatedDate = user.CreatedDate,
                        LastLogin = DateTime.Now,
                        LoginIdentifier = user.GetLoginIdentifier()
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error during login for identifier: {Identifier}", request.LoginIdentifier);
                return new LoginResponse
                {
                    Success = false,
                    Message = "שגיאה בהתחברות"
                };
            }
        }

        public async Task<AuthResult> RegisterAsync(RegisterRequest request)
        {
            try
            {
                _logger.LogInformation("📝 Registration attempt for: {Phone}/{Email}", request.Phone, request.Email);

                var validation = ValidateRegistrationRequest(request);
                if (!validation.Success)
                {
                    _logger.LogWarning("❌ Validation failed: {Message}", validation.Message);
                    return validation;
                }

                if (!string.IsNullOrEmpty(request.Phone) && await _userRepository.PhoneExistsAsync(request.Phone))
                {
                    return new AuthResult
                    {
                        Success = false,
                        Message = "מספר הטלפון כבר רשום במערכת"
                    };
                }

                if (!string.IsNullOrEmpty(request.Email) && await _userRepository.EmailExistsAsync(request.Email))
                {
                    return new AuthResult
                    {
                        Success = false,
                        Message = "כתובת האימייל כבר רשומה במערכת"
                    };
                }

                var user = new User
                {
                    Phone = string.IsNullOrEmpty(request.Phone) ? null : request.Phone,
                    Email = string.IsNullOrEmpty(request.Email) ? null : request.Email,
                    FullName = request.FullName.Trim(),
                    PasswordHash = HashPassword(request.Password),
                    IsActive = true,
                    CreatedDate = DateTime.Now
                };

                await _userRepository.CreateAsync(user);

                _logger.LogInformation("✅ Successful registration for user ID: {UserId}", user.Id);

                return new AuthResult
                {
                    Success = true,
                    Message = "המשתמש נרשם בהצלחה!"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error during registration");
                return new AuthResult
                {
                    Success = false,
                    Message = "שגיאה ברישום המשתמש"
                };
            }
        }

        public string HashPassword(string password)
        {
            // BCrypt עם work factor 12 לאבטחה טובה
            return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
        }

        public bool VerifyPassword(string password, string hash)
        {
            try
            {
                return BCrypt.Net.BCrypt.Verify(password, hash);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying password");
                return false;
            }
        }

        private AuthResult ValidateRegistrationRequest(RegisterRequest request)
        {
            if (string.IsNullOrEmpty(request.Phone) && string.IsNullOrEmpty(request.Email))
            {
                return new AuthResult { Success = false, Message = "נדרש לפחות מספר טלפון או כתובת אימייל" };
            }

            if (string.IsNullOrEmpty(request.FullName) || request.FullName.Trim().Length < 2)
            {
                return new AuthResult { Success = false, Message = "שם מלא נדרש (לפחות 2 תווים)" };
            }

            if (string.IsNullOrEmpty(request.Password) || request.Password.Length < 6)
            {
                return new AuthResult { Success = false, Message = "סיסמה חייבת להכיל לפחות 6 תווים" };
            }

            // בדיקת פורמט טלפון ישראלי
            if (!string.IsNullOrEmpty(request.Phone) && !IsValidIsraeliPhone(request.Phone))
            {
                return new AuthResult { Success = false, Message = "פורמט מספר טלפון לא תקין" };
            }

            // בדיקת פורמט אימייל
            if (!string.IsNullOrEmpty(request.Email) && !IsValidEmail(request.Email))
            {
                return new AuthResult { Success = false, Message = "פורמט כתובת אימייל לא תקין" };
            }

            return new AuthResult { Success = true };
        }

        private bool IsValidIsraeliPhone(string phone)
        {
            // פורמטים תקינים: 0501234567, 050-123-4567, +972-50-123-4567
            var phonePattern = @"^(\+972|0)[-\s]?[5][0-9][-\s]?[0-9]{3}[-\s]?[0-9]{4}$";
            return Regex.IsMatch(phone, phonePattern);
        }

        private bool IsValidEmail(string email)
        {
            var emailPattern = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$";
            return Regex.IsMatch(email, emailPattern);
        }
    }
}