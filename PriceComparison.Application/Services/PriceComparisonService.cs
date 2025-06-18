using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PriceComparison.Application.DTOs;
using PriceComparison.Infrastructure.Models;
using PriceComparison.Infrastructure.Repositories;
using System.Linq;

namespace PriceComparison.Application.Services
{
  
    public class PriceComparisonService : IPriceComparisonService
    {
        private readonly IStorePriceRepository _storePriceRepository;
        private readonly IProductRepository _productRepository;
        private readonly ILogger<PriceComparisonService> _logger;

        public PriceComparisonService(
            IStorePriceRepository storePriceRepository,
            IProductRepository productRepository,
            ILogger<PriceComparisonService> logger)
        {
            _storePriceRepository = storePriceRepository;
            _productRepository = productRepository;
            _logger = logger;
        }

        /// <summary>
        /// חיפוש מוצר לפי ברקוד והשוואת מחירים
        /// </summary>
        /// <param name="barcode">ברקוד המוצר</param>
        /// <returns>תוצאות החיפוש והשוואת המחירים</returns>
        public async Task<PriceComparisonResponseDto> SearchProductByBarcodeAsync(string barcode)
        {
            try
            {
                _logger.LogInformation("מחפש מוצר עבור ברקוד: {Barcode}", barcode);

                // חיפוש המוצר לפי ברקוד
                var product = await _productRepository.GetByBarcodeAsync(barcode);
                if (product == null)
                {
                    _logger.LogInformation("מוצר לא נמצא עבור ברקוד: {Barcode}", barcode);
                    return new PriceComparisonResponseDto
                    {
                        Success = false,
                        ErrorMessage = "מוצר לא נמצא במערכת",
                        PriceDetails = new List<ProductPriceInfoDto>()
                    };
                }

                // חיפוש כל המחירים למוצר זה
                var storePrices = await _storePriceRepository.GetPricesByBarcodeAsync(barcode);
                if (!storePrices.Any())
                {
                    _logger.LogInformation("לא נמצאו מחירים עבור מוצר: {ProductName}, ברקוד: {Barcode}",
                        product.ProductName, barcode);

                    return new PriceComparisonResponseDto
                    {
                        Success = true,
                        ProductInfo = new ProductInfoDto
                        {
                            ProductName = product.ProductName,
                            Barcode = product.Barcode,
                            ManufacturerName = product.ManufacturerName
                        },
                        Statistics = null,
                        PriceDetails = new List<ProductPriceInfoDto>()
                    };
                }

                // המרה ל-DTO והכנת תוצאות
                var priceDetails = storePrices.Select(MapToPriceInfoDto).ToList();
                var statistics = CalculateStatistics(priceDetails);

                // סימון המחיר הזול ביותר
                if (priceDetails.Any())
                {
                    var minPrice = priceDetails.Min(p => p.CurrentPrice);
                    foreach (var price in priceDetails.Where(p => p.CurrentPrice == minPrice))
                    {
                        price.IsMinPrice = true;
                    }
                }

                _logger.LogInformation("נמצאו {Count} מחירים עבור מוצר {ProductName}",
                    priceDetails.Count, product.ProductName);

                return new PriceComparisonResponseDto
                {
                    Success = true,
                    ProductInfo = new ProductInfoDto
                    {
                        ProductName = product.ProductName,
                        Barcode = product.Barcode,
                        ManufacturerName = product.ManufacturerName
                    },
                    Statistics = statistics,
                    PriceDetails = priceDetails
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "שגיאה בחיפוש מוצר עבור ברקוד: {Barcode}", barcode);
                throw;
            }
        }

        /// <summary>
        /// חישוב סטטיסטיקות מחירים עבור ברקוד
        /// </summary>
        /// <param name="barcode">ברקוד המוצר</param>
        /// <returns>סטטיסטיקות מחירים</returns>
        public async Task<PriceStatisticsDto?> GetPriceStatisticsAsync(string barcode)
        {
            try
            {
                var priceDetails = await GetPricesByBarcodeAsync(barcode);

                if (!priceDetails.Any())
                    return null;

                return CalculateStatistics(priceDetails);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "שגיאה בחישוב סטטיסטיקות עבור ברקוד: {Barcode}", barcode);
                throw;
            }
        }

        /// <summary>
        /// קבלת המחיר הזול ביותר עבור ברקוד
        /// </summary>
        /// <param name="barcode">ברקוד המוצר</param>
        /// <returns>פרטי המחיר הזול ביותר</returns>
        public async Task<ProductPriceInfoDto?> GetCheapestPriceAsync(string barcode)
        {
            try
            {
                var priceDetails = await GetPricesByBarcodeAsync(barcode);

                if (!priceDetails.Any())
                    return null;

                var cheapest = priceDetails.OrderBy(p => p.CurrentPrice).First();
                cheapest.IsMinPrice = true;

                return cheapest;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "שגיאה בחיפוש מחיר זול ביותר עבור ברקוד: {Barcode}", barcode);
                throw;
            }
        }

        /// <summary>
        /// קבלת כל המחירים עבור ברקוד מסוים
        /// </summary>
        /// <param name="barcode">ברקוד המוצר</param>
        /// <returns>רשימת מחירים</returns>
        public async Task<List<ProductPriceInfoDto>> GetPricesByBarcodeAsync(string barcode)
        {
            try
            {
                var storePrices = await _storePriceRepository.GetPricesByBarcodeAsync(barcode);
                return storePrices.Select(MapToPriceInfoDto).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "שגיאה בקבלת מחירים עבור ברקוד: {Barcode}", barcode);
                throw;
            }
        }

        /// <summary>
        /// המרת StorePrice ל-ProductPriceInfoDto
        /// </summary>
        private ProductPriceInfoDto MapToPriceInfoDto(StorePrice storePrice)
        {
            return new ProductPriceInfoDto
            {
                ProductId = storePrice.ProductId,
                ProductName = storePrice.Product?.ProductName ?? "לא זמין",
                ChainName = storePrice.Store?.Chain?.ChainName ?? "לא זמין",
                StoreName = storePrice.Store?.StoreName ?? "לא זמין",
                StoreAddress = storePrice.Store?.Address,
                CurrentPrice = (decimal)storePrice.CurrentPrice,
                UnitPrice = storePrice.UnitPrice.HasValue ? (decimal)storePrice.UnitPrice.Value : null,
                UnitOfMeasure = storePrice.Product?.UnitOfMeasure,
                IsWeighted = storePrice.Product?.IsWeighted ?? false,
                AllowDiscount = storePrice.AllowDiscount ?? false,
                LastUpdated = (DateTime)storePrice.LastUpdated,
                IsMinPrice = false // יוגדר מאוחר יותר
            };
        }

        /// <summary>
        /// חישוב סטטיסטיקות מחירים
        /// </summary>
        private PriceStatisticsDto CalculateStatistics(List<ProductPriceInfoDto> priceDetails)
        {
            if (!priceDetails.Any())
                return new PriceStatisticsDto();

            var prices = priceDetails.Select(p => p.CurrentPrice).ToList();
            var chains = priceDetails.Select(p => p.ChainName).Distinct().ToList();

            return new PriceStatisticsDto
            {
                MinPrice = prices.Min(),
                MaxPrice = prices.Max(),
                AveragePrice = prices.Average(),
                StoreCount = priceDetails.Count,
                ChainCount = chains.Count
            };
        }
    }
}