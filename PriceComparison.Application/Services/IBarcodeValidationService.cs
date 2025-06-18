using PriceComparison.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriceComparison.Application.Services
{
    public interface IBarcodeValidationService
    {
        Task<BarcodeValidationResponseDto> ValidateBarcodeAsync(string barcode);
        bool IsValidBarcodeFormat(string barcode);
        string NormalizeBarcode(string barcode);
    }
}
