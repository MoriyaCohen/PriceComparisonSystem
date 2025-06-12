using Microsoft.AspNetCore.Mvc;
using PriceComparison.Application.DTOs;
using PriceComparison.Application.Services;

namespace PriceComparison.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IAuthService authService, ILogger<AuthController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                _logger.LogInformation("🔐 Login endpoint called for: {Identifier}", request?.LoginIdentifier);

                if (request == null || string.IsNullOrEmpty(request.LoginIdentifier) || string.IsNullOrEmpty(request.Password))
                {
                    return BadRequest(new LoginResponse
                    {
                        Success = false,
                        Message = "נדרש מספר טלפון/אימייל וסיסמה"
                    });
                }

                var result = await _authService.LoginAsync(request);

                if (result.Success)
                {
                    _logger.LogInformation("✅ Login endpoint success for: {Identifier}", request.LoginIdentifier);
                    return Ok(result);
                }
                else
                {
                    _logger.LogWarning("❌ Login endpoint failed for: {Identifier}, Message: {Message}",
                        request.LoginIdentifier, result.Message);
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Unhandled error in login endpoint");
                return StatusCode(500, new LoginResponse
                {
                    Success = false,
                    Message = "שגיאה פנימית בשרת"
                });
            }
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            try
            {
                _logger.LogInformation("📝 Register endpoint called for: {Phone}/{Email}", request?.Phone, request?.Email);

                if (request == null)
                {
                    return BadRequest(new AuthResult
                    {
                        Success = false,
                        Message = "נתונים לא תקינים"
                    });
                }

                var result = await _authService.RegisterAsync(request);

                if (result.Success)
                {
                    _logger.LogInformation("✅ Registration endpoint success for: {Phone}/{Email}", request.Phone, request.Email);
                    return Ok(result);
                }
                else
                {
                    _logger.LogWarning("❌ Registration endpoint failed: {Message}", result.Message);
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Unhandled error in register endpoint");
                return StatusCode(500, new AuthResult
                {
                    Success = false,
                    Message = "שגיאה פנימית בשרת"
                });
            }
        }

        [HttpGet("test")]
        public IActionResult Test()
        {
            _logger.LogInformation("🧪 Test endpoint called");
            return Ok(new
            {
                message = "Auth Controller is working!",
                timestamp = DateTime.Now,
                version = "Simple Auth v1.0",
                status = "✅ Ready"
            });
        }
    }
}