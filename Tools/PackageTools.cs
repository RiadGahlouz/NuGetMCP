using System.ComponentModel;
using ModelContextProtocol.Server;

[McpServerToolType]
public static class PackageTools
{
  [McpServerTool, Description("Queries the package with the given ID.")]
  public static async Task<NuGetPackageInfo?> QueryPackage(
    INuGetApiService nuGetService,
    [Description("The ID of the package to query for")] string packageId,
    [Description("Optional version of the package to query for")] string? version = null)
  {
    var packageInfo = await nuGetService.GetPackageInfoAsync(packageId, version);
    return packageInfo;
  }

  [McpServerTool, Description("Searches for packages matching the given query.")]
  public static async Task<NuGetSearchResult?> SearchPackages(
    INuGetApiService nuGetService,
    [Description("The search query to use")] string query,
    [Description("The number of results to skip (for pagination)")] int skip = 0,
    [Description("The number of results to take (for pagination)")] int take = 20)
  {
    var searchResult = await nuGetService.SearchPackagesAsync(query, skip, take);
    return searchResult;
  }

  [McpServerTool, Description("Publishes a package to the NuGet repository.")]
  public static async Task<bool> PublishPackage(
    INuGetApiService nuGetService,
    [Description("The path to the package to publish")] string packageFilePath,
    [Description("Optional API key for publishing")] string? apiKey = null)
  {
    var packageData = await File.ReadAllBytesAsync(packageFilePath);
    return await nuGetService.PublishPackageAsync(packageData, apiKey);
  }

  [McpServerTool, Description("Unlists a package from the NuGet repository.")]
  public static async Task<bool> UnlistPackage(
    INuGetApiService nuGetService,
    [Description("The ID of the package to unlist")] string packageId,
    [Description("The version of the package to unlist")] string version,
    [Description("Optional API key for unlisting")] string? apiKey = null)
  {
    return await nuGetService.UnlistPackageAsync(packageId, version, apiKey);
  }

}
