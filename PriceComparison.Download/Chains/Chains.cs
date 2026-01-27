using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using PriceComparison.Download.Core;

namespace PriceComparison.Download.Chains
{
    /// <summary>
    /// מודול הורדה עבור רשת קינג סטור
    /// </summary>
    public class KingStoreDownloader : BinaProjectsDownloader
    {
        public override string ChainName => "אלמשהדאוי קינג סטור בע\"מ";
        public override string ChainPrefix => "kingstore";
        public override string BaseUrl => "https://kingstore.binaprojects.com";
    }

    /// <summary>
    /// מודול הורדה עבור רשת מעיין אלפיים
    /// </summary>
    public class MaayanDownloader : BinaProjectsDownloader
    {
        public override string ChainName => "ג.מ מעיין אלפיים (07) בע\"מ";
        public override string ChainPrefix => "maayan2000";
        public override string BaseUrl => "https://maayan2000.binaprojects.com";
    }

    /// <summary>
    /// מודול הורדה עבור רשת גוד פארם
    /// </summary>
    public class GoodPharmDownloader : BinaProjectsDownloader
    {
        public override string ChainName => "גוד פארם בע\"מ";
        public override string ChainPrefix => "goodpharm";
        public override string BaseUrl => "https://goodpharm.binaprojects.com";
    }

    /// <summary>
    /// מודול הורדה עבור רשת שפע ברכת השם
    /// </summary>
    public class ShefaBirkatHashemDownloader : BinaProjectsDownloader
    {
        public override string ChainName => "שפע ברכת השם בע\"מ";
        public override string ChainPrefix => "shefabirkathashem";
        public override string BaseUrl => "https://shefabirkathashem.binaprojects.com";
    }

    /// <summary>
    /// מודול הורדה עבור רשת סופר ספיר
    /// כולל טיפול מיוחד בעמודת "רשת" בטבלה
    /// </summary>
    public class SuperSapirDownloader : BinaProjectsDownloader
    {
        public override string ChainName => "סופר ספיר בע\"מ";
        public override string ChainPrefix => "supersapir";
        public override string BaseUrl => "https://supersapir.binaprojects.com";

        /// <summary>
        /// override לטיפול בעמודת רשת נוספת
        /// </summary>
        protected override List<Models.FileInfo> ParseFilesList(string jsonContent)
        {
            var files = new List<Models.FileInfo>();

            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(jsonContent);
                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    var fileInfo = new Models.FileInfo
                    {
                        FileName = element.TryGetProperty("FileNm", out var fileName) ?
                            fileName.GetString() ?? "" : "",
                        Company = element.TryGetProperty("Company", out var comp) ?
                            comp.GetString() ?? "" : "",
                        Store = element.TryGetProperty("Store", out var store) ?
                            store.GetString() ?? "" : "",
                        TypeFile = element.TryGetProperty("TypeFile", out var type) ?
                            type.GetString() ?? "" : "",
                        DateFile = element.TryGetProperty("DateFile", out var date) ?
                            date.GetString() ?? "" : ""
                    };

                    // טיפול מיוחד בעמודת רשת
                    if (element.TryGetProperty("Reshet", out var reshet))
                    {
                        var reshetValue = reshet.GetString();
                        if (!string.IsNullOrEmpty(reshetValue))
                        {
                            // אם יש ערך ברשת, נוסיף אותו לשם החברה
                            fileInfo.Company = $"{reshetValue} - {fileInfo.Company}";
                        }
                    }

                    files.Add(fileInfo);
                }
            }
            catch (System.Text.Json.JsonException ex)
            {
                Console.WriteLine($"⚠️ שגיאה בפיענוח JSON עבור סופר ספיר: {ex.Message}");
                return new List<Models.FileInfo>();
            }

            return files;
        }

        /// <summary>
        /// override לטיפול בבקשת קבצים עם פרמטר רשת
        /// </summary>
        protected override async Task<List<Models.FileInfo>> GetFilesFromServer(string date, string store, string fileType)
        {
            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("wReshet", ""), // פרמטר רשת ריק = כל הרשתות
                new KeyValuePair<string, string>("WStore", store),
                new KeyValuePair<string, string>("WDate", date),
                new KeyValuePair<string, string>("WFileType", fileType)
            });

            try
            {
                var response = await httpClient.PostAsync($"{BaseUrl}/MainIO_Hok.aspx", formContent);

                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync();
                    return ParseFilesList(jsonContent);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ שגיאה בקבלת קבצים מסוג {fileType} מסופר ספיר: {ex.Message}");
            }

            return new List<Models.FileInfo>();
        }
    }

    /// <summary>
    /// מודול הורדה עבור רשת שוק העיר
    /// </summary>
    public class ShukHayirDownloader : BinaProjectsDownloader
    {
        public override string ChainName => "שוק העיר (ט.ע.מ.ס) בע\"מ";
        public override string ChainPrefix => "shuk-hayir";
        public override string BaseUrl => "https://shuk-hayir.binaprojects.com";
    }

    /// <summary>
    /// מודול הורדה עבור רשת זול ובגדול
    /// </summary>
    public class ZolVeBegadolDownloader : BinaProjectsDownloader
    {
        public override string ChainName => "זול ובגדול בע\"מ";
        public override string ChainPrefix => "zolvebegadol";
        public override string BaseUrl => "https://zolvebegadol.binaprojects.com";
    }

    /// <summary>
    /// מודול הורדה עבור רשת קיי.טי (משנת יוסף)
    /// יש לו שני קישורים - נשתמש ב-binaprojects
    /// </summary>
    public class KTDownloader : BinaProjectsDownloader
    {
        public override string ChainName => "קיי.טי. יבוא ושיווק בע\"מ (משנת יוסף)";
        public override string ChainPrefix => "ktshivuk";
        public override string BaseUrl => "https://ktshivuk.binaprojects.com";
    }
}
