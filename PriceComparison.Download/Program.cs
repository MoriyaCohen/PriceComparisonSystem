using PriceComparison.Download.Core;
using PriceComparison.Download.Services;
using PriceComparison.Download.Configuration;
using PriceComparison.Download.Models;
using PriceComparison.Download.Exceptions;

using System.Globalization;
#nullable enable

namespace PriceComparison.Download
{
    /// <summary>
    /// תוכנית ראשית מעודכנת - משלבת את הפונקציונליות הקיימת של King Store עם מערכת מודולרית
    /// תומכת בהרצה אינטראקטיבית, תזמון אוטומטי ושורת פקודה
    /// מבוססת על הלוגיקה המוצלחת של UniversalBinaProjectsDownloader
    /// </summary>
    class Program
    {
        #region Fields - שדות מערכת

        private static ChainDownloaderFactory? _downloaderFactory;
        private static DownloadCoordinator? _downloadCoordinator;
        private static SchedulerService? _schedulerService;
        private static AzureStorageService? _azureStorageService;
        private static FileProcessingService? _fileProcessingService;
        private static ChainConfiguration? _configuration;
        private static bool _isRunning = true;

        // הגדרות זמניות מהגרסה הקודמת
        private static readonly TimeSpan SCHEDULE_TIME = new TimeSpan(6, 0, 0); // 06:00 בבוקר

        #endregion

        #region Main Method - נקודת כניסה ראשית

        static async Task Main(string[] args)
        {
            try
            {
                Console.OutputEncoding = System.Text.Encoding.UTF8;

                // הצגת כותרת מעודכנת עם מידע על הגרסה
                DisplayWelcomeMessage();

                // אתחול המערכת עם כל הרשתות
                if (!await InitializeServicesAsync())
                {
                    Console.WriteLine("❌ כישלון באתחול המערכת. התוכנית תסתיים.");
                    return;
                }

                // טיפול בארגומנטים של שורת הפקודה
                if (args.Length > 0)
                {
                    await HandleCommandLineArgumentsAsync(args);
                    return;
                }

                // הצגת תפריט אינטראקטיבי מעודכן
                await RunInteractiveMenuAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"💥 שגיאה קטלנית: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"   פרטים: {ex.InnerException.Message}");
                }
            }
            finally
            {
                await CleanupServicesAsync();
                Console.WriteLine("\n👋 להתראות!");

                if (System.Diagnostics.Debugger.IsAttached)
                {
                    Console.WriteLine("לחץ כל מקש לסגירה...");
                    Console.ReadKey();
                }
            }
        }

        #endregion

        #region Initialization - אתחול מערכת

        /// <summary>
        /// הצגת הודעת ברוכים הבאים מעודכנת
        /// </summary>
        private static void DisplayWelcomeMessage()
        {
            Console.Clear();
            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║       🌐 BinaProjects Universal Downloader - גרסה 3.0      ║");
            Console.WriteLine("║              השוואת מחירים טלפונית - מערכת מאוחדת           ║");
            Console.WriteLine("║                   מבוסס על King Store + מבנה מודולרי        ║");
            Console.WriteLine("║                        תומך ב-8 רשתות עיקריות               ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
            Console.WriteLine();
            Console.WriteLine("🔧 מאתחל מערכת מעודכנת...");
            Console.WriteLine("📋 תכונות: הורדה מקבילית, תזמון, Azure Storage, בדיקות זמינות");
        }

        /// <summary>
        /// אתחול כל השירותים עם תמיכה ברשתות נוספות
        /// </summary>
        /// <returns>האם האתחול הצליח</returns>
        private static async Task<bool> InitializeServicesAsync()
        {
            try
            {
                // טעינת תצורה מעודכנת
                Console.Write("📋 טוען תצורה מעודכנת... ");
                _configuration = LoadConfiguration();
                Console.WriteLine($"✅ נטענו {_configuration.Chains.Count} רשתות");

                // אתחול Factory עם רשתות נוספות
                Console.Write("🏭 מאתחל Factory מעודכן... ");
                _downloaderFactory = new ChainDownloaderFactory(_configuration);
                Console.WriteLine("✅");

                // אתחול Coordinator עם יכולות משופרות
                Console.Write("🎯 מאתחל מתאם הורדות משופר... ");
                _downloadCoordinator = new DownloadCoordinator(_downloaderFactory);
                Console.WriteLine("✅");

                // אתחול Scheduler
                Console.Write("⏰ מאתחל שירות תזמון... ");
                _schedulerService = new SchedulerService(_downloadCoordinator, _configuration);
                Console.WriteLine("✅");

                // אתחול File Processing
                Console.Write("⚙️ מאתחל עיבוד קבצים... ");
                _fileProcessingService = new FileProcessingService();
                Console.WriteLine("✅");

                // אתחול Azure Storage (אם הוגדר)
                if (!string.IsNullOrEmpty(_configuration.Azure.ConnectionString))
                {
                    Console.Write("☁️ מאתחל Azure Storage... ");
                    _azureStorageService = new AzureStorageService(_configuration.Azure);
                    await _azureStorageService.InitializeAsync();
                    Console.WriteLine("✅");
                }
                else
                {
                    Console.WriteLine("ℹ️ Azure Storage לא הוגדר - נשמור קבצים מקומית בלבד");
                }

                // רישום אירועים
                RegisterEventHandlers();

                // בדיקה מהירה של זמינות רשתות
                Console.Write("🔍 בודק זמינות רשתות... ");
                var activeNetworks = await CheckActiveNetworksQuick();
                Console.WriteLine($"✅ {activeNetworks} רשתות פעילות");

                Console.WriteLine("🎉 המערכת מוכנה לשימוש!");
                Console.WriteLine();

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ שגיאה: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// בדיקה מהירה של זמינות רשתות
        /// </summary>
        /// <returns>מספר רשתות פעילות</returns>
        private static async Task<int> CheckActiveNetworksQuick()
        {
            try
            {
                var report = await _downloadCoordinator!.CheckAllChainsAvailabilityAsync();
                return report.AvailableChains;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// טעינת תצורה מעודכנת עם ערכי ברירת מחדל חכמים
        /// </summary>
        /// <returns>תצורת המערכת</returns>
        private static ChainConfiguration LoadConfiguration()
        {
            try
            {
                if (File.Exists("chains.json"))
                {
                    var json = File.ReadAllText("chains.json");
                    var config = System.Text.Json.JsonSerializer.Deserialize<ChainConfiguration>(json,
                        new System.Text.Json.JsonSerializerOptions
                        {
                            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                        });

                    return config ?? ChainConfiguration.CreateDefault();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ שגיאה בטעינת תצורה: {ex.Message}");
            }

            return ChainConfiguration.CreateDefault();
        }

        /// <summary>
        /// רישום מטפלי אירועים
        /// </summary>
        private static void RegisterEventHandlers()
        {
            if (_downloadCoordinator != null)
            {
                _downloadCoordinator.ProgressUpdated += OnDownloadProgress;
                _downloadCoordinator.TaskCompleted += OnTaskCompleted;
            }

            if (_schedulerService != null)
            {
                _schedulerService.ScheduledRunStarted += OnScheduledRunStarted;
                _schedulerService.ScheduledRunCompleted += OnScheduledRunCompleted;
            }
        }

        /// <summary>
        /// ניקוי משאבים
        /// </summary>
        private static async Task CleanupServicesAsync()
        {
            try
            {
                _schedulerService?.Stop();
                _azureStorageService?.Dispose();
                // ניקוי נוסף...
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ שגיאה בניקוי: {ex.Message}");
            }
        }

        #endregion

        #region Interactive Menu - תפריט אינטראקטיבי

        /// <summary>
        /// הרצת תפריט אינטראקטיבי מעודכן
        /// </summary>
        private static async Task RunInteractiveMenuAsync()
        {
            while (_isRunning)
            {
                DisplayMainMenu();
                var choice = Console.ReadKey(true).KeyChar;
                Console.WriteLine();

                try
                {
                    await HandleMenuChoiceAsync(choice);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ שגיאה: {ex.Message}");
                    Console.WriteLine("לחץ כל מקש להמשך...");
                    Console.ReadKey(true);
                }
            }
        }

        /// <summary>
        /// הצגת התפריט הראשי המעודכן
        /// </summary>
        private static void DisplayMainMenu()
        {
            Console.Clear();
            var activeChains = _downloaderFactory?.GetAvailableChains().Count ?? 0;

            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                    🌐 תפריט ראשי - גרסה 3.0                ║");
            Console.WriteLine($"║                       {activeChains} רשתות זמינות                           ║");
            Console.WriteLine("╠══════════════════════════════════════════════════════════════╣");
            Console.WriteLine("║                                                              ║");
            Console.WriteLine("║  1️⃣  הרצה מיידית - הורדה מכל הרשתות עכשיו                  ║");
            Console.WriteLine("║  2️⃣  הרצה עבור תאריך ספציפי                               ║");
            Console.WriteLine("║  3️⃣  הרצה מרשתות נבחרות                                   ║");
            Console.WriteLine("║  4️⃣  הורדה חכמה (חיפוש בתאריכים שונים)                   ║");
            Console.WriteLine("║  5️⃣  התחלת תזמון אוטומטי יומי (06:00)                      ║");
            Console.WriteLine("║  6️⃣  עצירת תזמון                                           ║");
            Console.WriteLine("║  7️⃣  בדיקת זמינות רשתות מפורטת                            ║");
            Console.WriteLine("║  8️⃣  הצגת סטטוס מערכת ומשאבים                             ║");
            Console.WriteLine("║  9️⃣  בדיקת קבצים זמינים (ללא הורדה)                       ║");
            Console.WriteLine("║  A️⃣  ניהול הגדרות ו-Azure Storage                          ║");
            Console.WriteLine("║  B️⃣  ניקוי קבצים ישנים ותחזוקה                            ║");
            Console.WriteLine("║  C️⃣  מצב King Store קלאסי (כמו הגרסה הקודמת)              ║");
            Console.WriteLine("║  0️⃣  יציאה                                                 ║");
            Console.WriteLine("║                                                              ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
            Console.Write("\n🎯 בחר אפשרות (0-9, A-C): ");
        }

        /// <summary>
        /// טיפול בבחירת תפריט מעודכנת
        /// </summary>
        /// <param name="choice">הבחירה</param>
        private static async Task HandleMenuChoiceAsync(char choice)
        {
            switch (char.ToUpper(choice))
            {
                case '1':
                    await RunImmediateDownloadAsync();
                    break;
                case '2':
                    await RunDateSpecificDownloadAsync();
                    break;
                case '3':
                    await RunSelectedChainsDownloadAsync();
                    break;
                case '4':
                    await RunSmartDownloadAsync();
                    break;
                case '5':
                    await StartSchedulerAsync();
                    break;
                case '6':
                    StopScheduler();
                    break;
                case '7':
                    await CheckChainsAvailabilityDetailedAsync();
                    break;
                case '8':
                    await DisplaySystemStatusAsync();
                    break;
                case '9':
                    await CheckAvailableFilesAsync();
                    break;
                case 'A':
                    await ManageSettingsAsync();
                    break;
                case 'B':
                    await CleanupOldFilesAsync();
                    break;
                case 'C':
                    await RunClassicKingStoreModeAsync();
                    break;
                case '0':
                    _isRunning = false;
                    break;
                default:
                    Console.WriteLine("❌ בחירה לא תקינה");
                    Thread.Sleep(1000);
                    break;
            }
        }

        #endregion

        #region Menu Actions - פעולות תפריט

        /// <summary>
        /// הרצה מיידית מעודכנת עם מעקב התקדמות
        /// </summary>
        private static async Task RunImmediateDownloadAsync()
        {
            Console.WriteLine("🚀 מתחיל הרצה מיידית מכל הרשתות...");
            Console.WriteLine("📋 משתמש בלוגיקה המוצלחת של King Store, מורחבת לכל הרשתות");
            Console.WriteLine("⏱️ זמן הרצה משוער: 5-15 דקות תלוי במספר הרשתות");
            Console.WriteLine();

            var result = await _downloadCoordinator!.DownloadFromAllChainsAsync();

            DisplayDownloadResults(result);

            // הצעה להעלאה ל-Azure
            if (_azureStorageService != null && result.IsSuccess)
            {
                Console.Write("\n☁️ האם להעלות ל-Azure Storage? (y/n): ");
                var uploadChoice = Console.ReadKey(true).KeyChar;
                if (char.ToLower(uploadChoice) == 'y')
                {
                    Console.WriteLine("\n🔄 מעלה ל-Azure...");
                    await UploadResultsToAzureAsync(result.Results);
                }
            }

            PauseForUser();
        }

        /// <summary>
        /// הרצה עבור תאריך ספציפי מעודכנת
        /// </summary>
        private static async Task RunDateSpecificDownloadAsync()
        {
            Console.Write("📅 הזן תאריך (dd/MM/yyyy) או Enter עבור היום: ");
            var dateInput = Console.ReadLine();

            string targetDate;
            if (string.IsNullOrWhiteSpace(dateInput))
            {
                targetDate = DateTime.Now.ToString("dd/MM/yyyy");
            }
            else
            {
                if (!DateTime.TryParseExact(dateInput, "dd/MM/yyyy", null,
                    DateTimeStyles.None, out _))
                {
                    Console.WriteLine("❌ פורמט תאריך לא תקין");
                    PauseForUser();
                    return;
                }
                targetDate = dateInput;
            }

            Console.WriteLine($"🚀 מתחיל הרצה עבור תאריך {targetDate}...");
            Console.WriteLine("💡 אם התאריך רחוק מהיום, ייתכן שלא יהיו קבצים זמינים");

            var result = await _downloadCoordinator!.DownloadFromAllChainsAsync(targetDate);

            DisplayDownloadResults(result);
            PauseForUser();
        }

        /// <summary>
        /// הרצה מרשתות נבחרות מעודכנת
        /// </summary>
        private static async Task RunSelectedChainsDownloadAsync()
        {
            var availableChains = _downloaderFactory!.GetAvailableChains();

            Console.WriteLine("📝 רשתות זמינות:");
            for (int i = 0; i < availableChains.Count; i++)
            {
                var status = "🟢"; // נניח שהרשת פעילה
                Console.WriteLine($"  {i + 1:D2}. {status} {availableChains[i].Name}");
            }

            Console.WriteLine("\n💡 דוגמאות לבחירה:");
            Console.WriteLine("  1,3,5    - לבחירת רשתות ספציפיות");
            Console.WriteLine("  1-5      - לבחירת טווח רשתות");
            Console.WriteLine("  all      - לבחירת כל הרשתות");
            Console.WriteLine("  kingstore- לבחירת קינג סטור בלבד");

            Console.Write("\n🎯 הזן בחירה: ");
            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
            {
                Console.WriteLine("❌ לא נבחרו רשתות");
                PauseForUser();
                return;
            }

            var selectedChains = ParseChainSelection(input, availableChains);

            if (!selectedChains.Any())
            {
                Console.WriteLine("❌ לא נבחרו רשתות תקינות");
                PauseForUser();
                return;
            }

            Console.WriteLine($"🚀 מתחיל הרצה מ-{selectedChains.Count} רשתות נבחרות...");

            var result = await _downloadCoordinator!.DownloadFromSpecificChainsAsync(selectedChains);

            DisplayDownloadResults(result);
            PauseForUser();
        }

        /// <summary>
        /// הורדה חכמה עם חיפוש בתאריכים - תכונה חדשה ומתקדמת!
        /// </summary>
        private static async Task RunSmartDownloadAsync()
        {
            Console.WriteLine("🧠 הורדה חכמה - מחפש את התאריך הטוב ביותר לכל רשת");
            Console.WriteLine("📅 יחפש בתאריכים: היום, אתמול, שלשום, שבוע, שבועיים");
            Console.WriteLine("⚡ יבחר את התאריך עם הכי הרבה קבצים עבור כל רשת");
            Console.WriteLine();

            var testDates = new[]
            {
                DateTime.Now.ToString("dd/MM/yyyy"),                    // היום
                DateTime.Now.AddDays(-1).ToString("dd/MM/yyyy"),        // אתמול
                DateTime.Now.AddDays(-2).ToString("dd/MM/yyyy"),        // שלשום
                DateTime.Now.AddDays(-7).ToString("dd/MM/yyyy"),        // שבוע אחורה
                DateTime.Now.AddDays(-14).ToString("dd/MM/yyyy")        // שבועיים
            };

            var availableChains = _downloaderFactory!.GetAvailableChains();
            var smartResults = new List<DownloadResult>();

            foreach (var chain in availableChains)
            {
                Console.WriteLine($"🔍 מחפש קבצים עבור {chain.Name}...");

                string bestDate = null;
                int maxFiles = 0;

                // חיפוש התאריך עם הכי הרבה קבצים
                foreach (var testDate in testDates)
                {
                    try
                    {
                        var downloader = _downloaderFactory.CreateDownloader(chain.Name);
                        var stats = await downloader.GetDownloadStatisticsAsync(testDate);
                        var fileCount = stats.StoresFullCount + stats.PriceFullCount + stats.PromoFullCount;

                        Console.WriteLine($"   📅 {testDate}: {fileCount} קבצים");

                        if (fileCount > maxFiles)
                        {
                            maxFiles = fileCount;
                            bestDate = testDate;
                        }

                        await Task.Delay(300); // המתנה בין בדיקות
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"   ⚠️ {testDate}: שגיאה - {ex.Message}");
                    }
                }

                if (!string.IsNullOrEmpty(bestDate) && maxFiles > 0)
                {
                    Console.WriteLine($"   ✅ נבחר תאריך {bestDate} עם {maxFiles} קבצים");
                    var result = await _downloadCoordinator!.DownloadFromSpecificChainsAsync(
                        new[] { chain.Name }, bestDate);
                    smartResults.AddRange(result.Results);
                }
                else
                {
                    Console.WriteLine("   ❌ לא נמצאו קבצים בכל התאריכים");
                }

                Console.WriteLine();
            }

            // הצגת תוצאות מאוחדות
            var combinedResult = new CoordinatorResult
            {
                StartTime = DateTime.Now,
                EndTime = DateTime.Now,
                IsSuccess = smartResults.Any(r => r.IsSuccess),
                TotalRequests = smartResults.Count
            };
            combinedResult.Results.AddRange(smartResults);

            DisplayDownloadResults(combinedResult, "🧠 תוצאות הורדה חכמה");
            PauseForUser();
        }

        /// <summary>
        /// מצב King Store קלאסי - הרצה כמו הגרסה הקודמת
        /// </summary>
        private static async Task RunClassicKingStoreModeAsync()
        {
            Console.WriteLine("👑 מצב King Store קלאסי - מדמה את הגרסה המקורית");
            Console.WriteLine("📋 ירוץ בדיוק כמו UniversalBinaProjectsDownloader המקורי");
            Console.WriteLine("🎯 יוריד רק מקינג סטור: StoresFull + PriceFull + PromoFull");
            Console.WriteLine();

            try
            {
                var kingStoreDownloader = _downloaderFactory!.CreateDownloader("King Store");
                var todayDate = DateTime.Now.ToString("dd/MM/yyyy");

                Console.WriteLine($"📅 מעבד תאריך: {todayDate}");
                Console.WriteLine("🏢 שלב 1: הורדת קובץ סניפים (StoresFull)...");

                // הרצה עם הלוגיקה הקלאסית
                var classicResult = await _downloadCoordinator!.DownloadFromSpecificChainsAsync(
                    new[] { "King Store" }, todayDate);

                if (classicResult.IsSuccess)
                {
                    Console.WriteLine("✅ הורדה קלאסית הושלמה בהצלחה!");
                    Console.WriteLine("📊 תוצאות זהות לגרסה המקורית של UniversalBinaProjectsDownloader");
                }
                else
                {
                    Console.WriteLine("❌ הורדה קלאסית נכשלה");
                }

                DisplayDownloadResults(classicResult, "👑 תוצאות מצב King Store קלאסי");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ שגיאה במצב קלאסי: {ex.Message}");
            }

            PauseForUser();
        }

        /// <summary>
        /// התחלת תזמון אוטומטי יומי
        /// </summary>
        private static async Task StartSchedulerAsync()
        {
            Console.WriteLine("⏰ מפעיל תזמון אוטומטי יומי...");
            Console.WriteLine($"🕕 ירוץ כל יום בשעה {SCHEDULE_TIME:hh\\:mm}");
            Console.WriteLine("📋 יוריד מכל הרשתות הזמינות");

            try
            {
                if (_schedulerService!.StartFromConfiguration())
                {
                    var status = _schedulerService.GetStatus();
                    Console.WriteLine("✅ התזמון הופעל בהצלחה!");

                    if (status.NextScheduledRun.HasValue)
                    {
                        Console.WriteLine($"🔜 הרצה הבאה: {status.NextScheduledRun:dd/MM/yyyy HH:mm}");
                        Console.WriteLine($"⏱️ זמן המתנה: {status.FormattedTimeUntilNext}");
                    }

                    Console.WriteLine("🎛️ התזמון פועל ברקע - התוכנית תמשיך לרוץ");
                }
                else
                {
                    Console.WriteLine("❌ כישלון בהפעלת התזמון");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ שגיאה בהפעלת תזמון: {ex.Message}");
            }

            PauseForUser();
        }

        /// <summary>
        /// עצירת התזמון
        /// </summary>
        private static void StopScheduler()
        {
            Console.WriteLine("🛑 עוצר תזמון אוטומטי...");

            try
            {
                _schedulerService!.Stop();
                Console.WriteLine("✅ התזמון נעצר בהצלחה");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ שגיאה בעצירת תזמון: {ex.Message}");
            }

            PauseForUser();
        }

        #endregion

        #region Helper Methods - שיטות עזר

        /// <summary>
        /// פענוח בחירת רשתות מהקלט
        /// </summary>
        private static List<string> ParseChainSelection(string input, List<ChainInfo> availableChains)
        {
            var selectedChains = new List<string>();

            // בדיקות מיוחדות
            if (input.ToLower() == "all")
            {
                return availableChains.Select(c => c.Name).ToList();
            }

            if (input.ToLower() == "kingstore")
            {
                return new List<string> { "King Store" };
            }

            var parts = input.Split(',', StringSplitOptions.RemoveEmptyEntries);

            foreach (var part in parts)
            {
                var trimmedPart = part.Trim();

                // טיפול בטווח (1-5)
                if (trimmedPart.Contains('-'))
                {
                    var rangeParts = trimmedPart.Split('-');
                    if (rangeParts.Length == 2 &&
                        int.TryParse(rangeParts[0], out int start) &&
                        int.TryParse(rangeParts[1], out int end))
                    {
                        for (int i = start; i <= end && i <= availableChains.Count; i++)
                        {
                            if (i >= 1)
                            {
                                selectedChains.Add(availableChains[i - 1].Name);
                            }
                        }
                    }
                }
                // טיפול במספר יחיד
                else if (int.TryParse(trimmedPart, out int index) &&
                         index >= 1 && index <= availableChains.Count)
                {
                    selectedChains.Add(availableChains[index - 1].Name);
                }
            }

            return selectedChains.Distinct().ToList();
        }

        /// <summary>
        /// הצגת תוצאות הורדה מעודכנת עם פירוט מלא
        /// </summary>
        /// <param name="result">תוצאות ההורדה</param>
        /// <param name="title">כותרת אופציונלית</param>
        private static void DisplayDownloadResults(CoordinatorResult result, string? title = null)
        {
            Console.WriteLine();
            Console.WriteLine(title ?? "📊 תוצאות הורדה:");
            Console.WriteLine(new string('=', (title ?? "תוצאות הורדה").Length + 4));
            Console.WriteLine($"   ⏱️ משך זמן: {result.Duration:hh\\:mm\\:ss}");
            Console.WriteLine($"   ✅ רשתות שהצליחו: {result.SuccessfulDownloads}/{result.TotalRequests}");
            Console.WriteLine($"   📄 סה\"כ קבצים: {result.TotalFilesDownloaded:N0}");
            Console.WriteLine($"   💾 גודל כולל: {FormatFileSize(result.TotalSizeDownloaded)}");
            Console.WriteLine($"   🏃 מהירות ממוצעת: {FormatFileSize((long)(result.TotalDownloadedSize / Math.Max(result.Duration.TotalSeconds, 1)))}/s");

            if (!result.IsSuccess && !string.IsNullOrEmpty(result.GeneralError))
            {
                Console.WriteLine($"   ❌ שגיאה כללית: {result.GeneralError}");
            }

            // הצגת פירוט לפי רשת
            if (result.Results.Any())
            {
                Console.WriteLine("\n🏪 פירוט לפי רשת:");

                var successfulChains = result.Results.Where(r => r.IsSuccess).OrderBy(r => r.ChainName);
                var failedChains = result.Results.Where(r => !r.IsSuccess).OrderBy(r => r.ChainName);

                // רשתות שהצליחו
                foreach (var chainResult in successfulChains)
                {
                    var duration = chainResult.Duration.TotalSeconds;
                    var speed = chainResult.TotalDownloadedSize > 0
                        ? FormatFileSize((long)(chainResult.TotalDownloadedSize / Math.Max(duration, 1))) + "/s"
                        : "";

                    Console.WriteLine($"   ✅ {chainResult.ChainName}: {chainResult.TotalDownloadedFiles} קבצים " +
                                    $"({FormatFileSize(chainResult.TotalDownloadedSize)}) {speed}");

                    // פירוט סוגי קבצים
                    if (chainResult.TotalDownloadedFiles > 0)
                    {
                        Console.WriteLine($"      📋 StoresFull: {chainResult.StoresFullCount}, " +
                                        $"PriceFull: {chainResult.PriceFullCount}, " +
                                        $"PromoFull: {chainResult.PromoFullCount}");
                    }
                }

                // רשתות שנכשלו
                foreach (var chainResult in failedChains)
                {
                    Console.WriteLine($"   ❌ {chainResult.ChainName}: נכשל");
                    if (!string.IsNullOrEmpty(chainResult.ErrorMessage))
                    {
                        Console.WriteLine($"      💬 {chainResult.ErrorMessage}");
                    }
                }

                // סטטיסטיקות נוספות
                if (result.TotalFilesDownloaded > 0)
                {
                    var avgFilesPerChain = (double)result.TotalFilesDownloaded / result.SuccessfulDownloads;
                    var avgSizePerFile = result.TotalDownloadedSize / result.TotalFilesDownloaded;

                    Console.WriteLine($"\n📈 סטטיסטיקות:");
                    Console.WriteLine($"   📊 ממוצע קבצים לרשת: {avgFilesPerChain:F1}");
                    Console.WriteLine($"   📦 ממוצע גודל קובץ: {FormatFileSize(avgSizePerFile)}");
                    Console.WriteLine($"   🎯 אחוז הצלחה: {result.SuccessfulDownloads * 100.0 / result.TotalRequests:F1}%");
                }
            }
        }

        /// <summary>
        /// פורמט גודל קובץ מותאם לעברית
        /// </summary>
        private static string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} בייט";
            if (bytes < 1024 * 1024) return $"{bytes / 1024:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024 * 1024):F1} MB";
            return $"{bytes / (1024 * 1024 * 1024):F1} GB";
        }

        /// <summary>
        /// המתנה לקלט מהמשתמש
        /// </summary>
        private static void PauseForUser()
        {
            Console.WriteLine("\n⏸️ לחץ כל מקש להמשך...");
            Console.ReadKey(true);
        }

        #endregion

        #region Event Handlers - מטפלי אירועים

        private static void OnDownloadProgress(object sender, DownloadProgressEventArgs e)
        {
            Console.WriteLine($"📥 {e.ChainName}: {e.ProgressPercentage:F1}% - {e.CurrentOperation}");
        }

        private static void OnTaskCompleted(object sender, TaskCompletedEventArgs e)
        {
            var status = e.IsSuccess ? "✅" : "❌";
            Console.WriteLine($"{status} {e.ChainName} הושלם ({e.Duration:mm\\:ss})");
        }

        private static void OnScheduledRunStarted(object sender, ScheduledRunEventArgs e)
        {
            Console.WriteLine($"⏰ הרצה מתוזמנת התחילה: {e.StartTime:HH:mm:ss}");
        }

        private static void OnScheduledRunCompleted(object sender, ScheduledRunEventArgs e)
        {
            var status = e.IsSuccess ? "✅" : "❌";
            Console.WriteLine($"{status} הרצה מתוזמנת הושלמה: {e.Duration:hh\\:mm\\:ss}");
        }

        #endregion

        #region Additional Methods - שיטות נוספות

        // כאן יבואו השיטות הנוספות עבור:
        // - CheckChainsAvailabilityDetailedAsyncls
        // - DisplaySystemStatusAsync
        // - CheckAvailableFilesAsync
        // - ManageSettingsAsync
        // - CleanupOldFilesAsync
        // - HandleCommandLineArgumentsAsync
        // - UploadResultsToAzureAsync

        #endregion
    }
}