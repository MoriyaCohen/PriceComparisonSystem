using PriceComparison.Application.DTOs;

namespace PriceComparison.Application.Services
{
    /// <summary>
    /// שירות לחיפוש מקומי בקבצי XML ללא שימוש במסד נתונים
    /// </summary>
    public interface ILocalXmlSearchService
    {
        /// <summary>
        /// חיפוש מוצר לפי ברקוד והחזרת 3 המחירים הזולים ביותר
        /// </summary>
        /// <param name="barcode">ברקוד המוצר לחיפוש</param>
        /// <returns>תוצאות חיפוש עם 3 המחירים הזולים ביותר</returns>
        Task<PriceComparisonResponseDto> SearchByBarcodeAsync(string barcode);

        /// <summary>
        /// טעינה מחדש של נתוני XML מהתיקייה המקומית
        /// </summary>
        /// <returns>האם הטעינה הצליחה</returns>
        Task<bool> RefreshDataAsync();

        /// <summary>
        /// קבלת מצב הנתונים הנוכחי
        /// </summary>
        /// <returns>מידע על מצב הנתונים</returns>
        Task<LocalDataStatusDto> GetDataStatusAsync();
    }
}