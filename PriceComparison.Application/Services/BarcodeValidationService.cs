using Microsoft.Extensions.Logging;
using PriceComparison.Application.DTOs;
using System.Text.RegularExpressions;

namespace PriceComparison.Application.Services
{
    public class BarcodeValidationService : IBarcodeValidationService
    {
        private readonly ILogger<BarcodeValidationService> _logger;

        public BarcodeValidationService(ILogger<BarcodeValidationService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// בדיקה מלאה של תקינות ברקוד
        /// </summary>
        /// <param name="barcode">ברקוד לבדיקה</param>
        /// <returns>תוצאת הבדיקה</returns>
        public async Task<BarcodeValidationResponseDto> ValidateBarcodeAsync(string barcode)
        {
            try
            {
                _logger.LogDebug("מתחיל בדיקת ברקוד: {Barcode}", barcode);

                // בדיקה בסיסית - האם יש ברקוד
                if (string.IsNullOrWhiteSpace(barcode))
                {
                    return new BarcodeValidationResponseDto
                    {
                        IsValid = false,
                        ErrorMessage = "אנא הזן מספר ברקוד"
                    };
                }

                // ניקוי הברקוד
                var normalizedBarcode = NormalizeBarcode(barcode);
                _logger.LogDebug("ברקוד מנוקה: {NormalizedBarcode}", normalizedBarcode);

                // בדיקת פורמט בסיסי
                if (!IsValidBarcodeFormat(normalizedBarcode))
                {
                    var validLengths = new[] { 8, 12, 13 };
                    return new BarcodeValidationResponseDto
                    {
                        IsValid = false,
                        ErrorMessage = $"ברקוד חייב להכיל {string.Join(" או ", validLengths)} ספרות בלבד. " +
                                     $"הברקוד שהוזן: \"{normalizedBarcode}\" ({normalizedBarcode.Length} ספרות)"
                    };
                }

                // בדיקת ספרת ביקורת
                if (!IsValidChecksum(normalizedBarcode))
                {
                    return new BarcodeValidationResponseDto
                    {
                        IsValid = false,
                        ErrorMessage = "ספרת הביקורת של הברקוד אינה תקינה"
                    };
                }

                // אם הגענו לכאן - הברקוד תקין
                _logger.LogDebug("ברקוד תקין: {NormalizedBarcode}", normalizedBarcode);

                return new BarcodeValidationResponseDto
                {
                    IsValid = true,
                    NormalizedBarcode = normalizedBarcode
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "שגיאה בבדיקת ברקוד: {Barcode}", barcode);
                throw;
            }
        }

        /// <summary>
        /// בדיקה מהירה של פורמט ברקוד (ללא בדיקת ספרת ביקורת)
        /// </summary>
        /// <param name="barcode">ברקוד לבדיקה</param>
        /// <returns>האם הפורמט תקין</returns>
        public bool IsValidBarcodeFormat(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode))
                return false;

            var normalized = NormalizeBarcode(barcode);
            var validLengths = new[] { 8, 12, 13 };

            return validLengths.Contains(normalized.Length) &&
                   Regex.IsMatch(normalized, @"^\d+$");
        }

        /// <summary>
        /// ניקוי ברקוד מתווים לא רצויים
        /// </summary>
        /// <param name="barcode">ברקוד גולמי</param>
        /// <returns>ברקוד מנוקה</returns>
        public string NormalizeBarcode(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode))
                return string.Empty;

            // הסרת כל התווים שאינם ספרות
            return Regex.Replace(barcode.Trim(), @"\D", "");
        }

        /// <summary>
        /// בדיקת ספרת ביקורת לברקודים מסוג EAN-13/EAN-8/UPC-A
        /// </summary>
        /// <param name="barcode">ברקוד מנוקה</param>
        /// <returns>האם ספרת הביקורת תקינה</returns>
        private bool IsValidChecksum(string barcode)
        {
            try
            {
                // המרה למערך של מספרים
                var digits = barcode.Select(c => int.Parse(c.ToString())).ToArray();

                return barcode.Length switch
                {
                    13 => ValidateEan13Checksum(digits),
                    8 => ValidateEan8Checksum(digits),
                    12 => ValidateUpcAChecksum(digits),
                    _ => false
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "שגיאה בבדיקת ספרת ביקורת עבור ברקוד: {Barcode}", barcode);
                return false;
            }
        }

        /// <summary>
        /// בדיקת ספרת ביקורת לברקוד EAN-13
        /// </summary>
        private bool ValidateEan13Checksum(int[] digits)
        {
            if (digits.Length != 13)
                return false;

            int sum = 0;
            for (int i = 0; i < 12; i++)
            {
                int multiplier = (i % 2 == 0) ? 1 : 3;
                sum += digits[i] * multiplier;
            }

            int checkDigit = (10 - (sum % 10)) % 10;
            return checkDigit == digits[12];
        }

        /// <summary>
        /// בדיקת ספרת ביקורת לברקוד EAN-8
        /// </summary>
        private bool ValidateEan8Checksum(int[] digits)
        {
            if (digits.Length != 8)
                return false;

            int sum = 0;
            for (int i = 0; i < 7; i++)
            {
                int multiplier = (i % 2 == 0) ? 1 : 3;
                sum += digits[i] * multiplier;
            }

            int checkDigit = (10 - (sum % 10)) % 10;
            return checkDigit == digits[7];
        }

        /// <summary>
        /// בדיקת ספרת ביקורת לברקוד UPC-A
        /// </summary>
        private bool ValidateUpcAChecksum(int[] digits)
        {
            if (digits.Length != 12)
                return false;

            int sum = 0;
            for (int i = 0; i < 11; i++)
            {
                int multiplier = (i % 2 == 0) ? 3 : 1;
                sum += digits[i] * multiplier;
            }

            int checkDigit = (10 - (sum % 10)) % 10;
            return checkDigit == digits[11];
        }
    }
}