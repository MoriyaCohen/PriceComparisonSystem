
using Coravel.Scheduling.Schedule.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PriceComparison.Download.New;
using Coravel;


namespace DownloadScheduler
{
    class SchedulerProgram
    {
        static async Task Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    services.AddScheduler();
                })
                .Build();

            var scheduler = host.Services.GetRequiredService<IScheduler>();

            // Schedule
            // 20:15 ישראל = 18:15 UTC
            scheduler.Schedule(async () =>
            {
                try
                {
                    Console.WriteLine($"⏰ מתחיל Download Task: {DateTime.Now}");
                    await ProgramRunner.RunAllSystemsWrapper();
                    Console.WriteLine($"✅ סיום Download Task: {DateTime.Now}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"💥 Scheduler Error: {ex.Message}");
                }
            })
            .Cron("23 16 * * *"); // כל יום בשעה 20:15 ישראל


            await host.StartAsync();

            Console.WriteLine("🚀 Scheduler רץ ברקע. לחץ Ctrl+C ליציאה.");

            await host.WaitForShutdownAsync();
        }
    }

    public static class ProgramRunner
    {
        public static async Task RunAllSystemsWrapper()
        {
            string currentDate = DateTime.Now.ToString("dd/MM/yyyy");
            await PriceComparison.Download.New.Program.RunAllSystems(currentDate);
        }
    }
}
