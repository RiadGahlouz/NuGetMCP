public interface INuGetApiService
{
    Task<NuGetPackageInfo?> GetPackageInfoAsync(string packageId, string? version = null);
    Task<NuGetSearchResult?> SearchPackagesAsync(string query, int skip = 0, int take = 20);
    Task<bool> PublishPackageAsync(string packagePath, string? apiKey = null);
    Task<bool> UnlistPackageAsync(string packageId, string version, string? apiKey = null);
}