using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using System.Linq;
using PriceComparison.Download.New.Storage;
using PriceComparison.Download.New.BinaProject;


namespace PriceComparison.Download.New.Laibcatalog
{
    public class LaibcatalogDownloader : IChainDownloader
    {
        private readonly HttpClient _httpClient;
        private readonly FileManager _fileManager;

        public LaibcatalogDownloader(HttpClient httpClient, FileManager fileManager)
        {
            _httpClient = httpClient;
            _fileManager = fileManager;
        }

        public string ChainName => "Laibcatalog Chains";
        public string ChainId => "laibcatalog";

        public bool CanHandle(string chainId)
        {
            return chainId.Equals("laibcatalog", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<List<FileMetadata>> GetAvailableFiles(string date)
        {
            string baseUrl = "https://laibcatalog.co.il";
            string apiUrl = $"{baseUrl}/GetFiles?date={date}";

            var response = await _httpClient.GetStringAsync(apiUrl);
            return JsonSerializer.Deserialize<List<FileMetadata>>(response) ?? new List<FileMetadata>();
        }

        public async Task<DownloadResult> DownloadChain(ChainConfig config, string date)
        {
            var result = new DownloadResult { ChainName = config.Name };
            try
            {
                var files = await GetAvailableFiles(date);
                if (!files.Any())
                {
                    result.Success = false;
                    result.ErrorMessage = "No files available for download";
                    return result;
                }

                var latestFiles = files
                    .GroupBy(f => new { f.WStore, f.FileType })
                    .Select(g => g.OrderByDescending(f => DateTime.Parse(f.LastUpdateDate)).First());

                foreach (var file in latestFiles)
                {
                    var fileUrl = $"https://laibcatalog.co.il/files/{file.FileNm}";
                    var fileContent = await _httpClient.GetByteArrayAsync(fileUrl);

                    string savePath = _fileManager.CombinePath("Downloads", config.Prefix, file.WStore, file.FileType, file.FileNm);
                    _fileManager.CreateDirectory(_fileManager.GetDirectoryName(savePath));
                    await _fileManager.WriteAllBytesAsync(savePath, fileContent);

                    result.DownloadedFiles++;

                    if (file.FileType.Contains("Store")) result.StoresFiles++;
                    else if (file.FileType.Contains("Price")) result.PriceFiles++;
                    else if (file.FileType.Contains("Promo")) result.PromoFiles++;
                }

                result.Success = true;
                result.Duration = latestFiles.Count();
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }
    }
}
