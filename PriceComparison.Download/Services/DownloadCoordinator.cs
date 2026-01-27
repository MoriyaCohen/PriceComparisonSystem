using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using PriceComparison.Download.Core;
using PriceComparison.Download.Models;
using PriceComparison.Download.Configuration;
using PriceComparison.Download.Exceptions;
using System.Collections.Concurrent;

namespace PriceComparison.Download.Services
{
    /// <summary>
    /// מתאם הורדות מרכזי - מנהל הורדה ממספר רשתות במקביל
    /// כולל ניהול תור, מעקב התקדמות וטיפול בשגיאות
    /// </summary>
    public class DownloadCoordinator
    {
        #region Fields & Properties

        /// <summary>
        /// Factory ליצירת מודולי הורדה
        /// </summary>
        private readonly ChainDownloaderFactory _downloaderFactory;

        /// <summary>
        /// הגדרות התצורה
        /// </summary>
        private readonly ChainConfiguration _configuration;

        /// <summary>
        /// Semaphore לניהול מספר הורדות מקביליות
        /// </summary>
        private readonly SemaphoreSlim _concurrencySemaphore;

        /// <summary>
        /// תור המשימות הממתינות
        /// </summary>
        private readonly ConcurrentQueue<DownloadTask> _pendingTasks;

        /// <summary>
        /// משימות בביצוע
        /// </summary>
        private readonly ConcurrentDictionary<string, DownloadTask> _activeTasks;

        /// <summary>
        /// תוצאות שהושלמו
        /// </summary>
        private readonly ConcurrentDictionary<string, DownloadResult> _completedResults;

        /// <summary>
        /// מנעול לעדכונים thread-safe
        /// </summary>
        private readonly object _statusLock = new object();

        /// <summary>
        /// האם התיאום במצב פעיל
        /// </summary>
        public bool IsActive { get; private set; }

        /// <summary>
        /// מספר משימות ממתינות
        /// </summary>
        public int PendingTasksCount => _pendingTasks.Count;

        /// <summary>
        /// מספר משימות פעילות
        /// </summary>
        public int ActiveTasksCount => _activeTasks.Count;

        /// <summary>
        /// מספר משימות שהושלמו
        /// </summary>
        public int CompletedTasksCount => _completedResults.Count;

        /// <summary>
        /// אירוע עדכון התקדמות
        /// </summary>
        public event EventHandler<DownloadProgressEventArgs>? ProgressUpdated;

        /// <summary>
        /// אירוע השלמת משימה
        /// </summary>
        public event EventHandler<DownloadCompletedEventArgs>? TaskCompleted;

        #endregion

        #region Constructors

        /// <summary>
        /// בנאי עם Factory קיים
        /// </summary>
        /// <param name="downloaderFactory">Factory לייצור מודולי הורדה</param>
        public DownloadCoordinator(ChainDownloaderFactory downloaderFactory)
        {
            _downloaderFactory = downloaderFactory ?? throw new ArgumentNullException(nameof(downloaderFactory));
            _configuration = LoadConfiguration();

            _concurrencySemaphore = new SemaphoreSlim(_configuration.General.MaxConcurrentDownloads);
            _pendingTasks = new ConcurrentQueue<DownloadTask>();
            _activeTasks = new ConcurrentDictionary<string, DownloadTask>();
            _completedResults = new ConcurrentDictionary<string, DownloadResult>();
        }

        /// <summary>
        /// בנאי ברירת מחדל
        /// </summary>
        public DownloadCoordinator() : this(ChainDownloaderFactory.CreateDefault())
        {
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// הורדה מכל הרשתות הפעילות
        /// </summary>
        /// <param name="date">התאריך להורדה</param>
        /// <param name="fileTypes">סוגי קבצים להורדה</param>
        /// <returns>תוצאות כלליות</returns>
        public async Task<CoordinatorResult> DownloadFromAllChainsAsync(
            string? date = null,
            FileTypeFilter fileTypes = FileTypeFilter.All)
        {
            date ??= DateTime.Now.ToString("dd/MM/yyyy");

            Console.WriteLine($"🚀 מתחיל הורדה מכל הרשתות לתאריך {date}");

            var availableChains = _downloaderFactory.GetAvailableChains();
            var requests = availableChains.Select(chain => new DownloadRequest
            {
                Date = date,
                ChainName = chain.Name,
                FileTypes = fileTypes
            }).ToList();

            return await DownloadMultipleAsync(requests);
        }

        /// <summary>
        /// הורדה מרשתות ספציפיות
        /// </summary>
        /// <param name="chainNames">שמות הרשתות</param>
        /// <param name="date">התאריך להורדה</param>
        /// <param name="fileTypes">סוגי קבצים להורדה</param>
        /// <returns>תוצאות כלליות</returns>
        public async Task<CoordinatorResult> DownloadFromSpecificChainsAsync(
            IEnumerable<string> chainNames,
            string? date = null,
            FileTypeFilter fileTypes = FileTypeFilter.All)
        {
            date ??= DateTime.Now.ToString("dd/MM/yyyy");

            var requests = chainNames.Select(chainName => new DownloadRequest
            {
                Date = date,
                ChainName = chainName,
                FileTypes = fileTypes
            }).ToList();

            return await DownloadMultipleAsync(requests);
        }

        /// <summary>
        /// הורדה ממספר בקשות
        /// </summary>
        /// <param name="requests">רשימת בקשות הורדה</param>
        /// <returns>תוצאות כלליות</returns>
        public async Task<CoordinatorResult> DownloadMultipleAsync(IEnumerable<DownloadRequest> requests)
        {
            var requestsList = requests.ToList();
            if (!requestsList.Any())
            {
                return CoordinatorResult.Empty();
            }

            var result = new CoordinatorResult
            {
                StartTime = DateTime.Now,
                TotalRequests = requestsList.Count
            };

            try
            {
                IsActive = true;
                Console.WriteLine($"📋 מתכנן הורדה מ-{requestsList.Count} רשתות");

                // יצירת משימות הורדה
                var tasks = CreateDownloadTasks(requestsList);

                // הוספה לתור
                foreach (var task in tasks)
                {
                    _pendingTasks.Enqueue(task);
                }

                // ביצוע המשימות
                await ExecutePendingTasksAsync();

                // איסוף תוצאות
                result.Results.AddRange(_completedResults.Values);
                result.IsSuccess = result.Results.Any(r => r.IsSuccess);

                Console.WriteLine($"✅ הושלמה הורדה: {result.SuccessfulDownloads}/{result.TotalRequests} הצליחו");
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.GeneralError = $"שגיאה כללית בתיאום הורדות: {ex.Message}";
                Console.WriteLine($"❌ {result.GeneralError}");
            }
            finally
            {
                IsActive = false;
                result.EndTime = DateTime.Now;
                CleanupResources();
            }

            return result;
        }

        /// <summary>
        /// הורדה מרשת יחידה (אסינכרונית)
        /// </summary>
        /// <param name="request">בקשת ההורדה</param>
        /// <returns>תוצאת ההורדה</returns>
        public async Task<DownloadResult> DownloadSingleAsync(DownloadRequest request)
        {
            try
            {
                var downloader = _downloaderFactory.CreateDownloader(request.ChainName);
                return await downloader.DownloadAllFilesAsync(request);
            }
            catch (Exception ex)
            {
                return new DownloadResult
                {
                    ChainName = request.ChainName,
                    IsSuccess = false,
                    ErrorMessage = $"שגיאה בהורדה: {ex.Message}",
                    StartTime = DateTime.Now,
                    EndTime = DateTime.Now
                };
            }
        }

        /// <summary>
        /// ביטול כל המשימות הפעילות
        /// </summary>
        public async Task CancelAllTasksAsync()
        {
            Console.WriteLine("🛑 מבטל כל המשימות הפעילות...");

            IsActive = false;

            // ניקוי התור
            while (_pendingTasks.TryDequeue(out _)) { }

            // המתנה לסיום משימות פעילות (עד 30 ש')
            var timeout = TimeSpan.FromSeconds(30);
            var startTime = DateTime.Now;

            while (_activeTasks.Count > 0 && DateTime.Now - startTime < timeout)
            {
                await Task.Delay(1000);
            }

            CleanupResources();
            Console.WriteLine("✅ כל המשימות בוטלו");
        }

        /// <summary>
        /// קבלת סטטוס נוכחי
        /// </summary>
        /// <returns>סטטוס התיאום</returns>
        public CoordinatorStatus GetCurrentStatus()
        {
            lock (_statusLock)
            {
                return new CoordinatorStatus
                {
                    IsActive = IsActive,
                    PendingTasks = PendingTasksCount,
                    ActiveTasks = ActiveTasksCount,
                    CompletedTasks = CompletedTasksCount,
                    ActiveTaskDetails = _activeTasks.Values.Select(t => new TaskStatus
                    {
                        ChainName = t.Request.ChainName,
                        StartTime = t.StartTime,
                        Status = "בביצוע"
                    }).ToList()
                };
            }
        }

        /// <summary>
        /// בדיקת זמינות כל הרשתות
        /// </summary>
        /// <returns>דוח זמינות</returns>
        public async Task<AvailabilityReport> CheckAllChainsAvailabilityAsync()
        {
            var report = new AvailabilityReport { CheckTime = DateTime.Now };
            var chains = _downloaderFactory.GetAvailableChains();

            Console.WriteLine($"🔍 בודק זמינות {chains.Count} רשתות...");

            var tasks = chains.Select(async chain =>
            {
                try
                {
                    var downloader = _downloaderFactory.CreateDownloader(chain.Name);
                    var isAvailable = await downloader.IsServiceAvailableAsync();

                    return new ChainAvailability
                    {
                        ChainName = chain.Name,
                        IsAvailable = isAvailable,
                        ResponseTime = DateTime.Now - report.CheckTime,
                        ErrorMessage = isAvailable ? null : "שירות לא זמין"
                    };
                }
                catch (Exception ex)
                {
                    return new ChainAvailability
                    {
                        ChainName = chain.Name,
                        IsAvailable = false,
                        ResponseTime = DateTime.Now - report.CheckTime,
                        ErrorMessage = ex.Message
                    };
                }
            });

            report.ChainAvailabilities = (await Task.WhenAll(tasks)).ToList();

            var availableCount = report.ChainAvailabilities.Count(c => c.IsAvailable);
            Console.WriteLine($"📊 זמינות: {availableCount}/{chains.Count} רשתות זמינות");

            return report;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// יצירת משימות הורדה
        /// </summary>
        /// <param name="requests">בקשות ההורדה</param>
        /// <returns>רשימת משימות</returns>
        private List<DownloadTask> CreateDownloadTasks(List<DownloadRequest> requests)
        {
            return requests.Select(request => new DownloadTask
            {
                Id = Guid.NewGuid().ToString(),
                Request = request,
                CreatedTime = DateTime.Now,
                Status = DownloadTaskStatus.Pending
            }).ToList();
        }

        /// <summary>
        /// ביצוע כל המשימות הממתינות
        /// </summary>
        private async Task ExecutePendingTasksAsync()
        {
            var runningTasks = new List<Task>();

            // המשך עד שאין משימות ממתינות או פעילות
            while ((_pendingTasks.Count > 0 || _activeTasks.Count > 0) && IsActive)
            {
                // הוספת משימות חדשות עד המקסימום המותר
                while (_pendingTasks.TryDequeue(out var task) &&
                       _activeTasks.Count < _configuration.General.MaxConcurrentDownloads &&
                       IsActive)
                {
                    var taskExecution = ExecuteSingleTaskAsync(task);
                    runningTasks.Add(taskExecution);
                }

                // המתנה לסיום לפחות משימה אחת
                if (runningTasks.Count > 0)
                {
                    var completedTask = await Task.WhenAny(runningTasks);
                    runningTasks.Remove(completedTask);
                }

                // המתנה קצרה בין איטרציות
                await Task.Delay(100);
            }

            // המתנה לסיום כל המשימות הנותרות
            if (runningTasks.Count > 0)
            {
                await Task.WhenAll(runningTasks);
            }
        }

        /// <summary>
        /// ביצוע משימה יחידה
        /// </summary>
        /// <param name="task">המשימה לביצוע</param>
        private async Task ExecuteSingleTaskAsync(DownloadTask task)
        {
            await _concurrencySemaphore.WaitAsync();

            try
            {
                // עדכון סטטוס לפעיל
                task.StartTime = DateTime.Now;
                task.Status = DownloadTaskStatus.Running;
                _activeTasks.TryAdd(task.Id, task);

                // דיווח על התחלה
                OnProgressUpdated(new DownloadProgressEventArgs
                {
                    ChainName = task.Request.ChainName,
                    Status = "מתחיל הורדה",
                    Progress = 0
                });

                // ביצוע ההורדה
                var result = await DownloadSingleAsync(task.Request);

                // עדכון תוצאה
                task.EndTime = DateTime.Now;
                task.Status = result.IsSuccess ? DownloadTaskStatus.Completed : DownloadTaskStatus.Failed;
                task.Result = result;

                _completedResults.TryAdd(task.Id, result);

                // דיווח על סיום
                OnTaskCompleted(new DownloadCompletedEventArgs
                {
                    Task = task,
                    Result = result
                });

                OnProgressUpdated(new DownloadProgressEventArgs
                {
                    ChainName = task.Request.ChainName,
                    Status = result.IsSuccess ? "הושלם בהצלחה" : "נכשל",
                    Progress = 100,
                    ErrorMessage = result.ErrorMessage
                });

                // המתנה בין בקשות
                if (_configuration.General.DelayBetweenRequests > 0)
                {
                    await Task.Delay(_configuration.General.DelayBetweenRequests);
                }
            }
            catch (Exception ex)
            {
                task.EndTime = DateTime.Now;
                task.Status = DownloadTaskStatus.Failed;
                task.Result = new DownloadResult
                {
                    ChainName = task.Request.ChainName,
                    IsSuccess = false,
                    ErrorMessage = $"שגיאה לא צפויה: {ex.Message}",
                    StartTime = task.StartTime ?? DateTime.Now,
                    EndTime = DateTime.Now
                };

                _completedResults.TryAdd(task.Id, task.Result);

                OnProgressUpdated(new DownloadProgressEventArgs
                {
                    ChainName = task.Request.ChainName,
                    Status = "שגיאה",
                    Progress = 100,
                    ErrorMessage = ex.Message
                });
            }
            finally
            {
                _activeTasks.TryRemove(task.Id, out _);
                _concurrencySemaphore.Release();
            }
        }

        /// <summary>
        /// ניקוי משאבים
        /// </summary>
        private void CleanupResources()
        {
            _activeTasks.Clear();
            _completedResults.Clear();

            // ניקוי התור
            while (_pendingTasks.TryDequeue(out _)) { }
        }

        /// <summary>
        /// טעינת תצורה
        /// </summary>
        /// <returns>תצורת המערכת</returns>
        private ChainConfiguration LoadConfiguration()
        {
            try
            {
                // ניסיון טעינה מקובץ JSON
                if (File.Exists("chains.json"))
                {
                    var json = File.ReadAllText("chains.json");
                    var config = System.Text.Json.JsonSerializer.Deserialize<ChainConfiguration>(json, new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                    });

                    if (config != null)
                    {
                        return config;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ שגיאה בטעינת תצורה: {ex.Message}");
            }

            // fallback לתצורה ברירת מחדל
            return ChainConfiguration.CreateDefault();
        }

        /// <summary>
        /// הפעלת אירוע עדכון התקדמות
        /// </summary>
        /// <param name="args">פרטי האירוע</param>
        protected virtual void OnProgressUpdated(DownloadProgressEventArgs args)
        {
            ProgressUpdated?.Invoke(this, args);
        }

        /// <summary>
        /// הפעלת אירוע השלמת משימה
        /// </summary>
        /// <param name="args">פרטי האירוע</param>
        protected virtual void OnTaskCompleted(DownloadCompletedEventArgs args)
        {
            TaskCompleted?.Invoke(this, args);
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// שחרור משאבים
        /// </summary>
        public void Dispose()
        {
            _concurrencySemaphore?.Dispose();
            CleanupResources();
        }

        #endregion
    }

    #region Event Args & Support Classes

    /// <summary>
    /// פרטי אירוע עדכון התקדמות
    /// </summary>
    public class DownloadProgressEventArgs : EventArgs
    {
        public string ChainName { get; set; } = "";
        public string Status { get; set; } = "";
        public int Progress { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// פרטי אירוע השלמת משימה
    /// </summary>
    public class DownloadCompletedEventArgs : EventArgs
    {
        public DownloadTask Task { get; set; } = new();
        public DownloadResult Result { get; set; } = new();
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// משימת הורדה
    /// </summary>
    public class DownloadTask
    {
        public string Id { get; set; } = "";
        public DownloadRequest Request { get; set; } = new();
        public DateTime CreatedTime { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public DownloadTaskStatus Status { get; set; }
        public DownloadResult? Result { get; set; }

        public TimeSpan? Duration => EndTime?.Subtract(StartTime ?? CreatedTime);
    }

    /// <summary>
    /// מצבי משימת הורדה
    /// </summary>
    public enum DownloadTaskStatus
    {
        Pending,
        Running,
        Completed,
        Failed,
        Cancelled
    }

    /// <summary>
    /// תוצאות התיאום הכלליות
    /// </summary>
    public class CoordinatorResult
    {
        public bool IsSuccess { get; set; }
        public string? GeneralError { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public TimeSpan Duration => EndTime?.Subtract(StartTime) ?? TimeSpan.Zero;
        public int TotalRequests { get; set; }
        public List<DownloadResult> Results { get; set; } = new();

        public int SuccessfulDownloads => Results.Count(r => r.IsSuccess);
        public int FailedDownloads => Results.Count(r => !r.IsSuccess);
        public int TotalFilesDownloaded => Results.Sum(r => r.TotalDownloadedFiles);
        public long TotalSizeDownloaded => Results.Sum(r => r.TotalDownloadedSize);

        public static CoordinatorResult Empty() => new CoordinatorResult
        {
            IsSuccess = false,
            GeneralError = "אין בקשות להורדה",
            StartTime = DateTime.Now,
            EndTime = DateTime.Now
        };
    }

    /// <summary>
    /// סטטוס התיאום
    /// </summary>
    public class CoordinatorStatus
    {
        public bool IsActive { get; set; }
        public int PendingTasks { get; set; }
        public int ActiveTasks { get; set; }
        public int CompletedTasks { get; set; }
        public List<TaskStatus> ActiveTaskDetails { get; set; } = new();
    }

    /// <summary>
    /// סטטוס משימה
    /// </summary>
    public class TaskStatus
    {
        public string ChainName { get; set; } = "";
        public DateTime? StartTime { get; set; }
        public string Status { get; set; } = "";
    }

    /// <summary>
    /// דוח זמינות רשתות
    /// </summary>
    public class AvailabilityReport
    {
        public DateTime CheckTime { get; set; }
        public List<ChainAvailability> ChainAvailabilities { get; set; } = new();

        public int AvailableChains => ChainAvailabilities.Count(c => c.IsAvailable);
        public int TotalChains => ChainAvailabilities.Count;
        public double AvailabilityPercentage => TotalChains > 0 ? (double)AvailableChains / TotalChains * 100 : 0;
    }

    /// <summary>
    /// זמינות רשת יחידה
    /// </summary>
    public class ChainAvailability
    {
        public string ChainName { get; set; } = "";
        public bool IsAvailable { get; set; }
        public TimeSpan ResponseTime { get; set; }
        public string? ErrorMessage { get; set; }
    }

    #endregion
}
