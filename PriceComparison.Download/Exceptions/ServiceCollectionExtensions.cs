//using Microsoft.Extensions.Configuration;
//using Microsoft.Extensions.DependencyInjection;
//using PriceComparison.Download.Services;
//using System.Net;

//namespace PriceComparison.Download.Extensions
//{
//    /// <summary>
//    /// הרחבות להוספת שירותי BinaProjects ל-DI Container
//    /// גרסה מתוקנת ומשופרת
//    /// </summary>
//    public static class ServiceCollectionExtensions
//    {
//        /// <summary>
//        /// הוספת שירותי BinaProjects למיכל ה-DI - גרסה מעודכנת
//        /// </summary>
//        /// <param name="services">מיכל השירותים</param>
//        /// <param name="configuration">קונפיגורציית האפליקציה</param>
//        /// <returns>מיכל השירותים לשרשור</returns>
//        public static IServiceCollection AddBinaProjectsServices(
//            this IServiceCollection services,
//            IConfiguration configuration)
//        {
//            // רישום HttpClient מותאם עבור BinaProjects
//            services.AddHttpClient<IBinaProjectsDownloadService, BinaProjectsDownloadService>(client =>
//            {
//                SetupHttpClientHeaders(client, configuration);
//            })
//            .ConfigurePrimaryHttpMessageHandler(() => CreateHttpClientHandler(configuration));

//            // רישום השירות עם Scoped Lifetime
//            services.AddScoped<IBinaProjectsDownloadService, BinaProjectsDownloadService>();

//            return services;
//        }

//        /// <summary>
//        /// הוספת שירותי BinaProjects עם הגדרות מותאמות אישית
//        /// </summary>
//        /// <param name="services">מיכל השירותים</param>
//        /// <param name="configuration">קונפיגורציית האפליקציה</param>
//        /// <param name="configureHttpClient">פונקציה להגדרת HttpClient</param>
//        /// <returns>מיכל השירותים לשרשור</returns>
//        public static IServiceCollection AddBinaProjectsServices(
//            this IServiceCollection services,
//            IConfiguration configuration,
//            Action<HttpClient>? configureHttpClient = null)
//        {
//            services.AddHttpClient<IBinaProjectsDownloadService, BinaProjectsDownloadService>(client =>
//            {
//                // הגדרות ברירת מחדל
//                SetupHttpClientHeaders(client, configuration);

//                // הפעלת הגדרות מותאמות אישית אם הועברו
//                configureHttpClient?.Invoke(client);
//            })
//            .ConfigurePrimaryHttpMessageHandler(() => CreateHttpClientHandler(configuration));

//            services.AddScoped<IBinaProjectsDownloadService, BinaProjectsDownloadService>();

//            return services;
//        }

//        /// <summary>
//        /// הוספת שירותי BinaProjects עם הגדרות ברירת מחדל
//        /// </summary>
//        /// <param name="services">מיכל השירותים</param>
//        /// <returns>מיכל השירותים לשרשור</returns>
//        public static IServiceCollection AddBinaProjectsServicesWithDefaults(
//            this IServiceCollection services)
//        {
//            services.AddHttpClient<IBinaProjectsDownloadService, BinaProjectsDownloadService>(client =>
//            {
//                // הגדרות ברירת מחדל קבועות
//                client.DefaultRequestHeaders.Clear();
//                client.DefaultRequestHeaders.Add("User-Agent",
//                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
//                client.DefaultRequestHeaders.Add("Accept", "*/*");
//                client.DefaultRequestHeaders.Add("Accept-Language", "he-IL,he;q=0.8");
//                client.Timeout = TimeSpan.FromSeconds(30);
//            })
//            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
//            {
//                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
//                UseCookies = false
//            });

//            services.AddScoped<IBinaProjectsDownloadService, BinaProjectsDownloadService>();

//            return services;
//        }

//        /// <summary>
//        /// הוספת שירותי BinaProjects עם ולידציה של הקונפיגורציה
//        /// </summary>
//        /// <param name="services">מיכל השירותים</param>
//        /// <param name="configuration">קונפיגורציית האפליקציה</param>
//        /// <param name="validateConfiguration">האם לבצע ולידציה</param>
//        /// <returns>מיכל השירותים לשרשור</returns>
//        public static IServiceCollection AddBinaProjectsServicesWithValidation(
//            this IServiceCollection services,
//            IConfiguration configuration,
//            bool validateConfiguration = true)
//        {
//            if (validateConfiguration)
//            {
//                ValidateBinaProjectsConfiguration(configuration);
//            }

//            return services.AddBinaProjectsServices(configuration);
//        }

//        /// <summary>
//        /// הגדרת Headers ברירת מחדל ל-HttpClient
//        /// </summary>
//        private static void SetupHttpClientHeaders(HttpClient client, IConfiguration configuration)
//        {
//            client.DefaultRequestHeaders.Clear();

//            client.DefaultRequestHeaders.Add("User-Agent",
//                configuration.GetValue<string>("BinaProjects:UserAgent",
//                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36"));

//            client.DefaultRequestHeaders.Add("Accept", "*/*");

//            client.DefaultRequestHeaders.Add("Accept-Language",
//                configuration.GetValue<string>("BinaProjects:AcceptLanguage", "he-IL,he;q=0.8"));

//            client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
//            client.DefaultRequestHeaders.Add("Connection", "keep-alive");

//            // הוספת headers נוספים מהקונפיגורציה אם קיימים
//            var additionalHeaders = configuration.GetSection("BinaProjects:AdditionalHeaders")
//                .GetChildren()
//                .ToDictionary(x => x.Key, x => x.Value);

//            foreach (var header in additionalHeaders)
//            {
//                if (!string.IsNullOrEmpty(header.Value))
//                {
//                    try
//                    {
//                        client.DefaultRequestHeaders.Add(header.Key, header.Value);
//                    }
//                    catch (Exception)
//                    {
//                        // התעלם מ-headers לא תקינים
//                    }
//                }
//            }

//            var timeoutSeconds = configuration.GetValue<int>("BinaProjects:TimeoutSeconds", 30);
//            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
//        }

//        /// <summary>
//        /// יצירת HttpClientHandler מותאם
//        /// </summary>
//        private static HttpClientHandler CreateHttpClientHandler(IConfiguration configuration)
//        {
//            return new HttpClientHandler()
//            {
//                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
//                UseCookies = configuration.GetValue<bool>("BinaProjects:UseCookies", false),
//                MaxConnectionsPerServer = configuration.GetValue<int>("BinaProjects:MaxConnectionsPerServer", 10),

//                // הגדרות SSL אם נדרש
//                ServerCertificateCustomValidationCallback = configuration.GetValue<bool>("BinaProjects:IgnoreSSLErrors", false)
//                    ? (sender, cert, chain, sslPolicyErrors) => true
//                    : null
//            };
//        }

//        /// <summary>
//        /// ולידציה של הגדרות BinaProjects
//        /// </summary>
//        private static void ValidateBinaProjectsConfiguration(IConfiguration configuration)
//        {
//            // בדיקת נתיבי קונפיגורציה חיוניים
//            var configPath = configuration.GetValue<string>("BinaProjects:ConfigFilePath",
//                "BinaProjectsNetworks.json");

//            if (!Path.IsPathRooted(configPath))
//            {
//                configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configPath);
//            }

//            if (!File.Exists(configPath))
//            {
//                throw new FileNotFoundException($"קובץ קונפיגורציית BinaProjects לא נמצא: {configPath}");
//            }

//            // בדיקת תיקיית הורדות
//            var downloadFolder = configuration.GetValue<string>("BinaProjects:DownloadFolder", "DownloadedFiles");
//            if (!Path.IsPathRooted(downloadFolder))
//            {
//                downloadFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, downloadFolder);
//            }

//            if (!Directory.Exists(downloadFolder))
//            {
//                try
//                {
//                    Directory.CreateDirectory(downloadFolder);
//                }
//                catch (Exception ex)
//                {
//                    throw new DirectoryNotFoundException($"לא ניתן ליצור תיקיית הורדות: {downloadFolder}. שגיאה: {ex.Message}");
//                }
//            }

//            // בדיקת הגדרות timeout
//            var timeoutSeconds = configuration.GetValue<int>("BinaProjects:TimeoutSeconds", 30);
//            if (timeoutSeconds <= 0 || timeoutSeconds > 300)
//            {
//                throw new ArgumentOutOfRangeException(nameof(timeoutSeconds),
//                    "Timeout חייב להיות בין 1 ל-300 שניות");
//            }
//        }
//    }

//    /// <summary>
//    /// קלאס עזר להגדרות BinaProjects - מעודכן
//    /// </summary>
//    public static class BinaProjectsConfigurationHelper
//    {
//        /// <summary>
//        /// קבלת נתיב קובץ קונפיגורציית הרשתות
//        /// </summary>
//        public static string GetNetworkConfigPath(IConfiguration configuration)
//        {
//            var configPath = configuration.GetValue<string>("BinaProjects:ConfigFilePath",
//                "BinaProjectsNetworks.json");

//            if (!Path.IsPathRooted(configPath))
//            {
//                configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configPath);
//            }

//            return configPath;
//        }

//        /// <summary>
//        /// קבלת נתיב תיקיית הורדות
//        /// </summary>
//        public static string GetDownloadFolderPath(IConfiguration configuration)
//        {
//            var downloadFolder = configuration.GetValue<string>("BinaProjects:DownloadFolder", "DownloadedFiles");

//            if (!Path.IsPathRooted(downloadFolder))
//            {
//                downloadFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, downloadFolder);
//            }

//            return downloadFolder;
//        }

//        /// <summary>
//        /// בדיקה האם הגדרות BinaProjects תקינות
//        /// </summary>
//        public static bool IsConfigurationValid(IConfiguration configuration, out List<string> errors)
//        {
//            errors = new List<string>();

//            try
//            {
//                // בדיקת קובץ קונפיגורציה
//                var configPath = GetNetworkConfigPath(configuration);
//                if (!File.Exists(configPath))
//                {
//                    errors.Add($"קובץ קונפיגורציית רשתות לא נמצא: {configPath}");
//                }

//                // בדיקת timeout
//                var timeout = configuration.GetValue<int>("BinaProjects:TimeoutSeconds", 30);
//                if (timeout <= 0 || timeout > 300)
//                {
//                    errors.Add("Timeout חייב להיות בין 1 ל-300 שניות");
//                }

//                // בדיקת תיקיית הורדות
//                var downloadFolder = GetDownloadFolderPath(configuration);
//                var parentDir = Path.GetDirectoryName(downloadFolder);
//                if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
//                {
//                    errors.Add($"תיקיית אב של תיקיית ההורדות לא קיימת: {parentDir}");
//                }
//            }
//            catch (Exception ex)
//            {
//                errors.Add($"שגיאה בבדיקת קונפיגורציה: {ex.Message}");
//            }

//            return !errors.Any();
//        }

//        /// <summary>
//        /// 🆕 בדיקת תקינות רשת ספציפית
//        /// </summary>
//        public static async Task<bool> TestNetworkConnectivityAsync(string networkUrl, int timeoutSeconds = 10)
//        {
//            try
//            {
//                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(timeoutSeconds) };
//                var response = await client.GetAsync(networkUrl);
//                return response.IsSuccessStatusCode;
//            }
//            catch
//            {
//                return false;
//            }
//        }

//        /// <summary>
//        /// 🆕 יצירת תיקיות הורדה אוטומטית
//        /// </summary>
//        public static void EnsureDownloadFoldersExist(string baseFolder)
//        {
//            if (!Directory.Exists(baseFolder))
//                Directory.CreateDirectory(baseFolder);

//            var subFolders = new[] { "StoresFull", "PriceFull", "PromoFull", "ZIP_Files", "Raw_Data", "Logs" };
//            foreach (var folder in subFolders)
//            {
//                var path = Path.Combine(baseFolder, folder);
//                if (!Directory.Exists(path))
//                    Directory.CreateDirectory(path);
//            }
//        }
//    }
//}