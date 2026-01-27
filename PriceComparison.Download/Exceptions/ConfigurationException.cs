using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriceComparison.Download.Exceptions
{
    /// <summary>
    /// חריגה המתרחשת בעת בעיות בתצורת המערכת
    /// </summary>
    public class ConfigurationException : Exception
    {
        /// <summary>
        /// שם הקובץ או הרכיב שגרם לשגיאה
        /// </summary>
        public string? ConfigurationSource { get; }

        /// <summary>
        /// רשימת שגיאות ספציפיות
        /// </summary>
        public List<string> ValidationErrors { get; }

        /// <summary>
        /// בנאי בסיסי
        /// </summary>
        public ConfigurationException() : base()
        {
            ValidationErrors = new List<string>();
        }

        /// <summary>
        /// בנאי עם הודעה
        /// </summary>
        /// <param name="message">הודעת השגיאה</param>
        public ConfigurationException(string message) : base(message)
        {
            ValidationErrors = new List<string>();
        }

        /// <summary>
        /// בנאי עם הודעה וחריגה פנימית
        /// </summary>
        /// <param name="message">הודעת השגיאה</param>
        /// <param name="innerException">החריגה הפנימית</param>
        public ConfigurationException(string message, Exception innerException) : base(message, innerException)
        {
            ValidationErrors = new List<string>();
        }

        /// <summary>
        /// בנאי עם מקור התצורה
        /// </summary>
        /// <param name="message">הודעת השגיאה</param>
        /// <param name="configurationSource">מקור התצורה</param>
        public ConfigurationException(string message, string configurationSource) : base(message)
        {
            ConfigurationSource = configurationSource;
            ValidationErrors = new List<string>();
        }

        /// <summary>
        /// בנאי עם רשימת שגיאות
        /// </summary>
        /// <param name="message">הודעת השגיאה הכללית</param>
        /// <param name="validationErrors">רשימת שגיאות ספציפיות</param>
        public ConfigurationException(string message, List<string> validationErrors) : base(message)
        {
            ValidationErrors = validationErrors ?? new List<string>();
        }

        /// <summary>
        /// בנאי מלא
        /// </summary>
        /// <param name="message">הודעת השגיאה</param>
        /// <param name="configurationSource">מקור התצורה</param>
        /// <param name="validationErrors">רשימת שגיאות</param>
        /// <param name="innerException">חריגה פנימית</param>
        public ConfigurationException(string message, string configurationSource, List<string> validationErrors, Exception? innerException = null)
            : base(message, innerException)
        {
            ConfigurationSource = configurationSource;
            ValidationErrors = validationErrors ?? new List<string>();
        }

        /// <summary>
        /// קבלת הודעת שגיאה מפורטת עם כל השגיאות
        /// </summary>
        /// <returns>הודעה מפורטת</returns>
        public string GetDetailedMessage()
        {
            var message = Message;

            if (!string.IsNullOrEmpty(ConfigurationSource))
            {
                message += $"\nמקור: {ConfigurationSource}";
            }

            if (ValidationErrors.Any())
            {
                message += "\nשגיאות ספציפיות:";
                foreach (var error in ValidationErrors)
                {
                    message += $"\n  • {error}";
                }
            }

            return message;
        }

        /// <summary>
        /// יצירת חריגה עבור קובץ תצורה לא נמצא
        /// </summary>
        /// <param name="filePath">נתיב הקובץ</param>
        /// <returns>החריגה המתאימה</returns>
        public static ConfigurationException FileNotFound(string filePath)
        {
            return new ConfigurationException(
                $"קובץ התצורה לא נמצא: {filePath}",
                filePath);
        }

        /// <summary>
        /// יצירת חריגה עבור JSON לא תקין
        /// </summary>
        /// <param name="filePath">נתיב הקובץ</param>
        /// <param name="jsonException">השגיאה המקורית</param>
        /// <returns>החריגה המתאימה</returns>
        public static ConfigurationException InvalidJson(string filePath, Exception jsonException)
        {
            return new ConfigurationException(
                $"קובץ התצורה מכיל JSON לא תקין: {jsonException.Message}",
                filePath,
                new List<string>(),
                jsonException);
        }

        /// <summary>
        /// יצירת חריגה עבור שגיאות validation
        /// </summary>
        /// <param name="source">מקור התצורה</param>
        /// <param name="errors">רשימת השגיאות</param>
        /// <returns>החריגה המתאימה</returns>
        public static ConfigurationException ValidationFailed(string source, List<string> errors)
        {
            return new ConfigurationException(
                $"התצורה מכילה {errors.Count} שגיאות",
                source,
                errors);
        }

        /// <summary>
        /// יצירת חריגה עבור רכיב חסר
        /// </summary>
        /// <param name="componentName">שם הרכיב החסר</param>
        /// <returns>החריגה המתאימה</returns>
        public static ConfigurationException MissingComponent(string componentName)
        {
            return new ConfigurationException(
                $"רכיב נדרש חסר בתצורה: {componentName}",
                componentName);
        }
    }

    /// <summary>
    /// סוגי שגיאות הורדה
    /// </summary>
    public enum DownloadErrorType
    {
        /// <summary>
        /// שגיאה לא ידועה
        /// </summary>
        Unknown,

        /// <summary>
        /// רשת לא נמצאה או לא פעילה
        /// </summary>
        ChainNotFound,

        /// <summary>
        /// בעיית חיבור לשרת
        /// </summary>
        ConnectionError,

        /// <summary>
        /// בעיית אימות
        /// </summary>
        AuthenticationError,

        /// <summary>
        /// קובץ לא נמצא
        /// </summary>
        FileNotFound,

        /// <summary>
        /// תם זמן ההמתנה
        /// </summary>
        Timeout,

        /// <summary>
        /// שגיאה בפענוח נתונים
        /// </summary>
        ParsingError,

        /// <summary>
        /// שגיאה בכתיבה לדיסק
        /// </summary>
        FileSystemError,

        /// <summary>
        /// שגיאה בחילוץ ZIP
        /// </summary>
        ExtractionError,

        /// <summary>
        /// שגיאה ברשת (HTTP)
        /// </summary>
        NetworkError,

        /// <summary>
        /// שירות לא זמין
        /// </summary>
        ServiceUnavailable
    }
}
