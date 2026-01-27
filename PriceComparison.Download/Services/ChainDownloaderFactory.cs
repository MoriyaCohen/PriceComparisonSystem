using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using PriceComparison.Download.Core;
using PriceComparison.Download.Chains;
using PriceComparison.Download.Models;
using PriceComparison.Download.Configuration;
using PriceComparison.Download.Exceptions;
using System.Text.Json;

namespace PriceComparison.Download.Services
{
    /// <summary>
    /// Factory לייצור מודולי הורדה מתאימים לכל רשת
    /// מטפל בטעינת התצורה ויצירת המודולים הנכונים
    /// </summary>
    public class ChainDownloaderFactory
    {
        #region Fields & Properties

        /// <summary>
        /// תצורת הרשתות
        /// </summary>
        private readonly ChainConfiguration _configuration;

        /// <summary>
        /// מטמון של מודולים שכבר נוצרו
        /// </summary>
        private readonly Dictionary<string, IChainDownloader> _downloadersCache;

        /// <summary>
        /// מנעול לגישה thread-safe למטמון
        /// </summary>
        private readonly object _cacheLock = new object();

        #endregion

        #region Constructors

        /// <summary>
        /// בנאי עם תצורה קיימת
        /// </summary>
        /// <param name="configuration">תצורת הרשתות</param>
        public ChainDownloaderFactory(ChainConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _downloadersCache = new Dictionary<string, IChainDownloader>();

            ValidateConfiguration();
        }

        /// <summary>
        /// בנאי ברירת מחדל - טוען תצורה מקובץ
        /// </summary>
        public ChainDownloaderFactory() : this(LoadConfigurationFromFile())
        {
        }

        /// <summary>
        /// בנאי עם נתיב מותאם אישית לקובץ תצורה
        /// </summary>
        /// <param name="configFilePath">נתיב לקובץ התצורה</param>
        public ChainDownloaderFactory(string configFilePath) : this(LoadConfigurationFromFile(configFilePath))
        {
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// יצירת מודול הורדה לרשת ספציפית
        /// </summary>
        /// <param name="chainName">שם הרשת או קוד הרשת</param>
        /// <returns>מודול ההורדה המתאים</returns>
        /// <exception cref="ChainDownloadException">אם הרשת לא נמצאה או לא נתמכת</exception>
        public IChainDownloader CreateDownloader(string chainName)
        {
            if (string.IsNullOrWhiteSpace(chainName))
            {
                throw new ArgumentException("שם הרשת לא יכול להיות ריק", nameof(chainName));
            }

            // בדיקה במטמון
            lock (_cacheLock)
            {
                var cacheKey = chainName.ToLower();
                if (_downloadersCache.ContainsKey(cacheKey))
                {
                    return _downloadersCache[cacheKey];
                }
            }

            // חיפוש מידע הרשת
            var chainInfo = _configuration.GetChainByName(chainName);
            if (chainInfo == null)
            {
                throw new ChainDownloadException($"רשת '{chainName}' לא נמצאה בתצורה");
            }

            if (!chainInfo.IsActive)
            {
                throw new ChainDownloadException($"רשת '{chainName}' אינה פעילה");
            }

            // יצירת המודול המתאים
            var downloader = CreateDownloaderInstance(chainInfo);

            // שמירה במטמון
            lock (_cacheLock)
            {
                var cacheKey = chainName.ToLower();
                if (!_downloadersCache.ContainsKey(cacheKey))
                {
                    _downloadersCache[cacheKey] = downloader;
                }
            }

            return downloader;
        }

        /// <summary>
        /// יצירת מודולי הורדה לכל הרשתות הפעילות
        /// </summary>
        /// <returns>רשימת מודולי הורדה</returns>
        public List<IChainDownloader> CreateAllActiveDownloaders()
        {
            var downloaders = new List<IChainDownloader>();
            var activeChains = _configuration.GetActiveChains();

            foreach (var chain in activeChains)
            {
                try
                {
                    var downloader = CreateDownloader(chain.Name);
                    downloaders.Add(downloader);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ לא ניתן ליצור מודול עבור {chain.Name}: {ex.Message}");
                }
            }

            return downloaders;
        }

        /// <summary>
        /// יצירת מודולי הורדה לרשתות ממספר סוגים
        /// </summary>
        /// <param name="chainTypes">סוגי רשתות</param>
        /// <returns>רשימת מודולי הורדה</returns>
        public List<IChainDownloader> CreateDownloadersByType(params ChainType[] chainTypes)
        {
            var downloaders = new List<IChainDownloader>();

            foreach (var chainType in chainTypes)
            {
                var chains = _configuration.GetChainsByType(chainType);
                foreach (var chain in chains)
                {
                    try
                    {
                        var downloader = CreateDownloader(chain.Name);
                        downloaders.Add(downloader);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ לא ניתן ליצור מודול עבור {chain.Name}: {ex.Message}");
                    }
                }
            }

            return downloaders;
        }

        /// <summary>
        /// קבלת רשימת כל הרשתות הזמינות
        /// </summary>
        /// <returns>רשימת מידע רשתות</returns>
        public List<ChainInfo> GetAvailableChains()
        {
            return _configuration.GetActiveChains();
        }

        /// <summary>
        /// בדיקה אם רשת ספציפית נתמכת ופעילה
        /// </summary>
        /// <param name="chainName">שם הרשת</param>
        /// <returns>true אם הרשת נתמכת ופעילה</returns>
        public bool IsChainSupported(string chainName)
        {
            return _configuration.IsChainActive(chainName);
        }

        /// <summary>
        /// ניקוי מטמון המודולים
        /// </summary>
        public void ClearCache()
        {
            lock (_cacheLock)
            {
                _downloadersCache.Clear();
            }
        }

        /// <summary>
        /// רענון התצורה מהקובץ
        /// </summary>
        /// <param name="configFilePath">נתיב לקובץ התצורה (אופציונלי)</param>
        public void RefreshConfiguration(string? configFilePath = null)
        {
            var newConfig = LoadConfigurationFromFile(configFilePath ?? "chains.json");

            // החלפת התצורה
            _configuration.Chains.Clear();
            _configuration.Chains.AddRange(newConfig.Chains);
            _configuration.General = newConfig.General;
            _configuration.Azure = newConfig.Azure;

            // ניקוי המטמון כדי שיווצרו מודולים חדשים
            ClearCache();

            ValidateConfiguration();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// יצירת מודול הורדה ספציפי לפי מידע הרשת
        /// </summary>
        /// <param name="chainInfo">מידע הרשת</param>
        /// <returns>מודול ההורדה</returns>
        /// <exception cref="ChainDownloadException">אם הרשת לא נתמכת</exception>
        private IChainDownloader CreateDownloaderInstance(ChainInfo chainInfo)
        {
            // עבור רשתות בינה פרוגקט - יצירה לפי prefix
            if (chainInfo.Type == ChainType.BinaProjects)
            {
                return chainInfo.Prefix.ToLower() switch
                {
                    "kingstore" => new KingStoreDownloader(),
                    "maayan2000" => new MaayanDownloader(),
                    "goodpharm" => new GoodPharmDownloader(),
                    "shefabirkathashem" => new ShefaBirkatHashemDownloader(),
                    "supersapir" => new SuperSapirDownloader(),
                    "shuk-hayir" => new ShukHayirDownloader(),
                    "zolvebegadol" => new ZolVeBegadolDownloader(),
                    "ktshivuk" => new KTDownloader(),
                    _ => throw new ChainDownloadException($"רשת בינה פרוגקט '{chainInfo.Prefix}' אינה נתמכת")
                };
            }

            // עבור סוגי רשתות אחרים - יוספו בעתיד
            throw new ChainDownloadException($"סוג רשת '{chainInfo.Type}' אינו נתמך עדיין");
        }

        /// <summary>
        /// טעינת תצורה מקובץ JSON
        /// </summary>
        /// <param name="filePath">נתיב הקובץ</param>
        /// <returns>תצורת הרשתות</returns>
        /// <exception cref="ConfigurationException">אם יש בעיה בטעינה או בתקינות התצורה</exception>
        private static ChainConfiguration LoadConfigurationFromFile(string filePath = "chains.json")
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"⚠️ קובץ התצורה {filePath} לא נמצא - יוצר תצורה ברירת מחדל");
                    var defaultConfig = ChainConfiguration.CreateDefault();
                    SaveConfigurationToFile(defaultConfig, filePath);
                    return defaultConfig;
                }

                var jsonContent = File.ReadAllText(filePath);
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                var configuration = JsonSerializer.Deserialize<ChainConfiguration>(jsonContent, options);

                if (configuration == null)
                {
                    throw new ConfigurationException("שגיאה בהמרת התצורה מ-JSON");
                }

                Console.WriteLine($"✅ נטענה תצורה עם {configuration.Chains.Count} רשתות מ-{filePath}");
                return configuration;
            }
            catch (JsonException ex)
            {
                throw new ConfigurationException($"שגיאה בניתוח קובץ התצורה {filePath}: {ex.Message}", ex);
            }
            catch (IOException ex)
            {
                throw new ConfigurationException($"שגיאה בקריאת קובץ התצורה {filePath}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// שמירת תצורה לקובץ JSON
        /// </summary>
        /// <param name="configuration">התצורה לשמירה</param>
        /// <param name="filePath">נתיב הקובץ</param>
        private static void SaveConfigurationToFile(ChainConfiguration configuration, string filePath)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                var jsonContent = JsonSerializer.Serialize(configuration, options);
                File.WriteAllText(filePath, jsonContent);

                Console.WriteLine($"💾 תצורה נשמרה ל-{filePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ שגיאה בשמירת התצורה: {ex.Message}");
            }
        }

        /// <summary>
        /// validation של התצורה
        /// </summary>
        /// <exception cref="ConfigurationException">אם יש שגיאות בתצורה</exception>
        private void ValidateConfiguration()
        {
            var errors = _configuration.ValidateConfiguration();

            if (errors.Any())
            {
                var errorMessage = "שגיאות בתצורה:\n" + string.Join("\n", errors);
                throw new ConfigurationException(errorMessage);
            }
        }

        #endregion

        #region Static Helper Methods

        /// <summary>
        /// יצירת Factory עם תצורה ברירת מחדל
        /// </summary>
        /// <returns>Factory מוכן לשימוש</returns>
        public static ChainDownloaderFactory CreateDefault()
        {
            return new ChainDownloaderFactory();
        }

        /// <summary>
        /// יצירת Factory עם רשתות ספציפיות בלבד
        /// </summary>
        /// <param name="chainPrefixes">קודי הרשתות הרצויות</param>
        /// <returns>Factory עם הרשתות הנבחרות</returns>
        public static ChainDownloaderFactory CreateForSpecificChains(params string[] chainPrefixes)
        {
            var fullConfig = ChainConfiguration.CreateDefault();
            var filteredConfig = new ChainConfiguration
            {
                General = fullConfig.General,
                Azure = fullConfig.Azure
            };

            foreach (var prefix in chainPrefixes)
            {
                var chain = fullConfig.Chains.FirstOrDefault(c =>
                    c.Prefix.Equals(prefix, StringComparison.OrdinalIgnoreCase));

                if (chain != null)
                {
                    filteredConfig.Chains.Add(chain);
                }
            }

            return new ChainDownloaderFactory(filteredConfig);
        }

        #endregion
    }
}
