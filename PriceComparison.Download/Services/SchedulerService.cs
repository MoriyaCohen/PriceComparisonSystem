using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using PriceComparison.Download.Models;
using PriceComparison.Download.Configuration;

namespace PriceComparison.Download.Services
{
    /// <summary>
    /// שירות תזמון להרצה אוטומטית של הורדות
    /// מנהל הרצה יומית או לפי לוח זמנים מותאם אישית
    /// </summary>
    public class SchedulerService : IDisposable
    {
        #region Fields & Properties

        /// <summary>
        /// מתאם ההורדות
        /// </summary>
        private readonly DownloadCoordinator _downloadCoordinator;

        /// <summary>
        /// הגדרות התצורה
        /// </summary>
        private readonly ChainConfiguration _configuration;

        /// <summary>
        /// טיימר לתזמון
        /// </summary>
        private Timer? _schedulerTimer;

        /// <summary>
        /// האם השירות פעיל
        /// </summary>
        public bool IsRunning { get; private set; }

        /// <summary>
        /// זמן ההרצה הבאה המתוכננת
        /// </summary>
        public DateTime? NextScheduledRun { get; private set; }

        /// <summary>
        /// זמן ההרצה האחרונה
        /// </summary>
        public DateTime? LastRun { get; private set; }

        /// <summary>
        /// סטטוס ההרצה האחרונה
        /// </summary>
        public string? LastRunStatus { get; private set; }

        /// <summary>
        /// אירוע התחלת הרצה מתוזמנת
        /// </summary>
        public event EventHandler<ScheduledRunEventArgs>? ScheduledRunStarted;

        /// <summary>
        /// אירוע סיום הרצה מתוזמנת
        /// </summary>
        public event EventHandler<ScheduledRunCompletedEventArgs>? ScheduledRunCompleted;

        #endregion

        #region Constructors

        /// <summary>
        /// בנאי עם מתאם הורדות וקונפיגורציה
        /// </summary>
        /// <param name="downloadCoordinator">מתאם ההורדות</param>
        /// <param name="configuration">הגדרות התצורה</param>
        public SchedulerService(DownloadCoordinator downloadCoordinator, ChainConfiguration configuration)
        {
            _downloadCoordinator = downloadCoordinator ?? throw new ArgumentNullException(nameof(downloadCoordinator));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        /// <summary>
        /// בנאי ברירת מחדל
        /// </summary>
        public SchedulerService() : this(
            new DownloadCoordinator(),
            ChainConfiguration.CreateDefault())
        {
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// התחלת תזמון יומי
        /// </summary>
        /// <param name="hour">שעה להרצה (0-23)</param>
        /// <param name="minute">דקה להרצה (0-59)</param>
        /// <returns>האם התזמון התחיל בהצלחה</returns>
        public bool StartDailySchedule(int hour = 3, int minute = 0)
        {
            try
            {
                if (IsRunning)
                {
                    Console.WriteLine("⚠️ התזמון כבר פעיל");
                    return false;
                }

                if (hour < 0 || hour > 23 || minute < 0 || minute > 59)
                {
                    throw new ArgumentException("שעה או דקה לא תקינים");
                }

                // חישוב הזמן עד ההרצה הראשונה
                var now = DateTime.Now;
                var nextRun = new DateTime(now.Year, now.Month, now.Day, hour, minute, 0);

                // אם הזמן עבר היום, עבור למחר
                if (nextRun <= now)
                {
                    nextRun = nextRun.AddDays(1);
                }

                NextScheduledRun = nextRun;
                var timeUntilFirstRun = nextRun - now;

                // יצירת טיימר - הרצה ראשונה ואז כל 24 שעות
                _schedulerTimer = new Timer(
                    ExecuteScheduledRun,
                    null,
                    timeUntilFirstRun,
                    TimeSpan.FromDays(1));

                IsRunning = true;

                Console.WriteLine($"⏰ תזמון יומי הופעל - הרצה ראשונה ב-{nextRun:yyyy-MM-dd HH:mm}");
                Console.WriteLine($"📅 זמן המתנה: {timeUntilFirstRun:dd\\.hh\\:mm\\:ss}");

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ שגיאה בהפעלת תזמון: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// התחלת תזמון מותאם אישית
        /// </summary>
        /// <param name="interval">מרווח זמן בין הרצות</param>
        /// <param name="startDelay">דחיית התחלה (ברירת מחדל: מיידי)</param>
        /// <returns>האם התזמון התחיל בהצלחה</returns>
        public bool StartCustomSchedule(TimeSpan interval, TimeSpan? startDelay = null)
        {
            try
            {
                if (IsRunning)
                {
                    Console.WriteLine("⚠️ התזמון כבר פעיל");
                    return false;
                }

                if (interval.TotalMinutes < 1)
                {
                    throw new ArgumentException("מרווח זמן חייב להיות לפחות דקה אחת");
                }

                startDelay ??= TimeSpan.Zero;
                NextScheduledRun = DateTime.Now.Add(startDelay.Value);

                _schedulerTimer = new Timer(
                    ExecuteScheduledRun,
                    null,
                    startDelay.Value,
                    interval);

                IsRunning = true;

                Console.WriteLine($"⏰ תזמון מותאם אישית הופעל - כל {interval}");
                Console.WriteLine($"🚀 הרצה ראשונה ב-{NextScheduledRun:yyyy-MM-dd HH:mm:ss}");

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ שגיאה בהפעלת תזמון מותאם: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// עצירת התזמון
        /// </summary>
        public void StopSchedule()
        {
            try
            {
                _schedulerTimer?.Dispose();
                _schedulerTimer = null;
                IsRunning = false;
                NextScheduledRun = null;

                Console.WriteLine("🛑 התזמון הופסק");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ שגיאה בהפסקת תזמון: {ex.Message}");
            }
        }

        /// <summary>
        /// הרצה מיידית (מחוץ לתזמון)
        /// </summary>
        /// <param name="date">תאריך להורדה (ברירת מחדל: היום)</param>
        /// <param name="fileTypes">סוגי קבצים להורדה</param>
        /// <returns>תוצאות ההרצה</returns>
        public async Task<CoordinatorResult> RunNowAsync(string? date = null, FileTypeFilter fileTypes = FileTypeFilter.All)
        {
            date ??= DateTime.Now.ToString("dd/MM/yyyy");

            Console.WriteLine($"🚀 מתחיל הרצה מיידית לתאריך {date}");

            var startTime = DateTime.Now;
            OnScheduledRunStarted(new ScheduledRunEventArgs
            {
                RunType = ScheduledRunType.Manual,
                StartTime = startTime,
                TargetDate = date
            });

            try
            {
                var result = await _downloadCoordinator.DownloadFromAllChainsAsync(date, fileTypes);

                LastRun = startTime;
                LastRunStatus = result.IsSuccess ? "הצליח" : "נכשל";

                OnScheduledRunCompleted(new ScheduledRunCompletedEventArgs
                {
                    RunType = ScheduledRunType.Manual,
                    StartTime = startTime,
                    EndTime = DateTime.Now,
                    Result = result,
                    IsSuccess = result.IsSuccess
                });

                return result;
            }
            catch (Exception ex)
            {
                LastRun = startTime;
                LastRunStatus = $"שגיאה: {ex.Message}";

                var errorResult = CoordinatorResult.Empty();
                errorResult.IsSuccess = false;
                errorResult.GeneralError = ex.Message;

                OnScheduledRunCompleted(new ScheduledRunCompletedEventArgs
                {
                    RunType = ScheduledRunType.Manual,
                    StartTime = startTime,
                    EndTime = DateTime.Now,
                    Result = errorResult,
                    IsSuccess = false,
                    ErrorMessage = ex.Message
                });

                Console.WriteLine($"❌ הרצה מיידית נכשלה: {ex.Message}");
                return errorResult;
            }
        }

        /// <summary>
        /// הרצה עבור תאריך ספציפי
        /// </summary>
        /// <param name="targetDate">התאריך המבוקש</param>
        /// <param name="chainNames">רשתות ספציפיות (ריק = כל הרשתות)</param>
        /// <returns>תוצאות ההרצה</returns>
        public async Task<CoordinatorResult> RunForDateAsync(DateTime targetDate, IEnumerable<string>? chainNames = null)
        {
            var dateStr = targetDate.ToString("dd/MM/yyyy");
            Console.WriteLine($"📅 מתחיל הרצה עבור תאריך {dateStr}");

            try
            {
                CoordinatorResult result;

                if (chainNames?.Any() == true)
                {
                    result = await _downloadCoordinator.DownloadFromSpecificChainsAsync(chainNames, dateStr);
                }
                else
                {
                    result = await _downloadCoordinator.DownloadFromAllChainsAsync(dateStr);
                }

                Console.WriteLine($"✅ הרצה עבור {dateStr} הושלמה: {result.SuccessfulDownloads}/{result.TotalRequests}");
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ הרצה עבור {dateStr} נכשלה: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// קבלת סטטוס התזמון
        /// </summary>
        /// <returns>סטטוס נוכחי</returns>
        public SchedulerStatus GetStatus()
        {
            return new SchedulerStatus
            {
                IsRunning = IsRunning,
                NextScheduledRun = NextScheduledRun,
                LastRun = LastRun,
                LastRunStatus = LastRunStatus,
                ScheduledHour = _configuration.General.ScheduledHour,
                ScheduledMinute = _configuration.General.ScheduledMinute,
                TimeUntilNextRun = NextScheduledRun?.Subtract(DateTime.Now)
            };
        }

        /// <summary>
        /// הרצה מבחן - בדיקת זמינות כל הרשתות
        /// </summary>
        /// <returns>דוח זמינות</returns>
        public async Task<AvailabilityReport> RunHealthCheckAsync()
        {
            Console.WriteLine("🔍 מריץ בדיקת זמינות רשתות...");

            try
            {
                return await _downloadCoordinator.CheckAllChainsAvailabilityAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ שגיאה בבדיקת זמינות: {ex.Message}");
                return new AvailabilityReport
                {
                    CheckTime = DateTime.Now,
                    ChainAvailabilities = new List<ChainAvailability>()
                };
            }
        }

        /// <summary>
        /// הגדרת תזמון מהקונפיגורציה
        /// </summary>
        /// <returns>האם ההגדרה הצליחה</returns>
        public bool StartFromConfiguration()
        {
            return StartDailySchedule(
                _configuration.General.ScheduledHour,
                _configuration.General.ScheduledMinute);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// ביצוע הרצה מתוזמנת
        /// </summary>
        /// <param name="state">פרמטר לא בשימוש</param>
        private async void ExecuteScheduledRun(object? state)
        {
            var startTime = DateTime.Now;
            var targetDate = startTime.ToString("dd/MM/yyyy");

            Console.WriteLine($"⏰ מתחיל הרצה מתוזמנת - {startTime:yyyy-MM-dd HH:mm:ss}");

            OnScheduledRunStarted(new ScheduledRunEventArgs
            {
                RunType = ScheduledRunType.Scheduled,
                StartTime = startTime,
                TargetDate = targetDate
            });

            try
            {
                // ביצוע ההרצה
                var result = await _downloadCoordinator.DownloadFromAllChainsAsync(targetDate);

                // עדכון סטטוס
                LastRun = startTime;
                LastRunStatus = result.IsSuccess ?
                    $"הצליח: {result.SuccessfulDownloads}/{result.TotalRequests} רשתות" :
                    "נכשל";

                // חישוב הרצה הבאה
                if (IsRunning)
                {
                    NextScheduledRun = NextScheduledRun?.AddDays(1) ?? DateTime.Now.AddDays(1);
                }

                // דיווח על סיום
                OnScheduledRunCompleted(new ScheduledRunCompletedEventArgs
                {
                    RunType = ScheduledRunType.Scheduled,
                    StartTime = startTime,
                    EndTime = DateTime.Now,
                    Result = result,
                    IsSuccess = result.IsSuccess
                });

                if (result.IsSuccess)
                {
                    Console.WriteLine($"✅ הרצה מתוזמנת הושלמה בהצלחה");
                    Console.WriteLine($"📊 הורדו {result.TotalFilesDownloaded} קבצים ({result.TotalSizeDownloaded:N0} bytes)");
                }
                else
                {
                    Console.WriteLine($"⚠️ הרצה מתוזמנת הושלמה עם שגיאות: {result.GeneralError}");
                }

                if (NextScheduledRun.HasValue)
                {
                    Console.WriteLine($"⏭️ הרצה הבאה מתוכננת ל-{NextScheduledRun:yyyy-MM-dd HH:mm}");
                }
            }
            catch (Exception ex)
            {
                LastRun = startTime;
                LastRunStatus = $"שגיאה: {ex.Message}";

                var errorResult = CoordinatorResult.Empty();
                errorResult.IsSuccess = false;
                errorResult.GeneralError = ex.Message;

                OnScheduledRunCompleted(new ScheduledRunCompletedEventArgs
                {
                    RunType = ScheduledRunType.Scheduled,
                    StartTime = startTime,
                    EndTime = DateTime.Now,
                    Result = errorResult,
                    IsSuccess = false,
                    ErrorMessage = ex.Message
                });

                Console.WriteLine($"❌ הרצה מתוזמנת נכשלה: {ex.Message}");
            }
        }

        /// <summary>
        /// הפעלת אירוע התחלת הרצה
        /// </summary>
        /// <param name="args">פרטי האירוע</param>
        protected virtual void OnScheduledRunStarted(ScheduledRunEventArgs args)
        {
            ScheduledRunStarted?.Invoke(this, args);
        }

        /// <summary>
        /// הפעלת אירוע סיום הרצה
        /// </summary>
        /// <param name="args">פרטי האירוע</param>
        protected virtual void OnScheduledRunCompleted(ScheduledRunCompletedEventArgs args)
        {
            ScheduledRunCompleted?.Invoke(this, args);
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// שחרור משאבים
        /// </summary>
        public void Dispose()
        {
            StopSchedule();
            _downloadCoordinator?.Dispose();
            GC.SuppressFinalize(this);
        }

        #endregion
    }

    #region Event Args & Support Classes

    /// <summary>
    /// פרטי אירוע התחלת הרצה מתוזמנת
    /// </summary>
    public class ScheduledRunEventArgs : EventArgs
    {
        /// <summary>
        /// סוג ההרצה
        /// </summary>
        public ScheduledRunType RunType { get; set; }

        /// <summary>
        /// זמן התחלה
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// תאריך יעד להורדה
        /// </summary>
        public string TargetDate { get; set; } = "";
    }

    /// <summary>
    /// פרטי אירוע סיום הרצה מתוזמנת
    /// </summary>
    public class ScheduledRunCompletedEventArgs : ScheduledRunEventArgs
    {
        /// <summary>
        /// זמן סיום
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// תוצאות ההרצה
        /// </summary>
        public CoordinatorResult Result { get; set; } = new();

        /// <summary>
        /// האם הרצה הצליחה
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// הודעת שגיאה (אם קיימת)
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// משך זמן ההרצה
        /// </summary>
        public TimeSpan Duration => EndTime.Subtract(StartTime);
    }

    /// <summary>
    /// סוגי הרצה מתוזמנת
    /// </summary>
    public enum ScheduledRunType
    {
        /// <summary>
        /// הרצה מתוזמנת אוטומטית
        /// </summary>
        Scheduled,

        /// <summary>
        /// הרצה ידנית
        /// </summary>
        Manual,

        /// <summary>
        /// הרצת בדיקה
        /// </summary>
        Test
    }

    /// <summary>
    /// סטטוס שירות התזמון
    /// </summary>
    public class SchedulerStatus
    {
        /// <summary>
        /// האם התזמון פעיל
        /// </summary>
        public bool IsRunning { get; set; }

        /// <summary>
        /// זמן ההרצה הבאה
        /// </summary>
        public DateTime? NextScheduledRun { get; set; }

        /// <summary>
        /// זמן ההרצה האחרונה
        /// </summary>
        public DateTime? LastRun { get; set; }

        /// <summary>
        /// סטטוס ההרצה האחרונה
        /// </summary>
        public string? LastRunStatus { get; set; }

        /// <summary>
        /// שעה מתוכננת להרצה יומית
        /// </summary>
        public int ScheduledHour { get; set; }

        /// <summary>
        /// דקה מתוכננת להרצה יומית
        /// </summary>
        public int ScheduledMinute { get; set; }

        /// <summary>
        /// זמן עד ההרצה הבאה
        /// </summary>
        public TimeSpan? TimeUntilNextRun { get; set; }

        /// <summary>
        /// פורמט זמן נוח לקריאה
        /// </summary>
        public string FormattedTimeUntilNext
        {
            get
            {
                if (!TimeUntilNextRun.HasValue || TimeUntilNextRun.Value.TotalSeconds <= 0)
                    return "לא מתוזמן";

                var time = TimeUntilNextRun.Value;
                if (time.TotalDays >= 1)
                    return $"{time.Days} ימים, {time.Hours:D2}:{time.Minutes:D2}";
                else
                    return $"{time.Hours:D2}:{time.Minutes:D2}:{time.Seconds:D2}";
            }
        }

        /// <summary>
        /// תיאור סטטוס
        /// </summary>
        public string StatusDescription
        {
            get
            {
                if (!IsRunning)
                    return "לא פעיל";

                if (NextScheduledRun.HasValue)
                    return $"פעיל - הרצה הבאה: {NextScheduledRun:yyyy-MM-dd HH:mm}";

                return "פעיל";
            }
        }
    }

    #endregion
}
