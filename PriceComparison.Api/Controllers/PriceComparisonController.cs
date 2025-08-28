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
        private readonly ILocalXmlSearchService _localXmlSearchService;
        private readonly ILogger<PriceComparisonController> _logger;

        public PriceComparisonController(
            IPriceComparisonService priceComparisonService,
            IBarcodeValidationService barcodeValidationService,
            ILocalXmlSearchService localXmlSearchService,
            ILogger<PriceComparisonController> logger)
        {
            _priceComparisonService = priceComparisonService;
            _barcodeValidationService = barcodeValidationService;
            _localXmlSearchService = localXmlSearchService;
            _logger = logger;
        }

        /// <summary>
        /// חיפוש מוצר לפי ברקוד במסד הנתונים (הפונקציונליות הקיימת)
        /// </summary>
        [HttpPost("search")]
        public async Task<ActionResult<PriceComparisonResponseDto>> SearchProductByBarcode([FromBody] PriceComparisonRequestDto request)
        {
            try
            {
                _logger.LogInformation("מתחיל חיפוש מוצר במסד נתונים עבור ברקוד: {Barcode}", request.Barcode);

                // בדיקת תקינות בסיסית
                if (string.IsNullOrWhiteSpace(request.Barcode))
                {
                    return BadRequest(new PriceComparisonResponseDto
                    {
                        Success = false,
                        ErrorMessage = "ברקוד לא יכול להיות ריק",
                        PriceDetails = new List<ProductPriceInfoDto>()
                    });
                }

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

                // שלב 2: חיפוש מוצר והשוואת מחירים במסד הנתונים
                var normalizedBarcode = validationResult.NormalizedBarcode ?? request.Barcode;
                var searchResult = await _priceComparisonService.SearchProductByBarcodeAsync(normalizedBarcode);

                _logger.LogInformation("תוצאות חיפוש במסד נתונים עבור ברקוד {Barcode}: {Success}, {ProductCount} מוצרים",
                    request.Barcode, searchResult.Success, searchResult.PriceDetails?.Count ?? 0);

                return Ok(searchResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "שגיאה לא צפויה בחיפוש מוצר במסד נתונים: {Barcode}", request.Barcode);
                return StatusCode(500, new PriceComparisonResponseDto
                {
                    Success = false,
                    ErrorMessage = "שגיאה פנימית בשרת",
                    PriceDetails = new List<ProductPriceInfoDto>()
                });
            }
        }

        /// <summary>
        /// חיפוש מוצר לפי ברקוד בקבצי XML מקומיים - נדרש לפרונטאנד
        /// </summary>
        [HttpPost("search-local")]
        public async Task<ActionResult<PriceComparisonResponseDto>> SearchProductByBarcodeLocal([FromBody] BarcodeSearchRequestDto request)
        {
            try
            {
                _logger.LogInformation("מתחיל חיפוש מקומי עבור ברקוד: {Barcode}", request.Barcode);

                // בדיקת תקינות בסיסית
                if (string.IsNullOrWhiteSpace(request.Barcode))
                {
                    return BadRequest(new PriceComparisonResponseDto
                    {
                        Success = false,
                        ErrorMessage = "ברקוד לא יכול להיות ריק",
                        PriceDetails = new List<ProductPriceInfoDto>()
                    });
                }

                // שלב 1: בדיקת תקינות ברקוד
                var validationResult = await _barcodeValidationService.ValidateBarcodeAsync(request.Barcode);
                if (!validationResult.IsValid)
                {
                    _logger.LogWarning("חיפוש מקומי - ברקוד לא תקין: {Barcode}, שגיאה: {Error}",
                        request.Barcode, validationResult.ErrorMessage);

                    return BadRequest(new PriceComparisonResponseDto
                    {
                        Success = false,
                        ErrorMessage = validationResult.ErrorMessage ?? "ברקוד לא תקין",
                        PriceDetails = new List<ProductPriceInfoDto>()
                    });
                }

                // שלב 2: חיפוש מוצר בקבצי XML המקומיים
                var normalizedBarcode = validationResult.NormalizedBarcode ?? request.Barcode;
                var searchResult = await _localXmlSearchService.SearchByBarcodeAsync(normalizedBarcode);

                _logger.LogInformation("תוצאות חיפוש מקומי עבור ברקוד {Barcode}: {Success}, {ProductCount} מוצרים",
                    request.Barcode, searchResult.Success, searchResult.PriceDetails?.Count ?? 0);

                return Ok(searchResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "שגיאה לא צפויה בחיפוש מקומי: {Barcode}", request.Barcode);
                return StatusCode(500, new PriceComparisonResponseDto
                {
                    Success = false,
                    ErrorMessage = "שגיאה פנימית בשרת",
                    PriceDetails = new List<ProductPriceInfoDto>()
                });
            }
        }

        /// <summary>
        /// קבלת מצב נתוני XML המקומיים - נדרש לפרונטאנד
        /// </summary>
        [HttpGet("local-data-status")]
        public async Task<ActionResult<LocalDataStatusDto>> GetLocalDataStatus()
        {
            try
            {
                _logger.LogInformation("מקבל מצב נתונים מקומיים");

                var status = await _localXmlSearchService.GetDataStatusAsync();

                _logger.LogInformation("מצב נתונים מקומיים: {IsAvailable}, {ProductCount} מוצרים, {ChainCount} רשתות",
                    status.IsDataAvailable, status.TotalProducts, status.LoadedChains);

                return Ok(status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "שגיאה בקבלת מצב נתונים מקומיים");
                return StatusCode(500, new LocalDataStatusDto
                {
                    IsDataAvailable = false,
                    StatusMessage = "שגיאה בקבלת מצב הנתונים",
                    LoadedChains = 0,
                    LoadedStores = 0,
                    TotalProducts = 0,
                    LastRefresh = DateTime.MinValue
                });
            }
        }

        /// <summary>
        /// רענון נתוני XML מקומיים - נדרש לפרונטאנד
        /// </summary>
        [HttpPost("refresh-local-data")]
        public async Task<ActionResult<bool>> RefreshLocalData()
        {
            try
            {
                _logger.LogInformation("מתחיל רענון נתונים מקומיים");

                var success = await _localXmlSearchService.RefreshDataAsync();

                if (success)
                {
                    _logger.LogInformation("רענון נתונים מקומיים הצליח");
                    return Ok(true);
                }
                else
                {
                    _logger.LogWarning("רענון נתונים מקומיים נכשל");
                    return Ok(false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "שגיאה ברענון נתונים מקומיים");
                return StatusCode(500, false);
            }
        }

        /// <summary>
        /// קבלת סטטיסטיקות מחירים למוצר ספציפי
        /// </summary>
        [HttpGet("statistics/{barcode}")]
        public async Task<ActionResult<PriceStatisticsDto?>> GetPriceStatistics(string barcode)
        {
            try
            {
                _logger.LogInformation("מקבל סטטיסטיקות מחירים עבור ברקוד: {Barcode}", barcode);

                var statistics = await _priceComparisonService.GetPriceStatisticsAsync(barcode);

                return Ok(statistics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "שגיאה בקבלת סטטיסטיקות מחירים: {Barcode}", barcode);
                return StatusCode(500, "שגיאה בקבלת סטטיסטיקות");
            }
        }
    }
}