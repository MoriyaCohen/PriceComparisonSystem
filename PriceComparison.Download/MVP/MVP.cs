// === MVP Strategy Pattern ל-3 רשתות בינה פרוגקט ===

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json;
using System.IO;

namespace PriceComparison.Download.MVP
{
    // שלב 1: Interface אחיד לכל הרשתות
    public interface IChainDownloader
    {
        string ChainName { get; }
        bool CanHandle(string chainId);
        Task<DownloadResult> DownloadChain(ChainConfig config);
        Task<List<FileMetadata>> GetAvailableFiles(string date);
    }

    // שלב 2: מודל הגדרות רשת
    public class ChainConfig
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string BaseUrl { get; set; } = "";
        public string Prefix { get; set; } = "";
        public bool HasNetworkColumn { get; set; } = false; // עבור סופר ספיר
        public bool Enabled { get; set; } = true;
    }

    // שלב 3: מודל תוצאות הורדה
    public class DownloadResult
    {
        public string ChainName { get; set; } = "";
        public int DownloadedFiles { get; set; }
        public List<string> DownloadedFileNames { get; set; } = new();
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = "";
        public DateTime DownloadTime { get; set; } = DateTime.Now;
    }

    // שלב 4: מחלקת בסיס לבינה פרוגקט (מחליפה את הקוד הקיים)
    public abstract class BinaProjectsDownloaderBase : IChainDownloader
    {
        protected readonly HttpClient _httpClient;
        protected readonly string _downloadDirectory;

        public abstract string ChainName { get; }
        protected abstract string BaseUrl { get; }
        protected abstract string ChainPrefix { get; }
        protected virtual bool HasNetworkColumn => false;

        protected BinaProjectsDownloaderBase()
        {
            _httpClient = new HttpClient();
            _downloadDirectory = Path.Combine(Environment.CurrentDirectory, "Downloads");
            SetupHttpClient();
        }

        public abstract bool CanHandle(string chainId);

        public virtual async Task<DownloadResult> DownloadChain(ChainConfig config)
        {
            var result = new DownloadResult
            {
                ChainName = ChainName,
                Success = true
            };

            try
            {
                Console.WriteLine($"🏪 מתחיל הורדה: {ChainName}");
                Console.WriteLine($"🔗 מ: {BaseUrl}");

                var today = DateTime.Now.ToString("dd/MM/yyyy");

                // 1. הורדת StoresFull העדכני ביותר
                Console.WriteLine("📋 מוריד StoresFull...");
                await DownloadLatestStoresFull(today, result);

                // 2. זיהוי סניפים זמינים
                Console.WriteLine("🔍 מזהה סניפים...");
                var stores = await GetAvailableStores(today);
                Console.WriteLine($"📍 נמצאו {stores.Count} סניפים");

                // 3. הורדת PriceFull לכל סניף
                Console.WriteLine("💰 מוריד PriceFull...");
                await DownloadPriceFullForStores(today, stores, result);

                // 4. הורדת PromoFull לכל סניף  
                Console.WriteLine("🎁 מוריד PromoFull...");
                await DownloadPromoFullForStores(today, stores, result);

                Console.WriteLine($"✅ {ChainName}: הורדו {result.DownloadedFiles} קבצים");
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                Console.WriteLine($"❌ שגיאה ב-{ChainName}: {ex.Message}");
                return result;
            }
        }

        public virtual async Task<List<FileMetadata>> GetAvailableFiles(string date)
        {
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("WStore", "0"),
                new KeyValuePair<string, string>("WDate", date),
                new KeyValuePair<string, string>("WFileType", "0")
            });

            _httpClient.BaseAddress = new Uri(BaseUrl);
            var response = await _httpClient.PostAsync("MainIO_Hok.aspx", content);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<List<FileMetadata>>(json) ?? new List<FileMetadata>();
        }

        // שיטות פרטיות משותפות
        private void SetupHttpClient()
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        }

        private async Task DownloadLatestStoresFull(string date, DownloadResult result)
        {
            var files = await GetAvailableFiles(date);
            var storesFiles = files.Where(f => f.FileNm.Contains("StoresFull"))
                                  .OrderByDescending(f => ExtractTimeFromFileName(f.FileNm))
                                  .ToList();

            if (storesFiles.Any())
            {
                var latest = storesFiles.First();
                await DownloadAndExtractFile(latest.FileNm);
                result.DownloadedFiles++;
                result.DownloadedFileNames.Add(latest.FileNm);
            }
        }

        private async Task<List<string>> GetAvailableStores(string date)
        {
            var files = await GetAvailableFiles(date);
            var stores = new HashSet<string>();

            foreach (var file in files.Where(f => f.FileNm.Contains("Price") || f.FileNm.Contains("Promo")))
            {
                var parts = file.FileNm.Split('-');
                if (parts.Length >= 2)
                {
                    stores.Add(parts[1]);
                }
            }

            return stores.ToList();
        }

        private async Task DownloadPriceFullForStores(string date, List<string> stores, DownloadResult result)
        {
            var files = await GetAvailableFiles(date);

            foreach (var store in stores)
            {
                var priceFiles = files.Where(f =>
                    f.FileNm.Contains("PriceFull") &&
                    f.FileNm.Contains($"-{store}-"))
                    .OrderByDescending(f => ExtractTimeFromFileName(f.FileNm))
                    .ToList();

                if (priceFiles.Any())
                {
                    var latest = priceFiles.First();
                    await DownloadAndExtractFile(latest.FileNm);
                    result.DownloadedFiles++;
                    result.DownloadedFileNames.Add(latest.FileNm);
                }
            }
        }

        private async Task DownloadPromoFullForStores(string date, List<string> stores, DownloadResult result)
        {
            var files = await GetAvailableFiles(date);

            foreach (var store in stores)
            {
                var promoFiles = files.Where(f =>
                    f.FileNm.Contains("PromoFull") &&
                    f.FileNm.Contains($"-{store}-"))
                    .OrderByDescending(f => ExtractTimeFromFileName(f.FileNm))
                    .ToList();

                if (promoFiles.Any())
                {
                    var latest = promoFiles.First();
                    await DownloadAndExtractFile(latest.FileNm);
                    result.DownloadedFiles++;
                    result.DownloadedFileNames.Add(latest.FileNm);
                }
            }
        }

        private async Task DownloadAndExtractFile(string fileName)
        {
            try
            {
                var response = await _httpClient.PostAsync($"Download.aspx?FileNm={fileName}", null);
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();

                var downloadData = JsonSerializer.Deserialize<List<DownloadResponse>>(json);
                if (downloadData?.Any() != true) return;

                var downloadUrl = downloadData[0].SPath;
                var zipData = await _httpClient.GetByteArrayAsync(downloadUrl);

                var chainDir = Path.Combine(_downloadDirectory, ChainName);
                Directory.CreateDirectory(chainDir);

                var zipPath = Path.Combine(chainDir, $"{fileName}.zip");
                await File.WriteAllBytesAsync(zipPath, zipData);

                var extractPath = Path.Combine(chainDir, "XML", fileName);
                Directory.CreateDirectory(extractPath);

                System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, extractPath);
                File.Delete(zipPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ שגיאה בהורדת {fileName}: {ex.Message}");
            }
        }

        private string ExtractTimeFromFileName(string fileName)
        {
            var parts = fileName.Split('-');
            return parts.Length >= 3 ? parts[2] : fileName;
        }
    }

    // שלב 5: מימושים ספציפיים לכל רשת

    // קינג סטור - הבסיס
    public class KingStoreDownloader : BinaProjectsDownloaderBase
    {
        public override string ChainName => "קינג סטור";
        protected override string BaseUrl => "https://kingstore.binaprojects.com";
        protected override string ChainPrefix => "KingStore";

        public override bool CanHandle(string chainId)
        {
            return chainId.Equals("kingstore", StringComparison.OrdinalIgnoreCase);
        }
    }

    // מעיין אלפיים - פשוט
    public class MaayanDownloader : BinaProjectsDownloaderBase
    {
        public override string ChainName => "מעיין אלפיים";
        protected override string BaseUrl => "https://maayan2000.binaprojects.com";
        protected override string ChainPrefix => "Maayan";

        public override bool CanHandle(string chainId)
        {
            return chainId.Equals("maayan", StringComparison.OrdinalIgnoreCase);
        }
    }

    // סופר ספיר - עם עמודת רשת
    public class SuperSapirDownloader : BinaProjectsDownloaderBase
    {
        public override string ChainName => "סופר ספיר";
        protected override string BaseUrl => "https://supersapir.binaprojects.com";
        protected override string ChainPrefix => "SuperSapir";
        protected override bool HasNetworkColumn => true; // ההבדל הייחודי

        public override bool CanHandle(string chainId)
        {
            return chainId.Equals("supersapir", StringComparison.OrdinalIgnoreCase);
        }

        // Override לטיפול בעמודת רשת (לעתיד)
        public override async Task<List<FileMetadata>> GetAvailableFiles(string date)
        {
            var baseFiles = await base.GetAvailableFiles(date);

            // כאן נוסיף לוגיקה מיוחדת לטיפול בעמודת הרשת
            // לעת עתה פשוט נחזיר את הבסיס
            return baseFiles;
        }
    }

    // שלב 6: Factory לניהול המופעים
    public class ChainDownloaderFactory
    {
        private readonly List<IChainDownloader> _downloaders;

        public ChainDownloaderFactory()
        {
            _downloaders = new List<IChainDownloader>
            {
                new KingStoreDownloader(),
                new MaayanDownloader(),
                new SuperSapirDownloader()
            };
        }

        public IChainDownloader? GetDownloader(string chainId)
        {
            return _downloaders.FirstOrDefault(d => d.CanHandle(chainId));
        }

        public List<IChainDownloader> GetAllDownloaders()
        {
            return _downloaders.ToList();
        }

        public List<string> GetSupportedChains()
        {
            return new List<string> { "kingstore", "maayan", "supersapir" };
        }
    }

    // מודלים נדרשים
    public class FileMetadata
    {
        public string FileNm { get; set; } = "";
        public string WStore { get; set; } = "";
        public string WFileType { get; set; } = "";
        public string DateFile { get; set; } = "";
    }

    public class DownloadResponse
    {
        public string SPath { get; set; } = "";
    }
}
