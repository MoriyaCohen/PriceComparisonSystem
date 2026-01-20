using Microsoft.AspNetCore.Mvc;
using PriceComparison.Api.Services;
using PriceComparison.Application.DTOs;
using PriceComparison.Api.DTOs; // לוודא שיש גישה ל-NearbyStoreDto
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace PriceComparison.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SmartShoppingController : ControllerBase
    {
        // --- תלויות (Services) ---
        private readonly ILogger<SmartShoppingController> _logger;
        private readonly IBusStopService _busStopService;
        private readonly IStoreService _storeService;

        // --- נתיבים לקבצי XML ---
        private const string BaseFolder = @"C:\Users\ADMIN\projects\LocalXmlData";
        private string PriceDir => Path.Combine(BaseFolder, "PRICEFULL");
        private string StoresFullDir => Path.Combine(BaseFolder, "STORESFULL");

        // --- זיכרון מטמון למידע על חנויות ---
        private readonly Dictionary<string, (string ChainName, string StoreName, string Address, string City)> _storeData = new();

        public SmartShoppingController(
            ILogger<SmartShoppingController> logger,
            IBusStopService busStopService,
            IStoreService storeService)
        {
            _logger = logger;
            _busStopService = busStopService;
            _storeService = storeService;

            LoadStoreData();
        }

        [HttpGet("GetCheapestByStop")]
        public async Task<IActionResult> GetCheapestByStop(
            string stopId,
            double radiusKm,
            string barcode,
            [FromQuery] bool showClub = false,
            [FromQuery] bool showQuantity = false)
        {
            try
            {
                // שלב 1: מציאת מיקום התחנה
                var stopLocation = _busStopService.GetStopLocation(stopId);
                if (stopLocation == null)
                {
                    return NotFound($"תחנה מספר {stopId} לא נמצאה.");
                }

                // שלב 2: מציאת חנויות ברדיוס
                var nearbyStores = await _storeService.FindNearbyStores(
                    stopLocation.Value.Lat,
                    stopLocation.Value.Lon,
                    radiusKm
                );

                if (nearbyStores == null || nearbyStores.Count == 0)
                {
                    return Ok("לא נמצאו סניפים בטווח המבוקש מהתחנה.");
                }

                // שלב 3: יצירת מפתחות לחנויות הרלוונטיות
                var validStoreKeys = new HashSet<string>();

                foreach (var store in nearbyStores)
                {
                    // תיקון: שימוש ב-Null Coalescing כדי למנוע קריסה אם הנתון חסר
                    string cId = store.ChainId?.ToString() ?? "0";
                    string sId = store.StoreId?.ToString().TrimStart('0') ?? "0";

                    validStoreKeys.Add($"{cId}-{sId}");
                }

                // שלב 4: חיפוש המוצר בקבצי המחירים
                if (!Directory.Exists(PriceDir))
                    return BadRequest("תיקיית המחירים חסרה בשרת.");

                var results = new List<ProductData>();
                var priceFiles = Directory.GetFiles(PriceDir, "PriceFull*.xml", SearchOption.AllDirectories);

                foreach (var file in priceFiles)
                {
                    try
                    {
                        var xDoc = XDocument.Load(file);

                        // זיהוי רשת וסניף מה-XML
                        string xmlChainId = GetXmlVal(xDoc.Root, "ChainId") ?? "0";
                        string rawStoreId = GetXmlVal(xDoc.Root, "StoreId") ?? GetXmlVal(xDoc.Root, "SubChainId") ?? "0";
                        string xmlStoreId = rawStoreId.TrimStart('0');

                        string currentKey = $"{xmlChainId}-{xmlStoreId}";

                        // *** סינון: האם החנות הזו ברדיוס? ***
                        if (!validStoreKeys.Contains(currentKey))
                        {
                            continue;
                        }

                        var itemNode = xDoc.Descendants()
                            .Where(e => e.Name.LocalName == "Item")
                            .FirstOrDefault(x => GetXmlVal(x, "ItemCode")?.Trim() == barcode.Trim());

                        if (itemNode != null)
                        {
                            var productData = ParseProductNode(itemNode, xmlChainId, xmlStoreId, showClub, showQuantity);
                            if (productData != null)
                            {
                                results.Add(productData);
                            }
                        }
                    }
                    catch { /* התעלמות מקבצים פגומים */ }
                }

                if (!results.Any())
                    return NotFound("המוצר לא נמצא בחנויות שבטווח התחנה.");

                return Ok(results.OrderBy(r => r.FinalCalculatedPrice));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inside GetCheapestByStop");
                return StatusCode(500, new { Error = "שגיאה פנימית", Message = ex.Message });
            }
        }

        // --- פונקציות עזר ---

        private ProductData ParseProductNode(XElement itemNode, string chainId, string storeId, bool showClub, bool showQuantity)
        {
            string[] nameTags = { "ItemName", "ItemNm", "ItemNameHeb", "ItemDescription", "ItemDesc", "ItemNameEng" };
            string name = nameTags.Select(tag => GetXmlVal(itemNode, tag))
                .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "Unknown";

            decimal.TryParse(GetXmlVal(itemNode, "ItemPrice"), out decimal price);
            decimal.TryParse(GetXmlVal(itemNode, "PromoPrice"), out decimal promoPrice);

            bool hasPromo = GetXmlVal(itemNode, "HasPromo") == "true";
            bool isClubPromo = GetXmlVal(itemNode, "IsClub") == "true";
            bool isQtyPromo = GetXmlVal(itemNode, "IsQuantity") == "true";
            string promoEnd = GetXmlVal(itemNode, "PromoEndDate");

            decimal finalPrice = price;
            string label = "מחיר רגיל";

            if (hasPromo)
            {
                bool showPromo = false;
                if (!isClubPromo && !isQtyPromo) showPromo = true;
                else if (isClubPromo && showClub) { showPromo = true; label = "מבצע לחברי מועדון"; }
                else if (isQtyPromo && showQuantity) { showPromo = true; label = "מבצע כמות"; }

                if (showPromo)
                {
                    finalPrice = promoPrice;
                    if (!string.IsNullOrEmpty(promoEnd)) label += $" (בתוקף עד {promoEnd})";
                }
            }

            string lookupKey = $"{chainId}-{storeId}";
            bool foundStore = _storeData.TryGetValue(lookupKey, out var s);

            string displayStoreLabel = foundStore
                ? $"{s.StoreName}, {s.Address}, {s.City}"
                : $"סניף {storeId} (רשת {chainId})";

            return new ProductData
            {
                ChainId = chainId,
                StoreId = storeId,
                ItemCode = GetXmlVal(itemNode, "ItemCode"),
                ItemName = name,
                RegularPrice = price,
                FinalCalculatedPrice = finalPrice,
                PriceTypeLabel = label,
                HasPromo = hasPromo,
                PromoPrice = promoPrice,
                IsClubMemberPromo = isClubPromo,
                StoreLabel = displayStoreLabel,
                StoreAddress = foundStore ? s.Address : null,
                StoreCity = foundStore ? s.City : null
            };
        }

        private string? GetXmlVal(XElement item, string tagName)
        {
            var element = item.Elements()
                .FirstOrDefault(e => e.Name.LocalName.Equals(tagName, StringComparison.OrdinalIgnoreCase));
            return element?.Value;
        }

        private void LoadStoreData()
        {
            _storeData.Clear();
            if (!Directory.Exists(StoresFullDir)) return;

            var storeFiles = Directory.GetFiles(StoresFullDir, "*.xml", SearchOption.AllDirectories);
            foreach (var file in storeFiles)
            {
                try
                {
                    var xDoc = XDocument.Load(file);
                    string chainId = GetXmlVal(xDoc.Root, "ChainId")?.Trim() ?? "0";
                    string chainName = GetXmlVal(xDoc.Root, "ChainName")?.Trim() ?? "";

                    var subChains = xDoc.Descendants().Where(e => e.Name.LocalName == "SubChain");
                    foreach (var subChain in subChains)
                    {
                        var stores = subChain.Descendants().Where(e => e.Name.LocalName == "Store");
                        foreach (var s in stores)
                        {
                            string? storeId = GetXmlVal(s, "StoreId")?.Trim();
                            if (string.IsNullOrEmpty(storeId)) continue;

                            storeId = storeId.TrimStart('0');
                            string uniqueKey = $"{chainId}-{storeId}";
                            string name = GetXmlVal(s, "StoreName")?.Trim() ?? "לא ידוע";
                            string address = GetXmlVal(s, "Address")?.Trim() ?? "";
                            string city = GetXmlVal(s, "City")?.Trim() ?? "";

                            _storeData[uniqueKey] = (chainName, name, address, city);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error loading store file {file}: {ex.Message}");
                }
            }
        }
    }
}