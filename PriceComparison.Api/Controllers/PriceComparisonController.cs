
using Microsoft.AspNetCore.Mvc;
using PriceComparison.Application.DTOs;
using PriceComparison.Application.Services;

namespace PriceComparison.Api.Controllers
{
    /// <summary>
    /// קונטרולר לטיפול בהשוואת מחירים
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class PriceComparisonController : ControllerBase
    {
        private readonly IPriceComparisonService _priceComparisonService;
        private readonly IBarcodeValidationService _barcodeValidationService;
        private readonly ILogger<PriceComparisonController> _logger;

        public PriceComparisonController(
            IPriceComparisonService priceComparisonService,
            IBarcodeValidationService barcodeValidationService,
            ILogger<PriceComparisonController> logger)
        {
            _priceComparisonService = priceComparisonService;
            _barcodeValidationService = barcodeValidationService;
            _logger = logger;
        }

        /// <summary>
        /// חיפוש מוצר לפי ברקוד והשוואת מחירים
        /// </summary>
        /// <param name="request">בקשת חיפוש מוצר</param>
        /// <returns>תוצאות השוואת מחירים</returns>
        [HttpPost("search")]
        public async Task<ActionResult<PriceComparisonResponseDto>> SearchProductByBarcode([FromBody] PriceComparisonRequestDto request)
        {
            try
            {
                _logger.LogInformation("מתחיל חיפוש מוצר עבור ברקוד: {Barcode}", request.Barcode);

                // שלב 1: בדיקת תקינות ברקוד
                var validationResult = await _barcodeValidationService.ValidateBarcodeAsync(request.Barcode);
                if (!validationResult.IsValid)
                {
                    _logger.LogWarning("ברקוד לא תקין: {Barcode}, שגיאה: {Error}",
                        request.Barcode, validationResult.ErrorMessage);

                    return BadRequest(new PriceComparisonResponseDto
                    {
                        Success = false,
                        ErrorMessage = validationResult.ErrorMessage ?? "ברקוד לא תקין",
                        PriceDetails = new List<ProductPriceInfoDto>()
                    });
                }

                // שלב 2: חיפוש מוצר והשוואת מחירים
                var searchResult = await _priceComparisonService.SearchProductByBarcodeAsync(validationResult.NormalizedBarcode);

                _logger.LogInformation("תוצאות חיפוש עבור ברקוד {Barcode}: {Success}, {ProductCount} מוצרים",
                    request.Barcode, searchResult.Success, searchResult.PriceDetails?.Count ?? 0);

                // החזרת תוצאות
                if (searchResult.Success)
                {
                    return Ok(searchResult);
                }
                else
                {
                    return NotFound(searchResult);
                }
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "פרמטר לא תקין בחיפוש: {Barcode}", request.Barcode);
                return BadRequest(new PriceComparisonResponseDto
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    PriceDetails = new List<ProductPriceInfoDto>()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "שגיאה לא צפויה בחיפוש מוצר: {Barcode}", request.Barcode);
                return StatusCode(500, new PriceComparisonResponseDto
                {
                    Success = false,
                    ErrorMessage = "שגיאה פנימית בשרת",
                    PriceDetails = new List<ProductPriceInfoDto>()
                });
            }
        }

        /// <summary>
        /// קבלת סטטיסטיקות מחירים עבור ברקוד ספציפי
        /// </summary>
        /// <param name="barcode">ברקוד המוצר</param>
        /// <returns>סטטיסטיקות מחירים</returns>
        [HttpGet("statistics/{barcode}")]
        public async Task<ActionResult<PriceStatisticsDto>> GetPriceStatistics(string barcode)
        {
            try
            {
                _logger.LogInformation("מחשב סטטיסטיקות מחירים עבור ברקוד: {Barcode}", barcode);

                // בדיקת תקינות ברקוד
                var validationResult = await _barcodeValidationService.ValidateBarcodeAsync(barcode);
                if (!validationResult.IsValid)
                {
                    return BadRequest(validationResult.ErrorMessage);
                }

                // חישוב סטטיסטיקות
                var statistics = await _priceComparisonService.GetPriceStatisticsAsync(validationResult.NormalizedBarcode);

                if (statistics == null)
                {
                    return NotFound("מוצר לא נמצא במערכת");
                }

                return Ok(statistics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "שגיאה בחישוב סטטיסטיקות עבור ברקוד: {Barcode}", barcode);
                return StatusCode(500, "שגיאה פנימית בשרת");
            }
        }

        /// <summary>
        /// קבלת המחיר הזול ביותר עבור ברקוד
        /// </summary>
        /// <param name="barcode">ברקוד המוצר</param>
        /// <returns>פרטי המחיר הזול ביותר</returns>
        [HttpGet("cheapest/{barcode}")]
        public async Task<ActionResult<ProductPriceInfoDto>> GetCheapestPrice(string barcode)
        {
            try
            {
                _logger.LogInformation("מחפש מחיר זול ביותר עבור ברקוד: {Barcode}", barcode);

                var cheapestPrice = await _priceComparisonService.GetCheapestPriceAsync(barcode);

                if (cheapestPrice == null)
                {
                    return NotFound("מוצר לא נמצא במערכת");
                }

                return Ok(cheapestPrice);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "שגיאה בחיפוש מחיר זול ביותר עבור ברקוד: {Barcode}", barcode);
                return StatusCode(500, "שגיאה פנימית בשרת");
            }
        }
    }
}
