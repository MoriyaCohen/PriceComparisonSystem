using System;
using System.IO;
using System.Threading.Tasks;
using PriceComparison.Download.New.Shufersal;

namespace PriceComparison.Download.New.Storage
{
    /// <summary>
    /// מנהל קבצים פשוט
    /// </summary>
    public class FileManager
    {
        /// <summary>
        /// יצירת תיקייה
        /// </summary>
        public void CreateDirectory(string path)
        {
            Directory.CreateDirectory(path);
        }

        /// <summary>
        /// כתיבת קובץ
        /// </summary>
        public async Task WriteAllBytesAsync(string path, byte[] bytes)
        {
            await File.WriteAllBytesAsync(path, bytes);
        }

        /// <summary>
        /// כתיבת טקסט לקובץ
        /// </summary>
        public async Task WriteAllTextAsync(string path, string text)
        {
            await File.WriteAllTextAsync(path, text);
        }

        /// <summary>
        /// בדיקה אם קובץ קיים
        /// </summary>
        public bool FileExists(string path)
        {
            return File.Exists(path);
        }

        /// <summary>
        /// בדיקה אם תיקייה קיימת
        /// </summary>
        public bool DirectoryExists(string path)
        {
            return Directory.Exists(path);
        }

        /// <summary>
        /// מחיקת קובץ
        /// </summary>
        public void DeleteFile(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// קבלת שם תיקייה
        /// </summary>
        public string GetDirectoryName(string path)
        {
            return Path.GetDirectoryName(path) ?? "";
        }

        /// <summary>
        /// שילוב נתיבים
        /// </summary>
        public string CombinePath(params string[] paths)
        {
            return Path.Combine(paths);
        }
    }
}