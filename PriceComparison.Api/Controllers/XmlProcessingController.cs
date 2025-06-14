using Microsoft.AspNetCore.Mvc;
using PriceComparison.Application.DTOs;
using PriceComparison.Application.Services;

namespace PriceComparison.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class XmlProcessingController : ControllerBase
    {
        private readonly IXmlProcessingService _xmlProcessingService;
        private readonly ILogger<XmlProcessingController> _logger;

        public XmlProcessingController(
            IXmlProcessingService xmlProcessingService,
            ILogger<XmlProcessingController> logger)
        {
            _xmlProcessingService = xmlProcessingService;
            _logger = logger;
        }

        [HttpGet("test")]
        public IActionResult Test()
        {
            _logger.LogInformation("Test endpoint called!");
            return Ok(new
            {
                message = "XmlProcessing Controller is working!",
                timestamp = DateTime.Now,
                version = "2.0 - Real Database Integration"
            });
        }

        [HttpPost("upload-from-frontend")]
        public async Task<IActionResult> UploadFromFrontend([FromBody] XmlUploadRequest request)
        {
            try
            {
                _logger.LogInformation("Received XML data from frontend with {ItemCount} items", request?.Items?.Count ?? 0);

                if (request == null)
                    return BadRequest(new { success = false, message = "נתונים לא תקינים" });

                // בדיקת תקינות נתונים
                if (!await _xmlProcessingService.ValidateXmlDataAsync(request))
                    return BadRequest(new { success = false, message = "נתוני XML לא תקינים" });

                // עיבוד אמיתי של הנתונים
                var result = await _xmlProcessingService.ProcessXmlDataAsync(request);

                if (!result.Success)
                    return BadRequest(new { success = false, message = result.ErrorMessage });

                return Ok(new
                {
                    success = true,
                    message = "הנתונים נשמרו בהצלחה במסד הנתונים!",
                    data = new
                    {
                        totalItems = result.ProcessedItems,
                        newItems = result.NewItems,
                        updatedItems = result.UpdatedItems,
                        storeInfo = result.StoreInfo,
                        taskId = result.TaskId,
                        processedAt = result.ProcessedAt
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing XML data from frontend");
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה פנימית בשרת: " + ex.Message
                });
            }
        }
    }
}