public interface IStorageProvider
{
    Task<bool> UploadAsync(string path, Stream content, Dictionary<string, string> metadata);
    Task<Stream> DownloadAsync(string path);
    Task<bool> DeleteAsync(string path);
    Task<List<string>> ListFilesAsync(string prefix);
    Task CleanupOldFilesAsync(DateTime cutoffDate);
}