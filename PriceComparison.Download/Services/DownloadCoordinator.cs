/*using PriceComparison.Download.Scrapers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;

public class DownloadCoordinator
{
    private readonly IConfiguration _config;

    public DownloadCoordinator(IConfiguration config)
    {
        _config = config;
    }

    public async Task RunAsync()
    {
        var chains = _config.GetSection("Chains").Get<List<ChainConfig>>();
        Console.WriteLine($"🔧 Found {chains?.Count} chains to process.");

        foreach (var chain in chains)
        {
            Console.WriteLine($"➡️  Starting download for chain: {chain.Name} (Type: {chain.Type})");

            if (chain.Type == "HtmlTable" && chain.Name == "KingStore")
            {
                var scraper = new BinaprojectScraper(chain.BaseUrl, chain.DownloadDir);
                await scraper.DownloadTodayFilesAsync(chain.StoreCode);
            }
            else
            {
                Console.WriteLine($"⚠️ Skipped chain: {chain.Name}");
            }
        }
    }

}*/
