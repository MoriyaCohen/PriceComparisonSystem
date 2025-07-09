/*using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.IO.Compression;

namespace PriceComparison.Download.Services
{
    /// <summary>
    /// מנהל קבצים ותיקיות משותף לכל הרשתות
    /// תומך במבנה היררכי: ChainName/SubChain/FileType/Files
    /// </summary>
    public class FileManager
    {
        public string BaseDirectory { get; private set; }

        public FileManager()
        {
            BaseDirectory = Path.Combine(Environment.CurrentDirectory, "DownloadedFiles");
        }

        /// <summary>
        /// יצירת תיקיות בסיס לכל הרשתות עם תמיכה בתת-רשתות
        /// מבנה חדש: DownloadedFiles/[ChainName]/[SubChain]/[FileType]/
        /// </summary>
        public async Task CreateBaseDirectories()
        {
            Console.WriteLine("📁 יוצר מבנה תיקיות מעודכן...");

            var directories = new[]
            {
                // תיקיית בסיס
                BaseDirectory,
                
                // קינג סטור - ללא תת-רשתות (מבנה ישן)
                Path.Combine(BaseDirectory, "KingStore"),
                Path.Combine(BaseDirectory, "KingStore", "StoresFull"),
                Path.Combine(BaseDirectory, "KingStore", "PriceFull"),
                Path.Combine(BaseDirectory, "KingStore", "PromoFull"),
                Path.Combine(BaseDirectory, "KingStore", "XML"),
                
                // רמי לוי - עם תת-רשתות
                Path.Combine(BaseDirectory, "RamiLevi"),
                
                // רמי לוי - תת-רשת רמי לוי
                Path.Combine(BaseDirectory, "RamiLevi", "RamiLevi"),
                Path.Combine(BaseDirectory, "RamiLevi", "RamiLevi", "Stores"),
                Path.Combine(BaseDirectory, "RamiLevi", "RamiLevi", "PriceFull"),
                Path.Combine(BaseDirectory, "RamiLevi", "RamiLevi", "PromoFull"),
                Path.Combine(BaseDirectory, "RamiLevi", "RamiLevi", "XML"),
                
                // רמי לוי - תת-רשת סופר קופיקס
                Path.Combine(BaseDirectory, "RamiLevi", "SuperCofix"),
                Path.Combine(BaseDirectory, "RamiLevi", "SuperCofix", "Stores"),
                Path.Combine(BaseDirectory, "RamiLevi", "SuperCofix", "PriceFull"),
                Path.Combine(BaseDirectory, "RamiLevi", "SuperCofix", "PromoFull"),
                Path.Combine(BaseDirectory, "RamiLevi", "SuperCofix", "XML"),
                
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
        /// תומך במבנה: chainName/subChain/fileType/fileName
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
        /// קובע אוטומטית את מיקום ה-XML לפי מבנה הנתיב
        /// </summary>
        public async Task SaveXml(string relativePath, string xmlContent, string chainName)
        {
            // זיהוי אם יש תת-רשת בנתיב
            var pathParts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            string xmlPath;
            if (pathParts.Length >= 2)
            {
                // יש תת-רשת: chainName/subChain/XML/fileName
                var subChain = pathParts[0];
                var fileName = Path.GetFileName(relativePath);
                xmlPath = Path.Combine(BaseDirectory, chainName, subChain, "XML", fileName);
            }
            else
            {
                // אין תת-רשת: chainName/XML/fileName
                var fileName = Path.GetFileName(relativePath);
                xmlPath = Path.Combine(BaseDirectory, chainName, "XML", fileName);
            }

            var directory = Path.GetDirectoryName(xmlPath);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            await File.WriteAllTextAsync(xmlPath, xmlContent, Encoding.UTF8);
        }

        /// <summary>
        /// חילוץ XML מ-ZIP ושמירה במיקום הנכון
        /// </summary>
        public async Task<bool> ExtractAndSaveXml(byte[] zipBytes, string fileName, string chainName)
        {
            try
            {
                using var zipStream = new MemoryStream(zipBytes);
                using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

                foreach (var entry in archive.Entries)
                {
                    if (entry.Name.ToLower().EndsWith(".xml"))
                    {
                        using var entryStream = entry.Open();
                        using var reader = new StreamReader(entryStream, Encoding.UTF8);
                        var xmlContent = await reader.ReadToEndAsync();

                        var xmlFileName = $"{Path.GetFileNameWithoutExtension(fileName)}_{DateTime.Now:yyyyMMdd_HHmmss}.xml";
                        await SaveXml(xmlFileName, xmlContent, chainName);

                        Console.WriteLine($"📄 XML נשמר: {xmlFileName} ({xmlContent.Length:N0} תווים)");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ שגיאה בחילוץ XML: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// קבלת סיכום קבצים לרשת (כולל כל התת-רשתות)
        /// </summary>
        public (int FileCount, long TotalSize) GetChainSummary(string chainName)
        {
            try
            {
                var chainDir = Path.Combine(BaseDirectory, chainName);
                if (!Directory.Exists(chainDir))
                    return (0, 0);

                var files = Directory.GetFiles(chainDir, "*", SearchOption.AllDirectories);
                var totalSize = 0L;

                foreach (var file in files)
                {
                    totalSize += new FileInfo(file).Length;
                }

                return (files.Length, totalSize);
            }
            catch
            {
                return (0, 0);
            }
        }

        /// <summary>
        /// קבלת סיכום לתת-רשת ספציפית
        /// </summary>
        public (int FileCount, long TotalSize) GetSubChainSummary(string chainName, string subChainName)
        {
            try
            {
                var subChainDir = Path.Combine(BaseDirectory, chainName, subChainName);
                if (!Directory.Exists(subChainDir))
                    return (0, 0);

                var files = Directory.GetFiles(subChainDir, "*", SearchOption.AllDirectories);
                var totalSize = 0L;

                foreach (var file in files)
                {
                    totalSize += new FileInfo(file).Length;
                }

                return (files.Length, totalSize);
            }
            catch
            {
                return (0, 0);
            }
        }

        /// <summary>
        /// מחיקת קבצים ישנים (לעתיד - למערכת החלפה יומית)
        /// </summary>
        public async Task CleanOldFiles(string chainName, int daysToKeep = 3)
        {
            try
            {
                var chainDir = Path.Combine(BaseDirectory, chainName);
                if (!Directory.Exists(chainDir))
                    return;

                var cutoffDate = DateTime.Now.AddDays(-daysToKeep);
                var files = Directory.GetFiles(chainDir, "*", SearchOption.AllDirectories);

                int deletedCount = 0;
                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.CreationTime < cutoffDate)
                    {
                        File.Delete(file);
                        deletedCount++;
                    }
                }

                Console.WriteLine($"🗑️ נמחקו {deletedCount} קבצים ישנים מ-{chainName}");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ שגיאה במחיקת קבצים ישנים: {ex.Message}");
            }
        }

        /// <summary>
        /// יצירת דוח סיכום קבצים
        /// </summary>
        public async Task<string> GenerateFileSummaryReport()
        {
            try
            {
                var report = new StringBuilder();
                report.AppendLine("📊 דוח סיכום קבצים");
                report.AppendLine($"📅 תאריך: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
                report.AppendLine("═" + new string('═', 50));

                // סיכום כללי
                var (totalFiles, totalSize) = GetChainSummary("");
                report.AppendLine($"סה\"כ קבצים: {totalFiles}");
                report.AppendLine($"סה\"כ גודל: {totalSize / (1024.0 * 1024.0):F1} MB");
                report.AppendLine();

                // פירוט לפי רשתות
                var chains = new[] { "KingStore", "RamiLevi" };
                foreach (var chain in chains)
                {
                    var (chainFiles, chainSize) = GetChainSummary(chain);
                    report.AppendLine($"🏪 {chain}:");
                    report.AppendLine($"   קבצים: {chainFiles}");
                    report.AppendLine($"   גודל: {chainSize / (1024.0 * 1024.0):F1} MB");

                    // אם זה רמי לוי, הוסף פירוט תת-רשתות
                    if (chain == "RamiLevi")
                    {
                        var subChains = new[] { "RamiLevi", "SuperCofix" };
                        foreach (var subChain in subChains)
                        {
                            var (subFiles, subSize) = GetSubChainSummary(chain, subChain);
                            if (subFiles > 0)
                            {
                                report.AppendLine($"   └─ {subChain}: {subFiles} קבצים ({subSize / (1024.0 * 1024.0):F1} MB)");
                            }
                        }
                    }

                    report.AppendLine();
                }

                return report.ToString();
            }
            catch (Exception ex)
            {
                return $"❌ שגיאה ביצירת דוח: {ex.Message}";
            }
        }
    }
}*/
