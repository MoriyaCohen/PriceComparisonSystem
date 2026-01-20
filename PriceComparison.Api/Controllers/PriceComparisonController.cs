using Microsoft.AspNetCore.Mvc;
using PriceComparison.Application.DTOs; 
using PriceComparison.Application.Services; 
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace PriceComparison.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PriceComparisonController : ControllerBase
    {
        private readonly ILogger<PriceComparisonController> _logger;
        private readonly PriceSyncService _priceSyncService;

  
        private const string BaseFolder = @"C:\Users\ADMIN\projects\LocalXmlData";
        private string PriceDir => Path.Combine(BaseFolder, "PRICEFULL");
        private string PromoDir => Path.Combine(BaseFolder, "PROMOFULL");
        private string StoresFullDir => Path.Combine(BaseFolder, "STORESFULL");

        // --- התיקון המרכזי: המפתח במילון הוא ייחודי (ChainId-StoreId) ---
        private readonly Dictionary<string, (string ChainName, string StoreName, string Address, string City)> _storeData = new();

        public PriceComparisonController(
            ILogger<PriceComparisonController> logger,
            PriceSyncService priceSyncService)
        {
            _logger = logger;
            _priceSyncService = priceSyncService;

            // טעינת הנתונים לזיכרון בעת עליית השרת
            LoadStoreData();
        }

        // --- קריאה בטוחה של תגית XML ---
        private string? GetXmlVal(XElement item, string tagName)
        {
            var element = item.Elements()
                .FirstOrDefault(e => e.Name.LocalName.Equals(tagName, StringComparison.OrdinalIgnoreCase));
            return element?.Value;
        }

        // --- טעינת סניפים חכמה ומתוקנת ---
        private void LoadStoreData()
        {
            _storeData.Clear(); // ניקוי לפני טעינה

            if (!Directory.Exists(StoresFullDir))
            {
                _logger.LogError($"Store directory not found: {StoresFullDir}");
                return;
            }

            var storeFiles = Directory.GetFiles(StoresFullDir, "*.xml", SearchOption.AllDirectories);
            _logger.LogInformation($"Loading stores from {storeFiles.Length} files...");

            foreach (var file in storeFiles)
            {
                try
                {
                    var xDoc = XDocument.Load(file);

                    // 1. זיהוי הרשת של הקובץ הזה (חשוב מאוד!)
                    string chainId = GetXmlVal(xDoc.Root, "ChainId")?.Trim() ?? "0";
                    string chainName = GetXmlVal(xDoc.Root, "ChainName")?.Trim() ?? "";

                    // ירידה לרמת תתי-רשתות וסניפים
                    var subChains = xDoc.Descendants().Where(e => e.Name.LocalName == "SubChain");
                    foreach (var subChain in subChains)
                    {
                        var stores = subChain.Descendants().Where(e => e.Name.LocalName == "Store");
                        foreach (var s in stores)
                        {
                            string? storeId = GetXmlVal(s, "StoreId")?.Trim();
                            if (string.IsNullOrEmpty(storeId)) continue;

                            // 2. נרמול: הסרת אפסים מובילים (0340 -> 340)
                            storeId = storeId.TrimStart('0');

                            // 3. יצירת מפתח ייחודי: ChainId-StoreId
                            // זה מונע התנגשויות בין רשתות שונות שיש להן אותו מספר סניף
                            string uniqueKey = $"{chainId}-{storeId}";

                            string name = GetXmlVal(s, "StoreName")?.Trim() ?? "לא ידוע";
                            string address = GetXmlVal(s, "Address")?.Trim() ?? "";
                            string city = GetXmlVal(s, "City")?.Trim() ?? "";

                            // שמירה במילון
                            _storeData[uniqueKey] = (chainName, name, address, city);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error loading store file {Path.GetFileName(file)}: {ex.Message}");
                }
            }

            _logger.LogInformation($"Total unique stores loaded: {_storeData.Count}");

            // בדיקת דיבאג ללוג: האם סניף 340 של קינג סטור נטען?
            // המזהה של קינג סטור הוא 7290058108879
            string debugKey = "7290058108879-340";
            if (_storeData.ContainsKey(debugKey))
                _logger.LogInformation("SUCCESS: King Store Branch 340 loaded successfully!");
            else
                _logger.LogWarning("WARNING: King Store Branch 340 was NOT found in the loaded XMLs.");
        }

        // --- סנכרון ידני (נשאר ללא שינוי, כפי שביקשת) ---
        [HttpPost("run-daily-sync")]
        public IActionResult RunDailySyncManual()
        {
            try
            {
                if (!Directory.Exists(PriceDir))
                    return NotFound($"Price directory missing: {PriceDir}");

                var allPriceFiles = Directory.GetFiles(PriceDir, "PriceFull*.xml", SearchOption.AllDirectories);
                var allPromoFiles = Directory.Exists(PromoDir)
                    ? Directory.GetFiles(PromoDir, "PromoFull*.xml", SearchOption.AllDirectories)
                    : new string[0];

                int count = 0;
                _logger.LogInformation($"🔄 Starting Sync on {allPriceFiles.Length} files...");

                foreach (var pricePath in allPriceFiles)
                {
                    try
                    {
                        string fileName = Path.GetFileName(pricePath);
                        string clean = fileName.Replace("PriceFull", "");
                        var parts = clean.Split('-');

                        if (parts.Length >= 2)
                        {
                            var matchingPromo = allPromoFiles
                                .FirstOrDefault(f => Path.GetFileName(f).Contains(parts[0]));

                            _priceSyncService.GenerateSyncedFile(pricePath, matchingPromo, PriceDir);
                            count++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error processing file: {Path.GetFileName(pricePath)}");
                    }
                }

                return Ok(new { Message = "Sync completed & Files Updated.", FilesProcessed = count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal Sync Error");
                return StatusCode(500, ex.Message);
            }
        }

        // --- חיפוש חכם (הותאם להשתמש במפתח הייחודי) ---
        [HttpGet("get-cheapest-smart/{barcode}")]
        public IActionResult GetCheapestSmart(string barcode, [FromQuery] bool showClub = false, [FromQuery] bool showQuantity = false)
        {
            try
            {
                if (!Directory.Exists(PriceDir))
                    return BadRequest("Price directory not found.");

                var results = new List<ProductData>();
                var priceFiles = Directory.GetFiles(PriceDir, "PriceFull*.xml", SearchOption.AllDirectories);

                foreach (var file in priceFiles)
                {
                    try
                    {
                        var xDoc = XDocument.Load(file);

                        // זיהוי הרשת והסניף מקובץ המחירים
                        string chainId = GetXmlVal(xDoc.Root, "ChainId") ?? "0";
                        string chainNameFromFile = GetXmlVal(xDoc.Root, "ChainName") ?? "";
                        string storeId = GetXmlVal(xDoc.Root, "StoreId") ?? GetXmlVal(xDoc.Root, "SubChainId") ?? "0";

                     
                        storeId = storeId.TrimStart('0');

           
                        var itemNode = xDoc.Descendants()
                            .Where(e => e.Name.LocalName == "Item")
                            .FirstOrDefault(x => GetXmlVal(x, "ItemCode")?.Trim() == barcode.Trim());

                        if (itemNode != null)
                        {
                            // שליפת נתוני המוצר
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

                            string displayStoreName;
                            string displayAddress = null;
                            string displayCity = null;
                            string displayStoreLabel;

                            if (foundStore)
                            {
                               
                                displayStoreName = s.StoreName;
                                displayAddress = s.Address;
                                displayCity = s.City;
                                displayStoreLabel = $"{s.StoreName}, {s.Address}, {s.City}";
                            }
                            else
                            {
                               
                                displayStoreName = $"סניף {storeId}";
                                displayStoreLabel = $"סניף {storeId} (רשת {chainId})";
                            }

                    
                            results.Add(new ProductData
                            {
                                ChainId = chainId,
                                StoreId = storeId,
                                ItemCode = barcode,
                                ItemName = name,
                                RegularPrice = price,
                                FinalCalculatedPrice = finalPrice,
                                PriceTypeLabel = label,
                                HasPromo = hasPromo,
                                PromoPrice = promoPrice,
                                IsClubMemberPromo = isClubPromo,
                                PromoDescription = GetXmlVal(itemNode, "PromoDescription"),

                                StoreLabel = displayStoreLabel,
                                StoreAddress = displayAddress,
                                StoreCity = displayCity
                            });
                        }
                    }
                    catch { }
                }

                if (!results.Any())
                    return NotFound("Product not found in any store.");

                return Ok(results.OrderBy(r => r.FinalCalculatedPrice).Take(3));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting smart prices");
                return StatusCode(500, "Internal Server Error");
            }
        }

  
        [HttpGet("debug-stores")]
        public IActionResult DebugStores()
        {
            return Ok(new
            {
                TotalStores = _storeData.Count,
                Sample = _storeData.Take(10).Select(kv => $"{kv.Key} => {kv.Value.StoreName}")
            });
        }
    }
}