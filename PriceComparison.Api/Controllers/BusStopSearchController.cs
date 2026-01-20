using Microsoft.AspNetCore.Mvc;
using PriceComparison.Api.Services; // לוודא שיש Using לתיקיית הסרוויסים
using System;
using System.Threading.Tasks;

namespace PriceComparison.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BusStopSearchController : ControllerBase
    {
        private readonly IBusStopService _busStopService;
        private readonly IStoreService _storeService;

        // הזרקת תלויות (Dependency Injection)
        public BusStopSearchController(IBusStopService busStopService, IStoreService storeService)
        {
            _busStopService = busStopService;
            _storeService = storeService;
        }

        [HttpGet("FindStores")]
        public async Task<IActionResult> FindStores(string stopId, double radiusKm)
        {
            try
            {
                // שלב 1: מציאת מיקום התחנה
                var stopLocation = _busStopService.GetStopLocation(stopId);

                if (stopLocation == null)
                {
                    return NotFound($"תחנה מספר {stopId} לא נמצאה או שקובץ התחנות חסר.");
                }

                // שלב 2: חיפוש חנויות לפי המיקום
                var stores = await _storeService.FindNearbyStores(
                    stopLocation.Value.Lat,
                    stopLocation.Value.Lon,
                    radiusKm
                );

                if (stores.Count == 0)
                {
                    return Ok("לא נמצאו סניפים בטווח המבוקש.");
                }

                return Ok(stores);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Error = "קרתה שגיאה פנימית בשרת",
                    Message = ex.Message
                });
            }
        }
    }
}