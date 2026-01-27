using Microsoft.AspNetCore.Mvc;

namespace PriceComparison.Api.Controllers
{
    // Step 1: קובץ ראשון להתחלה - TestLocalPriceController.cs
    // מקום: PriceComparison.Api/Controllers/

    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;

    namespace PriceComparison.Api.Controllers
    {
        /// <summary>
        /// Controller בסיסי לבדיקת המערכת הלוקלית - שלב 1
        /// </summary>
        [ApiController]
        [Route("api/[controller]")]
        public class TestLocalPriceController : ControllerBase
        {
            private readonly ILogger<TestLocalPriceController> _logger;

            public TestLocalPriceController(ILogger<TestLocalPriceController> logger)
            {
                _logger = logger;
            }

            /// <summary>
            /// בדיקה בסיסית - מחזיר נתונים הרדקודדים
            /// </summary>
            [HttpGet("test-search/{barcode}")]
            public ActionResult<object> TestSearch(string barcode)
            {
                _logger.LogInformation("קיבלתי בקשת בדיקה עבור ברקוד: {Barcode}", barcode);

                // נתונים הרדקודדים לבדיקה
                var testResult = new
                {
                    Success = true,
                    ProductName = "חלב תנובה 3% 1 ליטר",
                    Barcode = barcode,
                    ManufacturerName = "תנובה",
                    BestPrices = new[]
                    {
                    new
                    {
                        ProductName = "חלב תנובה 3% 1 ליטר",
                        Price = 5.50m,
                        ChainName = "רמי לוי",
                        StoreName = "רמי לוי תל אביב",
                        StoreAddress = "דיזנגוף 200 תל אביב",
                        IsMinPrice = true
                    },
                    new
                    {
                        ProductName = "חלב תנובה 3% 1 ליטר",
                        Price = 5.90m,
                        ChainName = "שוק העיר",
                        StoreName = "שוק העיר בני ברק",
                        StoreAddress = "הרב קוק 23 בני ברק",
                        IsMinPrice = false
                    },
                    new
                    {
                        ProductName = "חלב תנובה 3% 1 ליטר",
                        Price = 6.20m,
                        ChainName = "ויקטורי",
                        StoreName = "ויקטורי נתניה",
                        StoreAddress = "הרצל 88 נתניה",
                        IsMinPrice = false
                    }
                },
                    Statistics = new
                    {
                        MinPrice = 5.50m,
                        MaxPrice = 6.20m,
                        AveragePrice = 5.87m,
                        PriceDifference = 0.70m,
                        PercentageDifference = 12.7m,
                        StoreCount = 3,
                        ChainCount = 3
                    },
                    Message = "זהו נתון לבדיקה - עדיין לא נתונים אמיתיים"
                };

                return Ok(testResult);
            }

            /// <summary>
            /// בדיקת תקינות המערכת
            /// </summary>
            [HttpGet("health")]
            public ActionResult<object> HealthCheck()
            {
                _logger.LogInformation("בדיקת תקינות מערכת מקומית");

                return Ok(new
                {
                    Status = "Healthy",
                    Message = "המערכת המקומית פועלת",
                    Timestamp = DateTime.Now,
                    Version = "Test-1.0"
                });
            }

            /// <summary>
            /// רשימת ברקודים לבדיקה
            /// </summary>
            [HttpGet("test-barcodes")]
            public ActionResult<object> GetTestBarcodes()
            {
                var testBarcodes = new
                {
                    Message = "ברקודים לבדיקה",
                    Barcodes = new[]
                    {
                    new { Barcode = "7290000065007", ProductName = "חלב תנובה 3%" },
                    new { Barcode = "1234567890123", ProductName = "בננות" },
                    new { Barcode = "7290108171267", ProductName = "קוקה קולה 1.5L" },
                    new { Barcode = "7290000066001", ProductName = "לחם שחור" }
                }
                };

                return Ok(testBarcodes);
            }
        }
    }
}
