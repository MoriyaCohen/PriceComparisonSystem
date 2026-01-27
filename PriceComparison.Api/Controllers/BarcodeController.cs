using Microsoft.AspNetCore.Mvc;
using PriceComparison.Application.DTOs;
using PriceComparison.Application.Services;

namespace PriceComparison.Api.Controllers
{
    /// <summary>
    /// קונטרולר לטיפול בבדיקת תקינות ברקודים
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class BarcodeController : ControllerBase
    {
        private readonly IBarcodeValidationService _barcodeService;
        private readonly ILogger<BarcodeController> _logger;

        public BarcodeController(
            IBarcodeValidationService barcodeService,
            ILogger<BarcodeController> logger)
        {
            _barcodeService = barcodeService;
            _logger = logger;
        }

        /// <summary>
        /// בדיקת תקינות ברקוד
        /// </summary>
        /// <param name="request">בקשת בדיקת ברקוד</param>
        /// <returns>תוצאת בדיקת התקינות</returns>
        [HttpPost("validate")]
        public async Task<ActionResult<BarcodeValidationResponseDto>> ValidateBarcode([FromBody] BarcodeValidationRequestDto request)
        {
            try
            {
                _logger.LogInformation("מתחיל בדיקת ברקוד: {Barcode}", request.Barcode);

                // בדיקות בסיסיות
                if (string.IsNullOrWhiteSpace(request.Barcode))
                {
                    _logger.LogWarning("ברקוד ריק נשלח לבדיקה");
                    return BadRequest(new BarcodeValidationResponseDto
                    {
                        IsValid = false,
                        ErrorMessage = "ברקוד לא יכול להיות ריק"
                    });
                }

                // בדיקת התקינות בשירות
                var result = await _barcodeService.ValidateBarcodeAsync(request.Barcode);

                _logger.LogInformation("תוצאת בדיקת ברקוד {Barcode}: {IsValid}",
                    request.Barcode, result.IsValid);

                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "ברקוד לא תקין: {Barcode}", request.Barcode);
                return BadRequest(new BarcodeValidationResponseDto
                {
                    IsValid = false,
                    ErrorMessage = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "שגיאה לא צפויה בבדיקת ברקוד: {Barcode}", request.Barcode);
                return StatusCode(500, new BarcodeValidationResponseDto
                {
                    IsValid = false,
                    ErrorMessage = "שגיאה פנימית בשרת"
                });
            }
        }

        /// <summary>
        /// בדיקה מהירה של פורמט ברקוד (ללא חיבור למסד נתונים)
        /// </summary>
        /// <param name="barcode">ברקוד לבדיקה</param>
        /// <returns>האם הפורמט תקין</returns>
        [HttpGet("validate-format/{barcode}")]
        public ActionResult<bool> ValidateBarcodeFormat(string barcode)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(barcode))
                {
                    return BadRequest("ברקוד לא יכול להיות ריק");
                }

                var isValidFormat = _barcodeService.IsValidBarcodeFormat(barcode);
                _logger.LogInformation("בדיקת פורמט ברקוד {Barcode}: {IsValid}", barcode, isValidFormat);

                return Ok(isValidFormat);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "שגיאה בבדיקת פורמט ברקוד: {Barcode}", barcode);
                return StatusCode(500, "שגיאה פנימית בשרת");
            }
        }
    }
}