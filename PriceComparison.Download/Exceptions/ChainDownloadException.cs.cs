using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriceComparison.Download.Exceptions
{

        /// <summary>
        /// חריגה המתרחשת בעת בעיות בהורדה מרשת ספציפית
        /// </summary>
        public class ChainDownloadException : Exception
        {
            /// <summary>
            /// שם הרשת שבה התרחשה השגיאה
            /// </summary>
            public string? ChainName { get; }

            /// <summary>
            /// סוג השגיאה
            /// </summary>
            public DownloadErrorType ErrorType { get; }

            /// <summary>
            /// בנאי בסיסי
            /// </summary>
            public ChainDownloadException() : base()
            {
                ErrorType = DownloadErrorType.Unknown;
            }

            /// <summary>
            /// בנאי עם הודעה
            /// </summary>
            /// <param name="message">הודעת השגיאה</param>
            public ChainDownloadException(string message) : base(message)
            {
                ErrorType = DownloadErrorType.Unknown;
            }

            /// <summary>
            /// בנאי עם הודעה וחריגה פנימית
            /// </summary>
            /// <param name="message">הודעת השגיאה</param>
            /// <param name="innerException">החריגה הפנימית</param>
            public ChainDownloadException(string message, Exception innerException) : base(message, innerException)
            {
                ErrorType = DownloadErrorType.Unknown;
            }

            /// <summary>
            /// בנאי מלא עם כל הפרטים
            /// </summary>
            /// <param name="message">הודעת השגיאה</param>
            /// <param name="chainName">שם הרשת</param>
            /// <param name="errorType">סוג השגיאה</param>
            public ChainDownloadException(string message, string chainName, DownloadErrorType errorType) : base(message)
            {
                ChainName = chainName;
                ErrorType = errorType;
            }

            /// <summary>
            /// בנאי מלא עם חריגה פנימית
            /// </summary>
            /// <param name="message">הודעת השגיאה</param>
            /// <param name="chainName">שם הרשת</param>
            /// <param name="errorType">סוג השגיאה</param>
            /// <param name="innerException">החריגה הפנימית</param>
            public ChainDownloadException(string message, string chainName, DownloadErrorType errorType, Exception innerException)
                : base(message, innerException)
            {
                ChainName = chainName;
                ErrorType = errorType;
            }

            /// <summary>
            /// יצירת חריגה עבור רשת לא נמצאה
            /// </summary>
            /// <param name="chainName">שם הרשת</param>
            /// <returns>החריגה המתאימה</returns>
            public static ChainDownloadException ChainNotFound(string chainName)
            {
                return new ChainDownloadException(
                    $"רשת '{chainName}' לא נמצאה בתצורה או אינה פעילה",
                    chainName,
                    DownloadErrorType.ChainNotFound);
            }

            /// <summary>
            /// יצירת חריגה עבור בעיית חיבור
            /// </summary>
            /// <param name="chainName">שם הרשת</param>
            /// <param name="innerException">החריגה המקורית</param>
            /// <returns>החריגה המתאימה</returns>
            public static ChainDownloadException ConnectionError(string chainName, Exception innerException)
            {
                return new ChainDownloadException(
                    $"שגיאת חיבור לרשת '{chainName}': {innerException.Message}",
                    chainName,
                    DownloadErrorType.ConnectionError,
                    innerException);
            }

            /// <summary>
            /// יצירת חריגה עבור בעיית אימות
            /// </summary>
            /// <param name="chainName">שם הרשת</param>
            /// <returns>החריגה המתאימה</returns>
            public static ChainDownloadException AuthenticationError(string chainName)
            {
                return new ChainDownloadException(
                    $"שגיאת אימות עבור רשת '{chainName}' - בדוק פרטי התחברות",
                    chainName,
                    DownloadErrorType.AuthenticationError);
            }

            /// <summary>
            /// יצירת חריגה עבור קובץ לא נמצא
            /// </summary>
            /// <param name="chainName">שם הרשת</param>
            /// <param name="fileName">שם הקובץ</param>
            /// <returns>החריגה המתאימה</returns>
            public static ChainDownloadException FileNotFound(string chainName, string fileName)
            {
                return new ChainDownloadException(
                    $"קובץ '{fileName}' לא נמצא ברשת '{chainName}'",
                    chainName,
                    DownloadErrorType.FileNotFound);
            }

            /// <summary>
            /// יצירת חריגה עבור timeout
            /// </summary>
            /// <param name="chainName">שם הרשת</param>
            /// <returns>החריגה המתאימה</returns>
            public static ChainDownloadException TimeoutError(string chainName)
            {
                return new ChainDownloadException(
                    $"תם זמן ההמתנה עבור הורדה מרשת '{chainName}'",
                    chainName,
                    DownloadErrorType.Timeout);
            }

            /// <summary>
            /// יצירת חריגה עבור בעיית parsing
            /// </summary>
            /// <param name="chainName">שם הרשת</param>
            /// <param name="details">פרטים נוספים</param>
            /// <returns>החריגה המתאימה</returns>
            public static ChainDownloadException ParsingError(string chainName, string details)
            {
                return new ChainDownloadException(
                    $"שגיאה בפענוח נתונים מרשת '{chainName}': {details}",
                    chainName,
                    DownloadErrorType.ParsingError);
            }
        }

        /// <summary>
        /// חריגה המתרחשת בעת בעיות בתצורת המערכת
        /// </summary>
    }
