using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace PriceComparison.Download.Storage
{
    /// <summary>
    /// מנהל קבצים ותיקיות משותף לכל הרשתות
    /// </summary>
    public class FileManager
    {
        public string BaseDirectory { get; private set; }

        public FileManager()
        {
            BaseDirectory = Path.Combine(Environment.CurrentDirectory, "DownloadedFiles");
        }

        /// <summary>
        /// יצירת תיקיות בסיס לכל הרשתות
        /// </summary>
        public async Task CreateBaseDirectories()
        {
            Console.WriteLine("📁 יוצר מבנה תיקיות...");

            var directories = new[]
            {
                // תיקיית בסיס
                BaseDirectory,
                
                // שופרסל
                Path.Combine(BaseDirectory, "Shufersal"),
                Path.Combine(BaseDirectory, "Shufersal", "Stores"),
                Path.Combine(BaseDirectory, "Shufersal", "Price"),
                Path.Combine(BaseDirectory, "Shufersal", "PriceFull"),
                Path.Combine(BaseDirectory, "Shufersal", "Promo"),
                Path.Combine(BaseDirectory, "Shufersal", "PromoFull"),
                Path.Combine(BaseDirectory, "Shufersal", "XML"),
                Path.Combine(BaseDirectory, "Shufersal", "ZIP"),
                
                // משנת יוסף
                Path.Combine(BaseDirectory, "MishnatYosef"),
                Path.Combine(BaseDirectory, "MishnatYosef", "Stores"),
                Path.Combine(BaseDirectory, "MishnatYosef", "Price"),
                Path.Combine(BaseDirectory, "MishnatYosef", "PriceFull"),
                Path.Combine(BaseDirectory, "MishnatYosef", "Promo"),
                Path.Combine(BaseDirectory, "MishnatYosef", "PromoFull"),
                Path.Combine(BaseDirectory, "MishnatYosef", "XML"),
                Path.Combine(BaseDirectory, "MishnatYosef", "ZIP"),
                
                // סופר פארם
                Path.Combine(BaseDirectory, "SuperPharm"),
                Path.Combine(BaseDirectory, "SuperPharm", "Stores"),
                Path.Combine(BaseDirectory, "SuperPharm", "Price"),
                Path.Combine(BaseDirectory, "SuperPharm", "PriceFull"),
                Path.Combine(BaseDirectory, "SuperPharm", "Promo"),
                Path.Combine(BaseDirectory, "SuperPharm", "PromoFull"),
                Path.Combine(BaseDirectory, "SuperPharm", "XML"),
                Path.Combine(BaseDirectory, "SuperPharm", "ZIP"),
                
                // וולט
                Path.Combine(BaseDirectory, "Wolt"),
                Path.Combine(BaseDirectory, "Wolt", "Stores"),
                Path.Combine(BaseDirectory, "Wolt", "Price"),
                Path.Combine(BaseDirectory, "Wolt", "PriceFull"),
                Path.Combine(BaseDirectory, "Wolt", "Promo"),
                Path.Combine(BaseDirectory, "Wolt", "PromoFull"),
                Path.Combine(BaseDirectory, "Wolt", "XML"),
                Path.Combine(BaseDirectory, "Wolt", "ZIP"),
                
                // תיקיות עזר
                Path.Combine(BaseDirectory, "Raw"),
                Path.Combine(BaseDirectory, "Logs")
            };

            foreach (var dir in directories)
            {
                Directory.CreateDirectory(dir);
            }

            Console.WriteLine($"✅ נוצרו {directories.Length} תיקיות");
            await Task.CompletedTask;
        }

        /// <summary>
        /// שמירת קובץ עם יצירת תיקיה אוטומטית
        /// </summary>
        public async Task SaveFile(string relativePath, byte[] content, string chainName)
        {
            var fullPath = Path.Combine(BaseDirectory, chainName, relativePath);
            var directory = Path.GetDirectoryName(fullPath);

            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            await File.WriteAllBytesAsync(fullPath, content);
        }

        /// <summary>
        /// שמירת XML עם encoding נכון
        /// </summary>
        public async Task SaveXml(string relativePath, string xmlContent, string chainName)
        {
            var fileName = Path.GetFileName(relativePath);
            var xmlPath = Path.Combine(BaseDirectory, chainName, "XML", fileName);

            var directory = Path.GetDirectoryName(xmlPath);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            // שמירה עם UTF-8 encoding
            await File.WriteAllTextAsync(xmlPath, xmlContent, Encoding.UTF8);
        }

        /// <summary>
        /// קבלת נתיב מלא לקובץ
        /// </summary>
        public string GetFullPath(string relativePath, string chainName)
        {
            return Path.Combine(BaseDirectory, chainName, relativePath);
        }

        /// <summary>
        /// בדיקה אם קובץ קיים
        /// </summary>
        public bool FileExists(string relativePath, string chainName)
        {
            string fullPath = GetFullPath(relativePath, chainName);
            return File.Exists(fullPath);
        }
    }
}