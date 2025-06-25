using System.ComponentModel;
using ModelContextProtocol.Server;

[McpServerToolType]
public static class PackageTools
{
  [McpServerTool, Description("Queries the package with the given ID.")]
  public static async Task<ToolResponse<NuGetPackageInfo>> QueryPackage(
    INuGetApiService nuGetService,
    [Description("The ID of the package to query for")] string packageId,
    [Description("Optional version of the package to query for")] string? version = null)
  {
    return await nuGetService.GetPackageInfoAsync(packageId, version);
  }

  [McpServerTool, Description("Searches for packages matching the given query.")]
  public static async Task<ToolResponse<NuGetSearchResult>> SearchPackages(
    INuGetApiService nuGetService,
    [Description("The search query to use")] string query,
    [Description("The number of results to skip (for pagination)")] int skip = 0,
    [Description("The number of results to take (for pagination)")] int take = 20)
  {
    return await nuGetService.SearchPackagesAsync(query, skip, take);
  }

  [McpServerTool, Description("Publishes a package to the NuGet repository.")]
  public static async Task<ToolResponse<string>> PublishPackage(
    INuGetApiService nuGetService,
    [Description("The path to the package to publish")] string packageFilePath,
    [Description("Optional API key for publishing")] string? apiKey = null)
  {
    return await nuGetService.PublishPackageAsync(packageFilePath, apiKey);
  }

  [McpServerTool, Description("Publishes a symbol package to the NuGet symbol server.")]
  public static async Task<ToolResponse<string>> PublishSymbolPackage(
    INuGetApiService nuGetService,
    [Description("The path to the symbol package to publish (.snupkg or .symbols.nupkg)")] string symbolPackagePath,
    [Description("Optional API key for publishing symbols")] string? apiKey = null)
  {
    return await nuGetService.PublishSymbolPackageAsync(symbolPackagePath, apiKey);
  }

  [McpServerTool, Description("Deletes all version of a package from the NuGet repository.")]
  public static async Task<ToolResponse<string>> DeletePackage(
    INuGetApiService nuGetService,
    [Description("The ID of the package to unlist")] string packageId,
    [Description("Optional API key for unlisting")] string? apiKey = null)
  {
    return await nuGetService.DeletePackageAsync(packageId, apiKey);
  }

  [McpServerTool, Description("Deletes a specific version of a package from the NuGet repository.")]
  public static async Task<ToolResponse<string>> DeletePackageVersion(
    INuGetApiService nuGetService,
    [Description("The ID of the package to delete")] string packageId,
    [Description("The version of the package to delete")] string version,
    [Description("Optional API key for deleting the package version")] string? apiKey = null)
  {
    return await nuGetService.DeletePackageVersionAsync(packageId, version, apiKey);
  }

  [McpServerTool, Description("Lists all files contained in a NuGet package.")]
  public static async Task<ToolResponse<List<string>>> ListPackageFiles(
    INuGetApiService nuGetService,
    [Description("The ID of the package to list files for")] string packageId,
    [Description("Optional specific version to list files for (defaults to latest)")] string? version = null)
  {
    return await nuGetService.ListPackageFilesAsync(packageId, version);
  }

}
