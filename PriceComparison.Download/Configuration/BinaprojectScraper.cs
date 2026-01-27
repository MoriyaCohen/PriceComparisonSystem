/*using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PriceComparison.Download.Scrapers
{
    public class BinaprojectScraper
    {
        private readonly HttpClient _client;
        private readonly string _baseUrl;
        private readonly string _downloadDir;
        private readonly string _today;

        public BinaprojectScraper(string baseUrl, string downloadDir)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _downloadDir = downloadDir;
            _client = new HttpClient(new HttpClientHandler { UseCookies = true })
            {
                BaseAddress = new Uri(_baseUrl)
            };
            _today = DateTime.Now.ToString("dd/MM/yyyy");
        }

        public async Task DownloadTodayFilesAsync(string storeCode)
        {
            Console.WriteLine("📡 Getting list of available files from server...");
            var fileList = await GetFileListAsync(storeCode);
            Console.WriteLine($"📦 Found {fileList.Count} total files.");

            var todayFiles = fileList.FindAll(file => file.DateFile == _today &&
                (file.FileNm.Contains("StoresFull") || file.FileNm.Contains("PriceFull") || file.FileNm.Contains("PromoFull")));
            Console.WriteLine($"📅 Filtered down to {todayFiles.Count} files for today ({_today})");

            foreach (var file in todayFiles)
            {
                Console.WriteLine($"⬇️ מוריד: {file.FileNm}");
                var zipUrl = await GetZipUrlAsync(file.FileNm);
                if (!string.IsNullOrEmpty(zipUrl))
                {
                    await DownloadAndExtractZipAsync(zipUrl, file.FileNm);
                }
                else
                {
                    Console.WriteLine($"❌ לא נמצא קישור הורדה לקובץ: {file.FileNm}");
                }
            }
        }

        private async Task<List<BinaFileMetadata>> GetFileListAsync(string storeCode)
        {
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("WStore", storeCode),
                new KeyValuePair<string, string>("WDate", _today),
                new KeyValuePair<string, string>("WFileType", "0")
            });

            var response = await _client.PostAsync("MainIO_Hok.aspx", content);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<List<BinaFileMetadata>>(json);
        }

        private async Task<string> GetZipUrlAsync(string fileName)
        {
            var response = await _client.PostAsync($"Download.aspx?FileNm={fileName}", null);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();

            var obj = JsonSerializer.Deserialize<List<DownloadResponse>>(json);
            return obj?[0].SPath;
        }

        private async Task DownloadAndExtractZipAsync(string zipUrl, string fileName)
        {
            var zipData = await _client.GetByteArrayAsync(zipUrl);
            var tempPath = Path.Combine(_downloadDir, "Temp");
            Directory.CreateDirectory(tempPath);*/