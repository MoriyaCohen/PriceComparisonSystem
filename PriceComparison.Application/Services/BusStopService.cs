using System.IO;
using System.Linq;

namespace PriceComparison.Api.Services
{
    public class BusStopService : IBusStopService
    {
        private string _foundStopsPath = "";
        private string _baseFolder = "";

        public BusStopService()
        {
            FindPaths(); // חיפוש הנתיבים מתבצע בעת יצירת הסרוויס
        }

        public string GetBaseDataFolder() => _baseFolder;

        public (double Lat, double Lon)? GetStopLocation(string stopId)
        {
            if (string.IsNullOrEmpty(_foundStopsPath) || !File.Exists(_foundStopsPath))
                return null;

            var lines = File.ReadLines(_foundStopsPath); // יעיל יותר מ-ReadAllLines
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Contains('|') ? line.Split('|') : line.Split(',');

                if (parts.Length >= 6)
                {
                    string currentId = parts[0].Replace("\"", "").Trim();
                    if (currentId == stopId)
                    {
                        string val1 = parts[parts.Length - 2].Replace("\"", "").Trim();
                        string val2 = parts[parts.Length - 1].Replace("\"", "").Trim();

                        if (double.TryParse(val1, out double v1) && double.TryParse(val2, out double v2))
                        {
                            // לוגיקת תיקון קואורדינטות הפוכות שלך
                            double lat = (v1 >= 29 && v1 <= 34) ? v1 : v2;
                            double lon = (v1 >= 29 && v1 <= 34) ? v2 : v1;
                            return (lat, lon);
                        }
                    }
                }
            }
            return null;
        }

        // הלוגיקה המקורית שלך למציאת הקובץ
        private void FindPaths()
        {
            string baseSearchPath = @"C:\Users\ADMIN\projects";
            string directPath = @"C:\Users\ADMIN\projects\LocalXmlData";

            // בדיקה 1
            if (Directory.Exists(directPath))
            {
                _foundStopsPath = Directory.GetFiles(directPath, "stops.*").FirstOrDefault();
                if (!string.IsNullOrEmpty(_foundStopsPath)) _baseFolder = directPath;
            }

            // בדיקה 2
            if (string.IsNullOrEmpty(_foundStopsPath))
            {
                string adminInnerPath = Path.Combine(directPath, "ADMIN");
                if (Directory.Exists(adminInnerPath))
                {
                    _foundStopsPath = Directory.GetFiles(adminInnerPath, "stops.*").FirstOrDefault();
                    if (!string.IsNullOrEmpty(_foundStopsPath)) _baseFolder = adminInnerPath;
                }
            }

            // בדיקה 3 (חיפוש רחב)
            if (string.IsNullOrEmpty(_foundStopsPath))
            {
                try
                {
                    if (Directory.Exists(baseSearchPath))
                    {
                        _foundStopsPath = Directory.GetFiles(baseSearchPath, "stops.txt", SearchOption.AllDirectories).FirstOrDefault();
                        if (!string.IsNullOrEmpty(_foundStopsPath)) _baseFolder = Path.GetDirectoryName(_foundStopsPath);
                    }
                }
                catch { }
            }
        }
    }
}