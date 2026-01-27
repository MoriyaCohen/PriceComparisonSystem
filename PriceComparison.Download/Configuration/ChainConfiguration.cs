using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using PriceComparison.Download.Models;

namespace PriceComparison.Download.Configuration
{
    /// <summary>
    /// הגדרות תצורה לכל הרשתות
    /// מכילה מידע על כל הרשתות הזמינות ופרטי החיבור שלהן
    /// </summary>
    public class ChainConfiguration
    {
        /// <summary>
        /// רשימת כל הרשתות הזמינות
        /// </summary>
        public List<ChainInfo> Chains { get; set; } = new();

        /// <summary>
        /// הגדרות כלליות
        /// </summary>
        public GeneralSettings General { get; set; } = new();

        /// <summary>
        /// הגדרות Azure Storage
        /// </summary>
        public AzureStorageSettings Azure { get; set; } = new();

        /// <summary>
        /// קבלת מידע רשת לפי שם
        /// </summary>
        /// <param name="chainName">שם הרשת</param>
        /// <returns>מידע הרשת או null אם לא נמצא</returns>
        public ChainInfo? GetChainByName(string chainName)
        {
            return Chains.FirstOrDefault(c =>
                c.Name.Equals(chainName, StringComparison.OrdinalIgnoreCase) ||
                c.Prefix.Equals(chainName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// קבלת כל הרשתות הפעילות
        /// </summary>
        /// <returns>רשימת רשתות פעילות</returns>
        public List<ChainInfo> GetActiveChains()
        {
            return Chains.Where(c => c.IsActive).ToList();
        }

        /// <summary>
        /// קבלת רשתות לפי סוג
        /// </summary>
        /// <param name="chainType">סוג הרשת</param>
        /// <returns>רשימת רשתות מהסוג המבוקש</returns>
        public List<ChainInfo> GetChainsByType(ChainType chainType)
        {
            return Chains.Where(c => c.Type == chainType && c.IsActive).ToList();
        }

        /// <summary>
        /// בדיקה אם רשת קיימת ופעילה
        /// </summary>
        /// <param name="chainName">שם הרשת</param>
        /// <returns>true אם הרשת קיימת ופעילה</returns>
        public bool IsChainActive(string chainName)
        {
            var chain = GetChainByName(chainName);
            return chain != null && chain.IsActive;
        }

        /// <summary>
        /// הוספת רשת חדשה
        /// </summary>
        /// <param name="chainInfo">מידע הרשת החדשה</param>
        public void AddChain(ChainInfo chainInfo)
        {
            // בדיקה שלא קיימת רשת עם אותו שם או prefix
            var existing = Chains.FirstOrDefault(c =>
                c.Name.Equals(chainInfo.Name, StringComparison.OrdinalIgnoreCase) ||
                c.Prefix.Equals(chainInfo.Prefix, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                throw new InvalidOperationException($"רשת עם השם '{chainInfo.Name}' או הקוד '{chainInfo.Prefix}' כבר קיימת");
            }

            Chains.Add(chainInfo);
        }

        /// <summary>
        /// עדכון מידע רשת קיימת
        /// </summary>
        /// <param name="chainName">שם הרשת לעדכון</param>
        /// <param name="updatedInfo">המידע המעודכן</param>
        public void UpdateChain(string chainName, ChainInfo updatedInfo)
        {
            var index = Chains.FindIndex(c =>
                c.Name.Equals(chainName, StringComparison.OrdinalIgnoreCase));

            if (index >= 0)
            {
                Chains[index] = updatedInfo;
            }
            else
            {
                throw new KeyNotFoundException($"רשת עם השם '{chainName}' לא נמצאה");
            }
        }

        /// <summary>
        /// הסרת רשת
        /// </summary>
        /// <param name="chainName">שם הרשת להסרה</param>
        public void RemoveChain(string chainName)
        {
            var chain = GetChainByName(chainName);
            if (chain != null)
            {
                Chains.Remove(chain);
            }
        }

        /// <summary>
        /// validation של התצורה
        /// </summary>
        /// <returns>רשימת שגיאות אם קיימות</returns>
        public List<string> ValidateConfiguration()
        {
            var errors = new List<string>();

            // בדיקת כפילויות
            var duplicateNames = Chains.GroupBy(c => c.Name.ToLower())
                .Where(g => g.Count() > 1)
                .Select(g => g.Key);

            var duplicatePrefixes = Chains.GroupBy(c => c.Prefix.ToLower())
                .Where(g => g.Count() > 1)
                .Select(g => g.Key);

            foreach (var name in duplicateNames)
            {
                errors.Add($"שם רשת כפול: {name}");
            }

            foreach (var prefix in duplicatePrefixes)
            {
                errors.Add($"קוד רשת כפול: {prefix}");
            }

            // בדיקת תקינות כל רשת
            foreach (var chain in Chains)
            {
                var chainErrors = ValidateChain(chain);
                errors.AddRange(chainErrors);
            }

            // בדיקת הגדרות כלליות
            if (General.MaxConcurrentDownloads <= 0)
            {
                errors.Add("מספר הורדות מקביליות חייב להיות גדול מ-0");
            }

            if (General.TimeoutMinutes <= 0)
            {
                errors.Add("זמן timeout חייב להיות גדול מ-0");
            }

            return errors;
        }

        /// <summary>
        /// validation של רשת יחידה
        /// </summary>
        /// <param name="chain">מידע הרשת</param>
        /// <returns>רשימת שגיאות</returns>
        private List<string> ValidateChain(ChainInfo chain)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(chain.Name))
            {
                errors.Add("שם הרשת לא יכול להיות ריק");
            }

            if (string.IsNullOrWhiteSpace(chain.Prefix))
            {
                errors.Add($"קוד הרשת '{chain.Name}' לא יכול להיות ריק");
            }

            if (string.IsNullOrWhiteSpace(chain.BaseUrl))
            {
                errors.Add($"כתובת הרשת '{chain.Name}' לא יכולה להיות ריקה");
            }
            else if (!Uri.TryCreate(chain.BaseUrl, UriKind.Absolute, out _))
            {
                errors.Add($"כתובת הרשת '{chain.Name}' אינה תקינה: {chain.BaseUrl}");
            }

            // בדיקת פרטי התחברות אם נדרשים
            if (chain.Credentials?.RequiresPassword == true)
            {
                if (string.IsNullOrWhiteSpace(chain.Credentials.Username))
                {
                    errors.Add($"שם משתמש נדרש עבור רשת '{chain.Name}'");
                }

                if (string.IsNullOrWhiteSpace(chain.Credentials.Password))
                {
                    errors.Add($"סיסמה נדרשת עבור רשת '{chain.Name}'");
                }
            }

            return errors;
        }

        /// <summary>
        /// יצירת תצורה ברירת מחדל
        /// </summary>
        /// <returns>תצורה עם הרשתות הבסיסיות</returns>
        public static ChainConfiguration CreateDefault()
        {
            var config = new ChainConfiguration();

            // הוספת כל הרשתות של בינה פרוגקט
            config.Chains.AddRange(new[]
            {
                new ChainInfo
                {
                    Name = "אלמשהדאוי קינג סטור בע\"מ",
                    Prefix = "kingstore",
                    BaseUrl = "https://kingstore.binaprojects.com",
                    Type = ChainType.BinaProjects,
                    IsActive = true,
                    Notes = "רשת מקורית שעליה התבסס הפיתוח"
                },
                new ChainInfo
                {
                    Name = "ג.מ מעיין אלפיים (07) בע\"מ",
                    Prefix = "maayan2000",
                    BaseUrl = "https://maayan2000.binaprojects.com",
                    Type = ChainType.BinaProjects,
                    IsActive = true
                },
                new ChainInfo
                {
                    Name = "גוד פארם בע\"מ",
                    Prefix = "goodpharm",
                    BaseUrl = "https://goodpharm.binaprojects.com",
                    Type = ChainType.BinaProjects,
                    IsActive = true
                },
                new ChainInfo
                {
                    Name = "שפע ברכת השם בע\"מ",
                    Prefix = "shefabirkathashem",
                    BaseUrl = "https://shefabirkathashem.binaprojects.com",
                    Type = ChainType.BinaProjects,
                    IsActive = true
                },
                new ChainInfo
                {
                    Name = "סופר ספיר בע\"מ",
                    Prefix = "supersapir",
                    BaseUrl = "https://supersapir.binaprojects.com",
                    Type = ChainType.BinaProjects,
                    IsActive = true,
                    Notes = "כולל עמודת רשת נוספת בטבלה"
                },
                new ChainInfo
                {
                    Name = "שוק העיר (ט.ע.מ.ס) בע\"מ",
                    Prefix = "shuk-hayir",
                    BaseUrl = "https://shuk-hayir.binaprojects.com",
                    Type = ChainType.BinaProjects,
                    IsActive = true
                },
                new ChainInfo
                {
                    Name = "זול ובגדול בע\"מ",
                    Prefix = "zolvebegadol",
                    BaseUrl = "https://zolvebegadol.binaprojects.com",
                    Type = ChainType.BinaProjects,
                    IsActive = true
                },
                new ChainInfo
                {
                    Name = "קיי.טי. יבוא ושיווק בע\"מ (משנת יוסף)",
                    Prefix = "ktshivuk",
                    BaseUrl = "https://ktshivuk.binaprojects.com",
                    Type = ChainType.BinaProjects,
                    IsActive = true,
                    Notes = "יש גם קישור חלופי: https://chp-kt.pages.dev"
                }
            });

            return config;
        }
    }

    /// <summary>
    /// הגדרות כלליות למערכת ההורדות
    /// </summary>
    public class GeneralSettings
    {
        /// <summary>
        /// מספר הורדות מקביליות מקסימלי
        /// </summary>
        public int MaxConcurrentDownloads { get; set; } = 3;

        /// <summary>
        /// זמן timeout בדקות
        /// </summary>
        public int TimeoutMinutes { get; set; } = 10;

        /// <summary>
        /// זמן המתנה בין בקשות (במילישניות)
        /// </summary>
        public int DelayBetweenRequests { get; set; } = 100;

        /// <summary>
        /// מספר ניסיונות חוזרים במקרה של כישלון
        /// </summary>
        public int RetryAttempts { get; set; } = 3;

        /// <summary>
        /// האם לשמור קבצי ZIP לאחר חילוץ
        /// </summary>
        public bool KeepZipFiles { get; set; } = false;

        /// <summary>
        /// האם לפעול במצב verbose (הרבה לוגים)
        /// </summary>
        public bool VerboseLogging { get; set; } = true;

        /// <summary>
        /// נתיב ברירת מחדל לשמירת קבצים
        /// </summary>
        public string DefaultDownloadPath { get; set; } = "DownloadedFiles";

        /// <summary>
        /// האם לבצע ניקוי קבצים ישנים
        /// </summary>
        public bool CleanupOldFiles { get; set; } = true;

        /// <summary>
        /// מספר ימים לשמירת קבצים ישנים
        /// </summary>
        public int KeepFilesForDays { get; set; } = 7;

        /// <summary>
        /// שעה לתזמון הרצה יומית (24 שעות)
        /// </summary>
        public int ScheduledHour { get; set; } = 3;

        /// <summary>
        /// דקה לתזמון הרצה יומית
        /// </summary>
        public int ScheduledMinute { get; set; } = 0;
    }
}
