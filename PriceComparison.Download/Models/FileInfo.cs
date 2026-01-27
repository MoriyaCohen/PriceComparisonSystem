using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriceComparison.Download.Models
{
    /// <summary>
    /// מידע על קובץ ברשת
    /// </summary>
    public class FileInfo
    {
        /// <summary>
        /// שם הקובץ
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// שם החברה/רשת
        /// </summary>
        public string Company { get; set; } = string.Empty;

        /// <summary>
        /// שם הסניף
        /// </summary>
        public string Store { get; set; } = string.Empty;

        /// <summary>
        /// סוג הקובץ (Price, PriceFull, Promo, PromoFull, StoresFull)
        /// </summary>
        public string TypeFile { get; set; } = string.Empty;

        /// <summary>
        /// תאריך הקובץ
        /// </summary>
        public string DateFile { get; set; } = string.Empty;

        /// <summary>
        /// סיומת הקובץ
        /// </summary>
        public string Extension { get; set; } = "ZIP";

        /// <summary>
        /// מזהה הסניף שחולץ משם הקובץ
        /// </summary>
        public string StoreId => ExtractStoreIdFromFileName();

        /// <summary>
        /// זמן יצירת הקובץ (נחלץ מהשם)
        /// </summary>
        public DateTime? CreationTime => ExtractCreationTimeFromFileName();

        /// <summary>
        /// חילוץ מזהה סניף משם הקובץ
        /// </summary>
        private string ExtractStoreIdFromFileName()
        {
            try
            {
                // פורמט: TypeXXXXXXXXXXXXX-StoreId-YYYYMMDDHHNN-Version
                var parts = FileName.Split('-');
                if (parts.Length >= 2)
                {
                    return parts[1];
                }
            }
            catch { }
            return "";
        }

        /// <summary>
        /// חילוץ זמן יצירה משם הקובץ
        /// </summary>
        private DateTime? ExtractCreationTimeFromFileName()
        {
            try
            {
                // פורמט: TypeXXXXXXXXXXXXX-StoreId-YYYYMMDDHHNN-Version
                var parts = FileName.Split('-');
                if (parts.Length >= 3)
                {
                    var dateTimeStr = parts[2];
                    if (dateTimeStr.Length == 12) // YYYYMMDDHHNN
                    {
                        var year = int.Parse(dateTimeStr.Substring(0, 4));
                        var month = int.Parse(dateTimeStr.Substring(4, 2));
                        var day = int.Parse(dateTimeStr.Substring(6, 2));
                        var hour = int.Parse(dateTimeStr.Substring(8, 2));
                        var minute = int.Parse(dateTimeStr.Substring(10, 2));

                        return new DateTime(year, month, day, hour, minute, 0);
                    }
                }
            }
            catch { }
            return null;
        }
    }
}
