public interface INuGetApiService
{
  Task<ToolResponse<NuGetPackageInfo>> GetPackageInfoAsync(string packageId, string? version = null);
  Task<ToolResponse<NuGetSearchResult>> SearchPackagesAsync(string query, int skip = 0, int take = 20);
  Task<ToolResponse<string>> PublishPackageAsync(string packagePath, string? apiKey = null);
  Task<ToolResponse<string>> DeletePackageAsync(string packageId, string? apiKey = null);
  Task<ToolResponse<string>> DeletePackageVersionAsync(string packageId, string version, string? apiKey = null);

  Task<List<NuGetPackageInfo>> GetUserPackagesAsync(string username);
}