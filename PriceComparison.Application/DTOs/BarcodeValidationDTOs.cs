using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriceComparison.Application.DTOs
{
    public class BarcodeValidationRequest
    {
        [Required(ErrorMessage = "מספר ברקוד הוא שדה חובה")]
        [StringLength(13, MinimumLength = 8, ErrorMessage = "ברקוד חייב להיות באורך 8-13 ספרות")]
        [RegularExpression(@"^\d+$", ErrorMessage = "ברקוד חייב להכיל ספרות בלבד")]
        public string Barcode { get; set; } = string.Empty;
    }
    public class BarcodeValidationResponse
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// הברקוד המנורמל לאחר ניקוי
        /// </summary>
        public string? NormalizedBarcode { get; set; }

        /// <summary>
        /// פרטים נוספים על הבדיקה (לדיבאג)
        /// </summary>
        public BarcodeValidationDetails? ValidationDetails { get; set; }

        /// <summary>
        /// יצירת תגובה מוצלחת
        /// </summary>
        public static BarcodeValidationResponse Success(string normalizedBarcode, BarcodeValidationDetails? details = null)
        {
            return new BarcodeValidationResponse
            {
                IsValid = true,
                NormalizedBarcode = normalizedBarcode,
                ValidationDetails = details
            };
        }

        /// <summary>
        /// יצירת תגובת שגיאה
        /// </summary>
        public static BarcodeValidationResponse Error(string errorMessage, BarcodeValidationDetails? details = null)
        {
            return new BarcodeValidationResponse
            {
                IsValid = false,
                ErrorMessage = errorMessage,
                ValidationDetails = details
            };
        }
    }

 
    public class BarcodeValidationDetails
    {
        /// <summary>
        /// סוג הברקוד שזוהה (EAN-8, EAN-13, UPC-A וכו')
        /// </summary>
        public string BarcodeType { get; set; } = string.Empty;

        /// <summary>
        /// האם ספרת הביקורת תקינה
        /// </summary>
        public bool IsChecksumValid { get; set; }

        /// <summary>
        /// ספרת הביקורת שחושבה
        /// </summary>
        public int CalculatedChecksum { get; set; }

        /// <summary>
        /// ספרת הביקורת שהתקבלה
        /// </summary>
        public int ProvidedChecksum { get; set; }

        /// <summary>
        /// האם הברקוד נמצא במסד הנתונים
        /// </summary>
        public bool ExistsInDatabase { get; set; }

        /// <summary>
        /// מספר מוצרים שנמצאו עם הברקוד
        /// </summary>
        public int ProductCount { get; set; }
    }

    /// <summary>
    /// טיפוסי ברקוד נתמכים
    /// </summary>
    public enum BarcodeType
    {
        /// <summary>
        /// לא מזוהה
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// EAN-8 (8 ספרות)
        /// </summary>
        EAN8 = 8,

        /// <summary>
        /// UPC-A (12 ספרות)
        /// </summary>
        UPCA = 12,

        /// <summary>
        /// EAN-13 (13 ספרות)
        /// </summary>
        EAN13 = 13
    }

    /// <summary>
    /// קודי שגיאה לבדיקת ברקוד
    /// </summary>
    public static class BarcodeValidationErrorCodes
    {
        public const string EMPTY_BARCODE = "EMPTY_BARCODE";
        public const string INVALID_LENGTH = "INVALID_LENGTH";
        public const string INVALID_CHARACTERS = "INVALID_CHARACTERS";
        public const string INVALID_CHECKSUM = "INVALID_CHECKSUM";
        public const string NOT_FOUND_IN_DATABASE = "NOT_FOUND_IN_DATABASE";
        public const string MULTIPLE_PRODUCTS_FOUND = "MULTIPLE_PRODUCTS_FOUND";
    }
}
