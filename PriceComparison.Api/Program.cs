using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PriceComparison.Download.Models;
using PriceComparison.Download.Services;
using PriceComparison.Download.Extensions;
using System.Text;
using System.Text.Json;

namespace PriceComparison.Console
{
    /// <summary>
    /// תוכנית משולבת - שומרת על הארכיטקטורה הישנה 
    /// אבל משתמשת באלגוריתם הפשוט והעובד של King Store
    /// </summary>
    class Program
    {
        private static IBinaProjectsDownloadService? _downloadService;
        private static ILogger<Program>? _logger;

        // 🆕 הוספת HttpClient פשוט לצד המערכת המתקדמת
        private static readonly HttpClient simpleHttpClient = new HttpClient();

        // מעקב שגיאות
        private static readonly string LogFileName = $"BinaProjects_Log_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
        private static readonly string ErrorFileName = $"BinaProjects_Errors_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
        private static readonly List<string> AllLogs = new();
        private static readonly List<string> ErrorSummary = new();

        static async Task Main(string[] args)
        {
            System.Console.OutputEncoding = System.Text.Encoding.UTF8;

            try
            {
                // הגדרת HttpClient פשוט
                SetupSimpleHttpClient();

                // בדיקה אם יש ארגומנט בקו הפקודה
                if (args.Length > 0)
                {
                    await HandleCommandLineArgs(args);
                    return;
                }

                // אם אין ארגומנטים - הצגת תפריט
                await ShowMainMenu();
            }
            catch (Exception ex)
            {
                var errorMsg = $"❌ שגיאה קריטית: {ex.Message}\n📋 Stack Trace: {ex.StackTrace}";
                LogError("CRITICAL_ERROR", errorMsg);
                System.Console.WriteLine(errorMsg);
            }
            finally
            {
                if (AllLogs.Any())
                {
                    await SaveAllLogsToFile();
                    ShowErrorSummary();
                }
            }

            System.Console.WriteLine("\n🔍 לחץ מקש כלשהו לסיום...");
            System.Console.ReadKey();
        }

        private static void SetupSimpleHttpClient()
        {
            simpleHttpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            simpleHttpClient.DefaultRequestHeaders.Add("Accept", "*/*");
            simpleHttpClient.DefaultRequestHeaders.Add("Accept-Language", "he-IL,he;q=0.8");
            LogMessage("🌐 Simple HTTP Client הוכן");
        }

        private static async Task HandleCommandLineArgs(string[] args)
        {
            var mode = args[0].ToLower();
            LogMessage($"🚀 הפעלה במצב: {mode}");

            await InitializeServices();

            switch (mode)
            {
                case "auto":
                case "automatic":
                    await RunAutomaticModeKingStoreMethod();  // 🆕 השיטה החדשה
                    break;

                case "debug":
                case "diagnose":
                    await RunDiagnosticMode();
                    break;

                case "quick":
                case "fast":
                    await RunQuickModeKingStoreMethod();  // 🆕 השיטה החדשה
                    break;

                case "test":
                    await RunTestMode();
                    break;

                default:
                    System.Console.WriteLine($"❌ מצב לא מוכר: {mode}");
                    System.Console.WriteLine("💡 מצבים זמינים: auto, debug, quick, test");
                    break;
            }
        }

        private static async Task ShowMainMenu()
        {
            while (true)
            {
                System.Console.Clear();
                PrintMainHeader();

                System.Console.WriteLine("🎯 בחר מצב הפעלה:");
                System.Console.WriteLine("==================");
                System.Console.WriteLine("1️⃣  🤖 הורדה אוטומטית (שיטת King Store) ⭐");
                System.Console.WriteLine("2️⃣  🔍 אבחון שגיאות מפורט");
                System.Console.WriteLine("3️⃣  ⚡ הורדה מהירה (שיטת King Store) ⭐");
                System.Console.WriteLine("4️⃣  🧪 בדיקת מערכת");
                System.Console.WriteLine("5️⃣  📋 הצגת רשתות זמינות");
                System.Console.WriteLine("6️⃣  📁 בדיקת קבצים שהורדו");
                System.Console.WriteLine("7️⃣  ⚙️  הגדרות מתקדמות");
                System.Console.WriteLine("0️⃣  🚪 יציאה");
                System.Console.WriteLine("==================");
                System.Console.WriteLine("⭐ = שיטה חדשה ומשופרת");
                System.Console.Write("👉 הזן מספר (או 'help' לעזרה): ");

                var choice = System.Console.ReadLine()?.ToLower();

                if (choice == "help")
                {
                    ShowHelp();
                    continue;
                }

                await ProcessMenuChoice(choice);

                if (choice == "0")
                    break;

                System.Console.WriteLine("\n🔍 לחץ מקש כלשהו להמשך...");
                System.Console.ReadKey();
            }
        }

        private static void PrintMainHeader()
        {
            System.Console.WriteLine("🌐 BinaProjects Universal Downloader - Hybrid Version");
            System.Console.WriteLine("====================================================");
            System.Console.WriteLine($"📅 {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
            System.Console.WriteLine($"📄 לוגים יישמרו ב: {LogFileName}");
            System.Console.WriteLine("🔧 משלב: ארכיטקטורה מתקדמת + אלגוריתם King Store");
            System.Console.WriteLine();
        }

        private static async Task ProcessMenuChoice(string? choice)
        {
            await InitializeServices();

            switch (choice)
            {
                case "1":
                    await RunAutomaticModeKingStoreMethod();  // 🆕 השיטה החדשה
                    break;
                case "2":
                    await RunDiagnosticMode();
                    break;
                case "3":
                    await RunQuickModeKingStoreMethod();  // 🆕 השיטה החדשה
                    break;
                case "4":
                    await RunTestMode();
                    break;
                case "5":
                    await ShowNetworksList();
                    break;
                case "6":
                    await CheckDownloadedFiles();
                    break;
                case "7":
                    await ShowAdvancedSettings();
                    break;
                case "0":
                    LogMessage("👋 יציאה מהתוכנית");
                    System.Console.WriteLine("👋 להתראות!");
                    break;
                default:
                    System.Console.WriteLine("❌ בחירה לא חוקית. נסה שוב.");
                    break;
            }
        }

        // 🆕 מצב 1: הורדה אוטומטית בשיטת King Store
        private static async Task RunAutomaticModeKingStoreMethod()
        {
            LogMessage("🤖 מתחיל מצב הורדה אוטומטית - שיטת King Store");
            System.Console.WriteLine("\n🤖 מצב הורדה אוטומטית - שיטת King Store ⭐");
            System.Console.WriteLine("===============================================");
            System.Console.WriteLine("📥 מוריד את הקבצים העדכניים ביותר מכל הרשתות...");

            // 🆕 השיטה החדשה - תאריך קבוע כמו King Store
            var todayDate = DateTime.Now.ToString("dd/MM/yyyy");
            System.Console.WriteLine($"📅 מחפש קבצים לתאריך: {todayDate}\n");

            try
            {
                var startTime = DateTime.Now;
                CreateDownloadFolders();

                // קבלת רשתות פעילות
                var networks = await _downloadService!.GetActiveNetworksAsync();
                LogMessage($"נמצאו {networks.Count} רשתות פעילות");
                System.Console.WriteLine($"🏪 נמצאו {networks.Count} רשתות פעילות\n");

                var allResults = new Dictionary<string, List<SimpleDownloadResult>>();

                // עבור כל רשת - נשתמש באותה שיטה כמו King Store
                foreach (var network in networks)
                {
                    System.Console.WriteLine($"🔄 מעבד רשת: {network.Name}");
                    var results = await ProcessNetworkKingStoreMethod(network, todayDate);
                    allResults[network.Id] = results;
                    System.Console.WriteLine();
                }

                var duration = DateTime.Now - startTime;
                await ShowKingStoreResults(allResults, networks, duration);
            }
            catch (Exception ex)
            {
                LogError("AUTO_DOWNLOAD_KS", $"שגיאה בהורדה אוטומטית: {ex.Message}\n{ex.StackTrace}");
                System.Console.WriteLine($"❌ שגיאה בהורדה אוטומטית: {ex.Message}");
            }
        }

        // 🆕 מצב 3: הורדה מהירה בשיטת King Store
        private static async Task RunQuickModeKingStoreMethod()
        {
            LogMessage("⚡ מתחיל מצב הורדה מהירה - שיטת King Store");
            System.Console.WriteLine("\n⚡ מצב הורדה מהירה - שיטת King Store ⭐");
            System.Console.WriteLine("============================================");

            try
            {
                CreateDownloadFolders();
                var networks = await _downloadService!.GetActiveNetworksAsync();

                System.Console.WriteLine("🏪 בחר רשת להורדה מהירה:");
                for (int i = 0; i < Math.Min(networks.Count, 5); i++)
                {
                    System.Console.WriteLine($"{i + 1}. {networks[i].Name}");
                }

                System.Console.Write("\n👉 הזן מספר (או Enter לרשת ראשונה): ");
                var choice = System.Console.ReadLine();

                int selectedIndex = 0;
                if (!string.IsNullOrEmpty(choice) && int.TryParse(choice, out int parsed))
                {
                    selectedIndex = Math.Max(0, Math.Min(parsed - 1, networks.Count - 1));
                }

                var selectedNetwork = networks[selectedIndex];
                var todayDate = DateTime.Now.ToString("dd/MM/yyyy");

                System.Console.WriteLine($"\n📥 מוריד מ: {selectedNetwork.Name}...");
                System.Console.WriteLine($"📅 תאריך: {todayDate}");

                var results = await ProcessNetworkKingStoreMethod(selectedNetwork, todayDate);

                var successCount = results.Count(r => r.Success);
                if (successCount > 0)
                {
                    System.Console.WriteLine($"\n✅ הורדה הצליחה! {successCount} קבצים הורדו:");
                    foreach (var success in results.Where(r => r.Success))
                    {
                        System.Console.WriteLine($"   📄 {success.FileName} ({success.FileSize:N0} bytes)");
                    }
                }
                else
                {
                    System.Console.WriteLine($"\n❌ הורדה נכשלה:");
                    foreach (var failure in results.Where(r => !r.Success))
                    {
                        System.Console.WriteLine($"   ❌ {failure.ErrorMessage}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogError("QUICK_MODE_KS", $"שגיאה בהורדה מהירה: {ex.Message}\n{ex.StackTrace}");
                System.Console.WriteLine($"❌ שגיאה: {ex.Message}");
            }
        }

        // 🆕 עיבוד רשת לפי השיטה של King Store
        private static async Task<List<SimpleDownloadResult>> ProcessNetworkKingStoreMethod(BinaProjectsNetworkInfo network, string todayDate)
        {
            var results = new List<SimpleDownloadResult>();

            try
            {
                LogMessage($"מתחיל עיבוד רשת {network.Name} לתאריך {todayDate}");

                // שלב 1: הורדת StoresFull העדכני ביותר (כמו בקוד המקורי)
                System.Console.WriteLine("🏢 שלב 1: הורדת קובץ סניפים (StoresFull)...");
                var storesResult = await DownloadLatestStoresFullKingStoreMethod(network, todayDate);
                if (storesResult != null) results.Add(storesResult);

                // שלב 2: קבלת רשימת כל הסניפים הזמינים
                System.Console.WriteLine("📍 שלב 2: זיהוי כל הסניפים הזמינים...");
                var allStores = await GetAllAvailableStoresKingStoreMethod(network, todayDate);

                // שלב 3: הורדת PriceFull העדכני ביותר לכל סניף
                System.Console.WriteLine("💰 שלב 3: הורדת PriceFull לכל סניף...");
                var priceResults = await DownloadLatestPriceFullKingStoreMethod(network, todayDate, allStores);
                results.AddRange(priceResults);

                // שלב 4: הורדת PromoFull העדכני ביותר לכל סניף
                System.Console.WriteLine("🎁 שלב 4: הורדת PromoFull לכל סניף...");
                var promoResults = await DownloadLatestPromoFullKingStoreMethod(network, todayDate, allStores);
                results.AddRange(promoResults);

                LogMessage($"הושלם עיבוד רשת {network.Name}: {results.Count(r => r.Success)}/{results.Count} הצליחו");
                System.Console.WriteLine($"✅ {network.Name} הושלם: {results.Count(r => r.Success)}/{results.Count} קבצים");
            }
            catch (Exception ex)
            {
                LogError($"PROCESS_NETWORK_{network.Id}", $"שגיאה בעיבוד רשת {network.Name}: {ex.Message}");
                System.Console.WriteLine($"❌ שגיאה ב-{network.Name}: {ex.Message}");
                results.Add(new SimpleDownloadResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    FileName = $"שגיאה ב-{network.Name}"
                });
            }

            return results;
        }

        // 🆕 הורדת StoresFull לפי השיטה של King Store
        private static async Task<SimpleDownloadResult?> DownloadLatestStoresFullKingStoreMethod(BinaProjectsNetworkInfo network, string date)
        {
            System.Console.WriteLine("🔍 מחפש קבצי StoresFull...");

            try
            {
                // חיפוש ספציפי לקבצי StoresFull בכל סוגי הקבצים (0-5) - כמו בקוד המקורי
                var allStoresFiles = await GetFilesByPatternKingStoreMethod(network, date, "", new[] { "store" });

                // סינון רק לקבצי StoresFull (לא Store רגיל) - כמו בקוד המקורי
                var storesFullFiles = allStoresFiles.Where(f =>
                    f.TypeFile.ToLower().Contains("storesfull") ||
                    f.TypeFile.ToLower().Contains("stores_full") ||
                    f.TypeFile.ToLower().Contains("storesmichsan") ||
                    f.TypeFile.ToLower().Contains("מחסנים מלא") ||
                    (f.TypeFile.ToLower().Contains("store") && f.TypeFile.ToLower().Contains("full"))
                ).ToList();

                System.Console.WriteLine($"🏪 נמצאו {storesFullFiles.Count} קבצי StoresFull");

                if (!storesFullFiles.Any())
                {
                    LogMessage($"לא נמצאו קבצי StoresFull עבור {network.Name}");
                    System.Console.WriteLine("⚠️ לא נמצאו קבצי StoresFull");
                    return new SimpleDownloadResult
                    {
                        Success = false,
                        ErrorMessage = "לא נמצאו קבצי StoresFull",
                        FileName = "StoresFull"
                    };
                }

                // מיון לפי תאריך ושעה - העדכני ביותר (הגבוה ביותר) - כמו בקוד המקורי
                var latestStoresFile = storesFullFiles
                    .OrderByDescending(f => ParseDateTime(f.DateFile))
                    .FirstOrDefault();

                if (latestStoresFile != null)
                {
                    System.Console.WriteLine($"📥 נבחר הקובץ העדכני ביותר: {latestStoresFile.TypeFile} ({latestStoresFile.DateFile})");
                    return await DownloadAndExtractFileKingStoreMethod(latestStoresFile, network, "StoresFull");
                }

                return new SimpleDownloadResult
                {
                    Success = false,
                    ErrorMessage = "לא נמצא קובץ StoresFull מתאים",
                    FileName = "StoresFull"
                };
            }
            catch (Exception ex)
            {
                LogError($"STORES_FULL_{network.Id}", $"שגיאה בהורדת StoresFull: {ex.Message}");
                return new SimpleDownloadResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    FileName = "StoresFull"
                };
            }
        }

        // 🆕 קבלת כל הסניפים הזמינים - שיטת King Store
        private static async Task<List<string>> GetAllAvailableStoresKingStoreMethod(BinaProjectsNetworkInfo network, string date)
        {
            var stores = new HashSet<string>();

            try
            {
                // נקבל קבצים מכל הסוגים ונחלץ מהם את שמות הסניפים - כמו בקוד המקורי
                var allFiles = await GetAllFilesForDateKingStoreMethod(network, date);

                foreach (var file in allFiles)
                {
                    if (!string.IsNullOrEmpty(file.Store) && file.Store.Trim() != "")
                    {
                        stores.Add(file.Store.Trim());
                    }
                }

                var storesList = stores.OrderBy(s => s).ToList();
                System.Console.WriteLine($"🏪 נמצאו {storesList.Count} סניפים");

                foreach (var store in storesList.Take(5))
                {
                    System.Console.WriteLine($"   🏪 {store}");
                }

                if (storesList.Count > 5)
                    System.Console.WriteLine($"   ... ועוד {storesList.Count - 5} סניפים");

                LogMessage($"נמצאו {storesList.Count} סניפים עבור {network.Name}");
                return storesList;
            }
            catch (Exception ex)
            {
                LogError($"GET_STORES_{network.Id}", $"שגיאה בקבלת סניפים: {ex.Message}");
                System.Console.WriteLine($"❌ שגיאה בקבלת סניפים: {ex.Message}");
                return new List<string>();
            }
        }

        // 🆕 הורדת PriceFull - שיטת King Store
        private static async Task<List<SimpleDownloadResult>> DownloadLatestPriceFullKingStoreMethod(BinaProjectsNetworkInfo network, string date, List<string> stores)
        {
            var results = new List<SimpleDownloadResult>();
            System.Console.WriteLine("🔍 מחפש קבצי PriceFull בלבד...");

            try
            {
                var allPriceFiles = await GetFilesByPatternKingStoreMethod(network, date, "", new[] { "price", "מחיר" });

                // סינון מדויק יותר - רק PriceFull ולא Price רגיל - כמו בקוד המקורי
                var priceFullFiles = allPriceFiles.Where(f =>
                    f.TypeFile.ToLower().Contains("pricefull") ||
                    f.TypeFile.ToLower().Contains("price_full") ||
                    (f.TypeFile.ToLower().Contains("price") && f.TypeFile.ToLower().Contains("full")) ||
                    f.TypeFile.ToLower().Contains("מחירים מלא")
                ).ToList();

                System.Console.WriteLine($"💰 נמצאו {priceFullFiles.Count} קבצי PriceFull");

                if (!priceFullFiles.Any())
                {
                    System.Console.WriteLine("⚠️ לא נמצאו קבצי PriceFull");
                    results.Add(new SimpleDownloadResult
                    {
                        Success = false,
                        ErrorMessage = "לא נמצאו קבצי PriceFull",
                        FileName = "PriceFull"
                    });
                    return results;
                }

                int downloadedCount = 0;
                foreach (var store in stores.Take(3)) // נגביל ל-3 ראשונים
                {
                    var storeFiles = priceFullFiles
                        .Where(f => f.Store.Contains(store) || store.Contains(f.Store))
                        .OrderByDescending(f => ParseDateTime(f.DateFile))
                        .FirstOrDefault();

                    if (storeFiles != null)
                    {
                        var result = await DownloadAndExtractFileKingStoreMethod(storeFiles, network, "PriceFull");
                        if (result != null) results.Add(result);
                        downloadedCount++;
                        await Task.Delay(500); // השהיה כמו בקוד המקורי
                    }
                }

                System.Console.WriteLine($"✅ הורדו {downloadedCount} קבצי PriceFull");
            }
            catch (Exception ex)
            {
                LogError($"PRICE_FULL_{network.Id}", $"שגיאה בהורדת PriceFull: {ex.Message}");
                results.Add(new SimpleDownloadResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    FileName = "PriceFull"
                });
            }

            return results;
        }

        // 🆕 הורדת PromoFull - שיטת King Store
        private static async Task<List<SimpleDownloadResult>> DownloadLatestPromoFullKingStoreMethod(BinaProjectsNetworkInfo network, string date, List<string> stores)
        {
            var results = new List<SimpleDownloadResult>();
            System.Console.WriteLine("🔍 מחפש קבצי PromoFull בלבד...");

            try
            {
                var allPromoFiles = await GetFilesByPatternKingStoreMethod(network, date, "", new[] { "promo", "מבצע" });

                // סינון מדויק יותר - רק PromoFull ולא Promo רגיל - כמו בקוד המקורי
                var promoFullFiles = allPromoFiles.Where(f =>
                    f.TypeFile.ToLower().Contains("promofull") ||
                    f.TypeFile.ToLower().Contains("promo_full") ||
                    (f.TypeFile.ToLower().Contains("promo") && f.TypeFile.ToLower().Contains("full")) ||
                    f.TypeFile.ToLower().Contains("מבצעים מלא")
                ).ToList();

                System.Console.WriteLine($"🎁 נמצאו {promoFullFiles.Count} קבצי PromoFull");

                if (!promoFullFiles.Any())
                {
                    System.Console.WriteLine("⚠️ לא נמצאו קבצי PromoFull");
                    results.Add(new SimpleDownloadResult
                    {
                        Success = false,
                        ErrorMessage = "לא נמצאו קבצי PromoFull",
                        FileName = "PromoFull"
                    });
                    return results;
                }

                int downloadedCount = 0;
                foreach (var store in stores.Take(3)) // נגביל ל-3 ראשונים
                {
                    var storeFiles = promoFullFiles
                        .Where(f => f.Store.Contains(store) || store.Contains(f.Store))
                        .OrderByDescending(f => ParseDateTime(f.DateFile))
                        .FirstOrDefault();

                    if (storeFiles != null)
                    {
                        var result = await DownloadAndExtractFileKingStoreMethod(storeFiles, network, "PromoFull");
                        if (result != null) results.Add(result);
                        downloadedCount++;
                        await Task.Delay(500); // השהיה כמו בקוד המקורי
                    }
                }

                System.Console.WriteLine($"✅ הורדו {downloadedCount} קבצי PromoFull");
            }
            catch (Exception ex)
            {
                LogError($"PROMO_FULL_{network.Id}", $"שגיאה בהורדת PromoFull: {ex.Message}");
                results.Add(new SimpleDownloadResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    FileName = "PromoFull"
                });
            }

            return results;
        }

        // 🆕 הפונקציות העזר - כמו בקוד המקורי של King Store

        private static async Task<List<BinaProjectsFileInfo>> GetFilesByPatternKingStoreMethod(BinaProjectsNetworkInfo network, string date, string store, string[] searchTerms)
        {
            var allFiles = new List<BinaProjectsFileInfo>();

            // נחפש בכל סוגי הקבצים (0-5) - כמו בקוד המקורי
            for (int fileType = 0; fileType <= 5; fileType++)
            {
                var files = await GetFilesFromServerKingStoreMethod(network, date, store, fileType.ToString());

                if (searchTerms != null && searchTerms.Any())
                {
                    files = files.Where(f =>
                        searchTerms.Any(term =>
                            f.TypeFile.ToLower().Contains(term.ToLower()) ||
                            f.FileName.ToLower().Contains(term.ToLower())
                        )
                    ).ToList();
                }

                allFiles.AddRange(files);
                await Task.Delay(100); // זמן המתנה קצר בין בקשות - כמו בקוד המקורי
            }

            return allFiles.Distinct().ToList();
        }

        private static async Task<List<BinaProjectsFileInfo>> GetAllFilesForDateKingStoreMethod(BinaProjectsNetworkInfo network, string date)
        {
            return await GetFilesByPatternKingStoreMethod(network, date, "", null); // בלי סינון - כל הקבצים
        }

        private static async Task<List<BinaProjectsFileInfo>> GetFilesFromServerKingStoreMethod(BinaProjectsNetworkInfo network, string date, string store, string fileType)
        {
            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("WStore", store),
                new KeyValuePair<string, string>("WDate", date),
                new KeyValuePair<string, string>("WFileType", fileType)
            });

            try
            {
                var response = await simpleHttpClient.PostAsync(network.ApiEndpoint, formContent);

                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync();
                    return ParseFilesListKingStoreMethod(jsonContent, network.Id);
                }
            }
            catch (Exception ex)
            {
                LogError($"GET_FILES_{network.Id}_{fileType}", $"שגיאה בקבלת קבצים: {ex.Message}");
            }

            return new List<BinaProjectsFileInfo>();
        }

        private static async Task<SimpleDownloadResult> DownloadAndExtractFileKingStoreMethod(BinaProjectsFileInfo fileInfo, BinaProjectsNetworkInfo network, string targetFolder)
        {
            try
            {
                // שלב 1: קבלת קישור ההורדה - כמו בקוד המקורי
                var metaResponse = await simpleHttpClient.PostAsync(
                    $"{network.BaseUrl}/Download.aspx?FileNm={fileInfo.FileName}",
                    new StringContent(""));

                if (metaResponse.IsSuccessStatusCode)
                {
                    var metaContent = await metaResponse.Content.ReadAsStringAsync();
                    var downloadUrl = ExtractDownloadUrlKingStoreMethod(metaContent);

                    if (!string.IsNullOrEmpty(downloadUrl))
                    {
                        // שלב 2: הורדת קובץ ה-ZIP - כמו בקוד המקורי
                        var fileResponse = await simpleHttpClient.GetAsync(downloadUrl);

                        if (fileResponse.IsSuccessStatusCode)
                        {
                            var zipBytes = await fileResponse.Content.ReadAsByteArrayAsync();

                            // שלב 3: שמירת ה-ZIP
                            string zipFileName = $"{fileInfo.Store}_{fileInfo.TypeFile}_{DateTime.Now:yyyyMMdd_HHmmss}.zip";
                            zipFileName = string.Join("_", zipFileName.Split(Path.GetInvalidFileNameChars()));
                            string zipPath = Path.Combine("DownloadedFiles", "ZIP_Files", zipFileName);
                            await File.WriteAllBytesAsync(zipPath, zipBytes);

                            // שלב 4: חילוץ ה-XML
                            await ExtractXmlFromZipKingStoreMethod(zipBytes, fileInfo, targetFolder);

                            LogMessage($"הושלמה הורדה: {fileInfo.TypeFile} ({zipBytes.Length} bytes)");

                            return new SimpleDownloadResult
                            {
                                Success = true,
                                FileName = fileInfo.TypeFile,
                                FileSize = zipBytes.Length
                            };
                        }
                    }
                }

                return new SimpleDownloadResult
                {
                    Success = false,
                    ErrorMessage = "לא הצליח להוריד",
                    FileName = fileInfo.TypeFile
                };
            }
            catch (Exception ex)
            {
                LogError($"DOWNLOAD_{network.Id}", $"שגיאה בהורדה: {ex.Message}");
                return new SimpleDownloadResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    FileName = fileInfo.TypeFile
                };
            }
        }

        // 🆕 שאר הפונקציות העזר...

        private static List<BinaProjectsFileInfo> ParseFilesListKingStoreMethod(string jsonContent, string networkId)
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(jsonContent);
                var files = new List<BinaProjectsFileInfo>();

                foreach (JsonElement element in doc.RootElement.EnumerateArray())
                {
                    if (element.TryGetProperty("SFile", out JsonElement fileNmProp))
                    {
                        var fileInfo = new BinaProjectsFileInfo
                        {
                            NetworkId = networkId,
                            FileName = fileNmProp.GetString() ?? "",
                            Store = element.TryGetProperty("SStore", out var store) ? store.GetString() ?? "" : "",
                            TypeFile = element.TryGetProperty("SType", out var type) ? type.GetString() ?? "" : "",
                            DateFile = element.TryGetProperty("SDate", out var date) ? date.GetString() ?? "" : "",
                            DownloadUrl = element.TryGetProperty("SPath", out var path) ? path.GetString() ?? "" : ""
                        };

                        files.Add(fileInfo);
                    }
                }

                return files;
            }
            catch (JsonException)
            {
                return new List<BinaProjectsFileInfo>();
            }
        }

        private static string ExtractDownloadUrlKingStoreMethod(string metaContent)
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(metaContent);
                foreach (JsonElement element in doc.RootElement.EnumerateArray())
                {
                    if (element.TryGetProperty("SPath", out JsonElement pathProp))
                    {
                        return pathProp.GetString() ?? "";
                    }
                }
            }
            catch (JsonException)
            {
                if (metaContent.Contains("http"))
                {
                    var lines = metaContent.Split('\n');
                    foreach (var line in lines)
                    {
                        if (line.Contains("http") && (line.Contains(".zip") || line.Contains(".xml")))
                        {
                            return line.Trim().Trim('"');
                        }
                    }
                }
            }

            return "";
        }

        private static async Task ExtractXmlFromZipKingStoreMethod(byte[] zipBytes, BinaProjectsFileInfo fileInfo, string targetFolder)
        {
            try
            {
                using var zipStream = new MemoryStream(zipBytes);
                using var archive = new System.IO.Compression.ZipArchive(zipStream, System.IO.Compression.ZipArchiveMode.Read);

                foreach (var entry in archive.Entries)
                {
                    if (entry.Name.ToLower().EndsWith(".xml"))
                    {
                        using var entryStream = entry.Open();
                        using var reader = new StreamReader(entryStream);
                        var xmlContent = await reader.ReadToEndAsync();

                        var xmlFileName = $"{fileInfo.Store}_{fileInfo.TypeFile}_{DateTime.Now:yyyyMMdd_HHmmss}.xml";
                        xmlFileName = string.Join("_", xmlFileName.Split(Path.GetInvalidFileNameChars()));

                        var xmlPath = Path.Combine("DownloadedFiles", targetFolder, xmlFileName);
                        await File.WriteAllTextAsync(xmlPath, xmlContent);

                        LogMessage($"חולץ XML: {xmlFileName} ({xmlContent.Length} תווים)");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                LogError("EXTRACT_XML", $"שגיאה בחילוץ XML: {ex.Message}");
            }
        }

        private static DateTime ParseDateTime(string dateStr)
        {
            if (string.IsNullOrEmpty(dateStr))
                return DateTime.MinValue;

            var formats = new[]
            {
                "dd/MM/yyyy HH:mm:ss",
                "dd/MM/yyyy HH:mm",
                "yyyy-MM-dd HH:mm:ss",
                "dd/MM/yyyy",
                "MM/dd/yyyy HH:mm:ss",
                "MM/dd/yyyy HH:mm"
            };

            foreach (var format in formats)
            {
                if (DateTime.TryParseExact(dateStr, format, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime result))
                {
                    return result;
                }
            }

            if (DateTime.TryParse(dateStr, out DateTime fallbackResult))
            {
                return fallbackResult;
            }

            return DateTime.MinValue;
        }

        private static void CreateDownloadFolders()
        {
            var DOWNLOAD_FOLDER = "DownloadedFiles";
            if (!Directory.Exists(DOWNLOAD_FOLDER))
                Directory.CreateDirectory(DOWNLOAD_FOLDER);

            var subFolders = new[] { "StoresFull", "PriceFull", "PromoFull", "ZIP_Files", "Raw_Data" };
            foreach (var folder in subFolders)
            {
                var path = Path.Combine(DOWNLOAD_FOLDER, folder);
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
            }

            LogMessage($"תיקיות הורדה מוכנות: {Path.GetFullPath(DOWNLOAD_FOLDER)}");
        }

        private static async Task ShowKingStoreResults(Dictionary<string, List<SimpleDownloadResult>> allResults, List<BinaProjectsNetworkInfo> networks, TimeSpan duration)
        {
            var totalSuccess = allResults.Values.SelectMany(r => r).Count(r => r.Success);
            var totalFiles = allResults.Values.SelectMany(r => r).Count();
            var totalSize = allResults.Values.SelectMany(r => r).Sum(r => r.FileSize);

            LogMessage($"הורדה הושלמה: {totalSuccess}/{totalFiles} קבצים, {totalSize / 1024 / 1024:N2} MB, {duration.TotalMinutes:N1} דקות");

            System.Console.WriteLine("\n📊 תוצאות הורדה - שיטת King Store:");
            System.Console.WriteLine("=====================================");
            System.Console.WriteLine($"✅ הצליחו: {totalSuccess}/{totalFiles} קבצים");
            System.Console.WriteLine($"💾 גודל כולל: {totalSize / 1024 / 1024:N2} MB");
            System.Console.WriteLine($"⏱️ זמן ביצוע: {duration.TotalMinutes:N1} דקות");

            foreach (var networkResults in allResults)
            {
                var networkName = networks.FirstOrDefault(n => n.Id == networkResults.Key)?.Name ?? networkResults.Key;
                var successCount = networkResults.Value.Count(r => r.Success);
                var networkTotal = networkResults.Value.Count;

                System.Console.WriteLine($"\n🏪 {networkName}: {successCount}/{networkTotal}");

                foreach (var success in networkResults.Value.Where(r => r.Success))
                {
                    System.Console.WriteLine($"   ✅ {success.FileName} ({success.FileSize / 1024:N0} KB)");
                }

                foreach (var failure in networkResults.Value.Where(r => !r.Success))
                {
                    System.Console.WriteLine($"   ❌ {failure.FileName}: {failure.ErrorMessage}");
                }
            }

            if (totalSuccess < totalFiles)
            {
                System.Console.WriteLine($"\n📄 פרטים מלאים בקובץ: {ErrorFileName}");
            }
        }

        // שאר הפונקציות הקיימות נשארות כמו שהן...

        private static async Task InitializeServices()
        {
            if (_downloadService != null) return;

            LogMessage("🔧 מאתחל שירותים...");

            var host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
                    services.AddBinaProjectsServices(context.Configuration);
                })
                .Build();

            _downloadService = host.Services.GetRequiredService<IBinaProjectsDownloadService>();
            _logger = host.Services.GetRequiredService<ILogger<Program>>();

            LogMessage("✅ שירותים אותחלו בהצלחה");
        }

        // [כל שאר הפונקציות הישנות נשארות כמו שהן - רק מצב 1 ו-3 הוחלפו]

        private static async Task RunDiagnosticMode()
        {
            // הפונקציה הישנה נשארת כמו שהיא...
            LogMessage("🔍 מתחיל מצב אבחון שגיאות - גרסה ישנה");
            System.Console.WriteLine("\n🔍 מצב אבחון שגיאות");
            System.Console.WriteLine("===================");
            System.Console.WriteLine("🔧 משתמש בשיטה הישנה לאבחון...\n");

            try
            {
                var networks = await _downloadService!.GetActiveNetworksAsync();
                LogMessage($"נמצאו {networks.Count} רשתות פעילות לאבחון");
                System.Console.WriteLine($"✅ נמצאו {networks.Count} רשתות\n");

                var connections = await _downloadService.TestNetworkConnectionsAsync();
                foreach (var conn in connections)
                {
                    LogMessage($"חיבור {conn.Key}: {conn.Value}");
                    System.Console.WriteLine($"   {conn.Value} {conn.Key}");

                    if (conn.Value.Contains("❌"))
                        LogError($"CONNECTION_{conn.Key}", $"רשת {conn.Key} לא זמינה");
                }

                System.Console.WriteLine("\n💡 לתוצאות טובות יותר, השתמש באפשרות 1 או 3 (שיטת King Store)");
            }
            catch (Exception ex)
            {
                LogError("DIAGNOSTIC", $"שגיאה באבחון: {ex.Message}\n{ex.StackTrace}");
                System.Console.WriteLine($"❌ שגיאה באבחון: {ex.Message}");
            }
        }

        private static async Task RunTestMode()
        {
            // הפונקציה הישנה נשארת כמו שהיא...
            LogMessage("🧪 מתחיל מצב בדיקת מערכת");
            System.Console.WriteLine("\n🧪 מצב בדיקת מערכת");
            System.Console.WriteLine("==================");

            CheckConfigFiles();
            await CheckInternetConnection();
            CheckDirectories();
            CheckPermissions();
        }

        private static async Task ShowNetworksList()
        {
            System.Console.WriteLine("\n📋 רשימת רשתות זמינות");
            System.Console.WriteLine("=====================");

            try
            {
                var networks = await _downloadService!.GetActiveNetworksAsync();

                foreach (var network in networks)
                {
                    System.Console.WriteLine($"🏪 {network.Name}");
                    System.Console.WriteLine($"   🆔 {network.Id}");
                    System.Console.WriteLine($"   🌐 {network.BaseUrl}");
                    System.Console.WriteLine();
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"❌ שגיאה בטעינת רשתות: {ex.Message}");
            }
        }

        private static async Task CheckDownloadedFiles()
        {
            System.Console.WriteLine("\n📁 בדיקת קבצים שהורדו");
            System.Console.WriteLine("====================");

            var baseFolder = "DownloadedFiles";
            if (!Directory.Exists(baseFolder))
            {
                System.Console.WriteLine("❌ תיקיית הורדות לא נמצאה");
                return;
            }

            System.Console.WriteLine($"📂 נתיב: {Path.GetFullPath(baseFolder)}");

            var subFolders = new[] { "StoresFull", "PriceFull", "PromoFull", "ZIP_Files", "Raw_Data" };

            foreach (var folder in subFolders)
            {
                var folderPath = Path.Combine(baseFolder, folder);
                if (Directory.Exists(folderPath))
                {
                    var files = Directory.GetFiles(folderPath);
                    var totalSize = files.Sum(f => new FileInfo(f).Length);

                    System.Console.WriteLine($"📁 {folder}: {files.Length} קבצים ({totalSize / 1024:N0} KB)");

                    if (files.Length > 0)
                    {
                        var newestFile = files
                            .Select(f => new FileInfo(f))
                            .OrderByDescending(f => f.CreationTime)
                            .FirstOrDefault();

                        if (newestFile != null)
                        {
                            System.Console.WriteLine($"   🕒 אחרון: {newestFile.Name} ({newestFile.CreationTime:dd/MM HH:mm})");
                        }
                    }
                }
                else
                {
                    System.Console.WriteLine($"📁 {folder}: לא קיים");
                }
            }
        }

        private static async Task ShowAdvancedSettings()
        {
            System.Console.WriteLine("\n⚙️ הגדרות מתקדמות");
            System.Console.WriteLine("==================");
            System.Console.WriteLine("1. ניקוי קבצי לוג ישנים");
            System.Console.WriteLine("2. ניקוי תיקיות הורדה");
            System.Console.WriteLine("3. יצירת תיקיות מחדש");
            System.Console.WriteLine("4. בדיקת הגדרות");
            System.Console.WriteLine("0. חזרה");

            System.Console.Write("\n👉 בחר אפשרות: ");
            var choice = System.Console.ReadLine();

            switch (choice)
            {
                case "1":
                    CleanOldLogFiles();
                    break;
                case "2":
                    CleanDownloadFolders();
                    break;
                case "3":
                    CreateDownloadFolders();
                    break;
                case "4":
                    await CheckSettings();
                    break;
                default:
                    return;
            }
        }

        private static void ShowHelp()
        {
            System.Console.WriteLine("\n📖 עזרה - BinaProjects Downloader Hybrid");
            System.Console.WriteLine("=========================================");
            System.Console.WriteLine("🎯 מצבי הפעלה:");
            System.Console.WriteLine("   1. הורדה אוטומטית (King Store) ⭐ - מוריד מכל הרשתות");
            System.Console.WriteLine("   2. אבחון שגיאות - בודק מה לא עובד");
            System.Console.WriteLine("   3. הורדה מהירה (King Store) ⭐ - רשת אחת בלבד");
            System.Console.WriteLine("   4. בדיקת מערכת - וידוא תקינות");
            System.Console.WriteLine();
            System.Console.WriteLine("⭐ שיטת King Store:");
            System.Console.WriteLine("   • פשוט ומהיר יותר");
            System.Console.WriteLine("   • מבוסס על הקוד המקורי שעבד");
            System.Console.WriteLine("   • מחפש רק לתאריך היום");
            System.Console.WriteLine("   • סינון מדויק לקבצים הנכונים");
            System.Console.WriteLine();
            System.Console.WriteLine("🖥️ הפעלה מקו הפקודה:");
            System.Console.WriteLine("   dotnet run auto     - הורדה אוטומטית (King Store)");
            System.Console.WriteLine("   dotnet run debug    - אבחון שגיאות");
            System.Console.WriteLine("   dotnet run quick    - הורדה מהירה (King Store)");
            System.Console.WriteLine("   dotnet run test     - בדיקת מערכת");
        }

        // פונקציות עזר נוספות...

        private static void CheckConfigFiles()
        {
            var files = new[] { "appsettings.json", "BinaProjectsNetworks.json" };

            foreach (var file in files)
            {
                if (File.Exists(file))
                {
                    LogMessage($"קובץ {file} נמצא");
                    System.Console.WriteLine($"   ✅ {file}");
                }
                else
                {
                    LogError("MISSING_CONFIG", $"קובץ {file} חסר");
                    System.Console.WriteLine($"   ❌ {file} - חסר!");
                }
            }
        }

        private static async Task CheckInternetConnection()
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                var response = await client.GetAsync("https://www.google.com");

                if (response.IsSuccessStatusCode)
                {
                    LogMessage("חיבור אינטרנט תקין");
                    System.Console.WriteLine("   ✅ חיבור אינטרנט תקין");
                }
                else
                {
                    LogError("INTERNET", $"בעיה בחיבור אינטרנט: {response.StatusCode}");
                    System.Console.WriteLine($"   ❌ בעיה בחיבור: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                LogError("INTERNET_EX", $"שגיאה בבדיקת אינטרנט: {ex.Message}");
                System.Console.WriteLine($"   ❌ שגיאה: {ex.Message}");
            }
        }

        private static void CheckDirectories()
        {
            var baseDir = "DownloadedFiles";
            var subDirs = new[] { "StoresFull", "PriceFull", "PromoFull", "ZIP_Files", "Raw_Data" };

            if (Directory.Exists(baseDir))
            {
                LogMessage($"תיקיית בסיס {baseDir} קיימת");
                System.Console.WriteLine($"   ✅ תיקיית בסיס: {baseDir}");

                foreach (var subDir in subDirs)
                {
                    var path = Path.Combine(baseDir, subDir);
                    if (Directory.Exists(path))
                    {
                        System.Console.WriteLine($"      ✅ {subDir}");
                    }
                    else
                    {
                        System.Console.WriteLine($"      ❌ {subDir} - חסרה");
                    }
                }
            }
            else
            {
                LogError("MISSING_DIR", "תיקיית DownloadedFiles חסרה");
                System.Console.WriteLine($"   ❌ תיקיית {baseDir} - חסרה!");
            }
        }

        private static void CheckPermissions()
        {
            try
            {
                var testFile = "test_permissions.tmp";
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);

                LogMessage("הרשאות כתיבה תקינות");
                System.Console.WriteLine("   ✅ הרשאות כתיבה תקינות");
            }
            catch (Exception ex)
            {
                LogError("PERMISSIONS", $"בעיה בהרשאות: {ex.Message}");
                System.Console.WriteLine($"   ❌ בעיה בהרשאות: {ex.Message}");
            }
        }

        private static void CleanOldLogFiles()
        {
            try
            {
                var logFiles = Directory.GetFiles(".", "BinaProjects_*_*.txt");
                var oldFiles = logFiles.Where(f => File.GetCreationTime(f) < DateTime.Now.AddDays(-7));

                foreach (var file in oldFiles)
                {
                    File.Delete(file);
                }

                System.Console.WriteLine($"🗑️ נוקו {oldFiles.Count()} קבצי לוג ישנים");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"❌ שגיאה בניקוי: {ex.Message}");
            }
        }

        private static void CleanDownloadFolders()
        {
            try
            {
                var baseFolder = "DownloadedFiles";
                if (Directory.Exists(baseFolder))
                {
                    Directory.Delete(baseFolder, true);
                    System.Console.WriteLine("🗑️ תיקיות הורדה נוקו");
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"❌ שגיאה בניקוי: {ex.Message}");
            }
        }

        private static async Task CheckSettings()
        {
            System.Console.WriteLine("⚙️ הגדרות נוכחיות:");
            System.Console.WriteLine($"📁 תיקיית עבודה: {Directory.GetCurrentDirectory()}");
            System.Console.WriteLine($"📄 קבצי לוג: {LogFileName}");

            if (File.Exists("appsettings.json"))
            {
                var content = await File.ReadAllTextAsync("appsettings.json");
                System.Console.WriteLine($"📋 גודל appsettings.json: {content.Length} תווים");
            }
        }

        // פונקציות לוגים
        private static void LogMessage(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var logEntry = $"[{timestamp}] {message}";
            AllLogs.Add(logEntry);
        }

        private static void LogError(string errorCode, string errorMessage)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var errorEntry = $"[{timestamp}] ERROR_{errorCode}: {errorMessage}";
            AllLogs.Add(errorEntry);
            ErrorSummary.Add(errorEntry);
        }

        private static async Task SaveAllLogsToFile()
        {
            try
            {
                await File.WriteAllLinesAsync(LogFileName, AllLogs, Encoding.UTF8);

                if (ErrorSummary.Any())
                {
                    await File.WriteAllLinesAsync(ErrorFileName, ErrorSummary, Encoding.UTF8);
                }

                System.Console.WriteLine($"\n💾 לוגים נשמרו: {Path.GetFullPath(LogFileName)}");
                if (ErrorSummary.Any())
                {
                    System.Console.WriteLine($"❌ שגיאות נשמרו: {Path.GetFullPath(ErrorFileName)}");
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"❌ שגיאה בשמירת לוגים: {ex.Message}");
            }
        }

        private static void ShowErrorSummary()
        {
            if (ErrorSummary.Any())
            {
                System.Console.WriteLine($"\n🚨 סיכום שגיאות: {ErrorSummary.Count} שגיאות נמצאו");
                System.Console.WriteLine($"📄 פרטים מלאים: {ErrorFileName}");
            }
            else
            {
                System.Console.WriteLine("\n✅ לא נמצאו שגיאות!");
            }
        }
    }

    // 🆕 מחלקות עזר פשוטות
    public class SimpleDownloadResult
    {
        public bool Success { get; set; }
        public string FileName { get; set; } = "";
        public string? ErrorMessage { get; set; }
        public long FileSize { get; set; }
    }

    public class BinaProjectsFileInfo
    {
        public string NetworkId { get; set; } = "";
        public string FileName { get; set; } = "";
        public string Store { get; set; } = "";
        public string TypeFile { get; set; } = "";
        public string DateFile { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
    }
}