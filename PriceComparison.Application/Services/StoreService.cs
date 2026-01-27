using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Net.Http;
using PriceComparison.Api.DTOs;

namespace PriceComparison.Api.Services
{
    public class StoreService : IStoreService
    {
        private readonly IBusStopService _busStopService;
        private readonly IHttpClientFactory _httpClientFactory; // הדרך הנכונה לעבוד עם HTTP

        public StoreService(IBusStopService busStopService, IHttpClientFactory httpClientFactory)
        {
            _busStopService = busStopService;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<List<NearbyStoreDto>> FindNearbyStores(double originLat, double originLon, double radiusKm)
        {
            string baseFolder = _busStopService.GetBaseDataFolder();
            if (string.IsNullOrEmpty(baseFolder)) throw new Exception("לא ניתן לאתר את תיקיית הנתונים הבסיסית.");

            string jsonPath = Path.Combine(baseFolder, "stores_coordinates.json");

            // יצירת הקובץ אם לא קיים
            if (!File.Exists(jsonPath))
            {
                await GenerateCoordinatesFile(baseFolder, jsonPath);
            }

            // קריאת הקובץ
            if (!File.Exists(jsonPath)) throw new Exception("קובץ stores_coordinates.json חסר ולא הצלחתי לייצר אותו.");

            string jsonContent = await File.ReadAllTextAsync(jsonPath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var stores = JsonSerializer.Deserialize<List<StoreInternalModel>>(jsonContent, options);

            if (stores == null) return new List<NearbyStoreDto>();

            var nearbyStores = new List<NearbyStoreDto>();

            foreach (var store in stores)
            {
                // סינון
                if (store.Name.Contains("אונליין") || store.Name.ToLower().Contains("online") || store.Name.Contains("מחסני"))
                    continue;

                double dist = CalcDistance(originLat, originLon, store.Lat, store.Lon);

                if (dist <= radiusKm)
                {
                    nearbyStores.Add(new NearbyStoreDto
                    {
                        StoreName = store.Name,
                        City = store.City,
                        Address = store.Address.Replace(" 0", "").Trim(),
                        DistanceKm = Math.Round(dist, 2),
                        MapLink = $"http://googleusercontent.com/maps.google.com/?q={store.Lat},{store.Lon}"
                    });
                }
            }

            return nearbyStores.OrderBy(x => x.DistanceKm).Take(5).ToList();
        }

        private async Task GenerateCoordinatesFile(string baseFolder, string jsonOutputPath)
        {
            // מציאת תיקיית ה-XML (לוגיקה מקורית שלך)
            string storesFolder = Path.Combine(baseFolder, "STORESFULL");
            if (!Directory.Exists(storesFolder))
            {
                string[] dirs = Directory.GetDirectories(baseFolder, "STORESFULL", SearchOption.AllDirectories);
                if (dirs.Length > 0) storesFolder = dirs[0];
                else throw new Exception("לא מצאתי את תיקיית STORESFULL.");
            }

            string xmlPath = Directory.GetFiles(storesFolder, "*.xml").FirstOrDefault();
            if (string.IsNullOrEmpty(xmlPath)) throw new Exception($"התיקייה {storesFolder} ריקה מקבצי XML.");

            // טעינת XML וגיאוקודינג
            var doc = XDocument.Load(xmlPath);
            var storesList = new List<StoreInternalModel>();

            using (var client = _httpClientFactory.CreateClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "PriceComparisonApp/1.0");
                client.Timeout = TimeSpan.FromSeconds(10);

                foreach (var item in doc.Descendants("Store"))
                {
                    var s = new StoreInternalModel
                    {
                        Id = item.Element("StoreId")?.Value,
                        Name = item.Element("StoreName")?.Value,
                        Address = item.Element("Address")?.Value,
                        City = item.Element("City")?.Value
                    };

                    if (string.IsNullOrWhiteSpace(s.Address) || s.Address.ToLower().Contains("unknown")) continue;

                    try
                    {
                        string cleanAddr = s.Address.Replace(" 0", "").Trim();
                        string url = $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(cleanAddr + ", " + s.City + ", Israel")}&format=json&limit=1";

                        await Task.Delay(1100); // Nominatim limit
                        var response = await client.GetStringAsync(url);

                        using (JsonDocument document = JsonDocument.Parse(response))
                        {
                            if (document.RootElement.ValueKind == JsonValueKind.Array && document.RootElement.GetArrayLength() > 0)
                            {
                                var first = document.RootElement[0];
                                if (double.TryParse(first.GetProperty("lat").GetString(), out double lat) &&
                                    double.TryParse(first.GetProperty("lon").GetString(), out double lon))
                                {
                                    s.Lat = lat; s.Lon = lon;
                                    storesList.Add(s);
                                }
                            }
                        }
                    }
                    catch { }
                }
            }

            var opts = new JsonSerializerOptions { WriteIndented = true };
            await File.WriteAllTextAsync(jsonOutputPath, JsonSerializer.Serialize(storesList, opts));
        }

        // הנוסחה המקורית שלך
        private double CalcDistance(double lat1, double lon1, double lat2, double lon2)
        {
            var R = 6371;
            var dLat = (lat2 - lat1) * (Math.PI / 180);
            var dLon = (lon2 - lon1) * (Math.PI / 180);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(lat1 * (Math.PI / 180)) * Math.Cos(lat2 * (Math.PI / 180)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }
    }
}