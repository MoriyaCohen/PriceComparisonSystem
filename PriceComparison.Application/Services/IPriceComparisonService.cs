using PriceComparison.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriceComparison.Application.Services
{
    public interface IPriceComparisonService
    {
        Task<PriceComparisonResponseDto> SearchProductByBarcodeAsync(string barcode);
        Task<PriceStatisticsDto?> GetPriceStatisticsAsync(string barcode);
        Task<ProductPriceInfoDto?> GetCheapestPriceAsync(string barcode);
        Task<List<ProductPriceInfoDto>> GetPricesByBarcodeAsync(string barcode);
    }
}
