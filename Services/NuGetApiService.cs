using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

public class NuGetApiService : INuGetApiService
{
  private const string NUGET_SEARCH_URL = "https://azuresearch-usnc.nuget.org/query";
  private const string NUGET_INDEX_URL = "https://api.nuget.org/v3/index.json";

  private readonly HttpClient _httpClient;
  private readonly SourceRepository _sourceRepository;
  private readonly ILogger<NuGetApiService> _logger;
  private readonly string _apiKey;

  public NuGetApiService(IHttpClientFactory httpClientFactory, ILogger<NuGetApiService> logger, string? apiKey = null)
  {
    _httpClient = httpClientFactory.CreateClient();
    _logger = logger;
    _apiKey = apiKey ?? string.Empty;

    // Create source repository
    var source = new PackageSource(NUGET_INDEX_URL);
    _sourceRepository = new SourceRepository(source, Repository.Provider.GetCoreV3());


    _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("NuGetMCP", "1.0"));

    if (!string.IsNullOrEmpty(_apiKey))
    {
      _httpClient.DefaultRequestHeaders.Add("X-NuGet-ApiKey", _apiKey);
    }
  }

  public async Task<NuGetPackageInfo?> GetPackageInfoAsync(string packageId, string? version = null)
  {
    try
    {
      var searchUrl = $"{NUGET_SEARCH_URL}?q=packageid:{packageId}&prerelease=true";
      var response = await _httpClient.GetAsync(searchUrl);
      response.EnsureSuccessStatusCode();

      var content = await response.Content.ReadAsStringAsync();
      var searchResult = JsonSerializer.Deserialize<NuGetSearchResult>(content, new JsonSerializerOptions
      {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
      });

      var package = searchResult?.Data?.FirstOrDefault(p =>
          string.Equals(p.Id, packageId, StringComparison.OrdinalIgnoreCase));

      return package;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting package info for {PackageId}", packageId);
      return null;
    }
  }

  public async Task<NuGetSearchResult?> SearchPackagesAsync(string query, int skip = 0, int take = 20)
  {
    try
    {
      var searchUrl = $"{NUGET_SEARCH_URL}?q={Uri.EscapeDataString(query)}&skip={skip}&take={take}&prerelease=true";
      var response = await _httpClient.GetAsync(searchUrl);
      response.EnsureSuccessStatusCode();

      var content = await response.Content.ReadAsStringAsync();
      var result = JsonSerializer.Deserialize<NuGetSearchResult>(content, new JsonSerializerOptions
      {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
      });

      return result;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error searching packages with query {Query}", query);
      return null;
    }
  }

  public async Task<bool> PublishPackageAsync(string packageFilePath, string? apiKey = null)
  {
    try
    {
      if (string.IsNullOrEmpty(apiKey) && string.IsNullOrEmpty(_apiKey))
      {
        _logger.LogError("API key is required for publishing packages");
        return false;
      }

      if (!File.Exists(packageFilePath))
      {
        _logger.LogError("Package file does not exist: {PackageFilePath}", packageFilePath);
        return false;
      }

      if (Path.GetExtension(packageFilePath).ToLowerInvariant() != ".nupkg")
      {
        _logger.LogError("Invalid package file extension: {PackageFilePath}", packageFilePath);
        return false;
      }

      var packageUpdateResource = await _sourceRepository.GetResourceAsync<PackageUpdateResource>();

      await packageUpdateResource.PushAsync(
          new[] { packageFilePath },
          symbolSource: null,
          timeoutInSecond: 5 * 60,
          disableBuffering: false,
          getApiKey: _ => apiKey ?? _apiKey,
          getSymbolApiKey: _ => null,
          noServiceEndpoint: false,
          skipDuplicate: false,
          allowInsecureConnections: false,
          allowSnupkg: false,
          log: NuGet.Common.NullLogger.Instance);
      _logger.LogInformation("Package {PackageFilePath} published successfully", packageFilePath);
      return true;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error publishing package");
      return false;
    }
  }

  public async Task<string?> DeletePackageAsync(string packageId, string? apiKey = null)
  {
    string? result = null;
    if (string.IsNullOrEmpty(apiKey) && string.IsNullOrEmpty(_apiKey))
    {
      result = "API key is required for deleting packages";
      _logger.LogError(result);
      return result;
    }

    if (string.IsNullOrEmpty(packageId))
    {
      result = "Package ID is required for deletion";
      _logger.LogError(result);
      return result;
    }

    // Get package update resource (for deletion)
    var packageUpdateResource = await _sourceRepository.GetResourceAsync<PackageUpdateResource>();
    if (packageUpdateResource == null)
    {
      result = "Could not get package update resource from the source. This source may not support package deletion.";
      _logger.LogError(result);
      return result;
    }

    // Get package metadata resource
    var packageMetadataResource = await _sourceRepository.GetResourceAsync<PackageMetadataResource>();
    if (packageMetadataResource == null)
    {
      result = "Could not get package metadata resource from the source. This source may not support package metadata retrieval.";
      _logger.LogError(result);
      return result;
    }

    // Get all versions of the package
    _logger.LogInformation($"Fetching all versions of package {packageId} for deletion...");

    var packageMetadata = await packageMetadataResource.GetMetadataAsync(
        packageId,
        includePrerelease: true,
        includeUnlisted: true,
        new SourceCacheContext(),
        NuGet.Common.NullLogger.Instance,
        CancellationToken.None);

    if (!packageMetadata.Any())
    {
      result = $"No versions found for package {packageId}";
      _logger.LogError(result);
      return result;
    }

    var versions = packageMetadata.Select(p => p.Identity.Version).OrderBy(v => v).ToList();
    _logger.LogInformation($"Found {versions.Count} versions:");
    foreach (var version in versions)
    {
      _logger.LogInformation($"  - {version}");
    }

    // Delete each version
    _logger.LogInformation("\nStarting deletion...");
    int deletedCount = 0;
    int failedCount = 0;

    result = "";
    foreach (var version in versions)
    {
      var deletionResult = await _DeletePackageVersion(packageUpdateResource,
       packageId, version, apiKey);
      if (deletionResult == null)
      {
        deletedCount++;
      }
      else
      {
        result += deletionResult + "\n";
        failedCount++;
      }
    }

    // No failed deletions, log success message
    if (deletedCount > 0 && failedCount == 0)
    {
      _logger.LogInformation($"Deletion complete. Successfully deleted: {deletedCount}");
      return null; // Return null to indicate success
    }
    // If all versions were deleted, return a null string to indicate success
    return result;
  }

  public async Task<bool> DeletePackageVersionAsync(string packageId, string version, string? apiKey = null)
  {
    if (string.IsNullOrEmpty(apiKey) && string.IsNullOrEmpty(_apiKey))
    {
      _logger.LogError("API key is required for deleting package versions");
      return false;
    }

    if (string.IsNullOrEmpty(packageId) || string.IsNullOrEmpty(version))
    {
      _logger.LogError("Package ID and version are required for deletion");
      return false;
    }

    var packageUpdateResource = await _sourceRepository.GetResourceAsync<PackageUpdateResource>();
    if (packageUpdateResource == null)
    {
      _logger.LogError("Could not get package update resource from the source. This source may not support package deletion.");
      return false;
    }

    var deletionResult = await _DeletePackageVersion(packageUpdateResource, packageId, new NuGetVersion(version), apiKey);
    return deletionResult == null; // Return true if deletion was successful (null result means no error)
  }

  public async Task<List<NuGetPackageInfo>> GetUserPackagesAsync(string username)
  {
    var packages = new List<NuGetPackageInfo>();
    int skip = 0;
    const int take = 100; // NuGet API max per request
    bool hasMoreResults = true;

    while (hasMoreResults)
    {
      string url = $"{NUGET_SEARCH_URL}?q=owner:{username}&skip={skip}&take={take}&prerelease=true";

      try
      {
        var response = await _httpClient.GetStringAsync(url);
        var searchResult = JsonSerializer.Deserialize<NuGetSearchResult>(response, new JsonSerializerOptions
        {
          PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        if (searchResult?.Data == null || searchResult.Data.Length == 0)
        {
          hasMoreResults = false;
          break;
        }

        foreach (var item in searchResult.Data)
        {
          // Additional filtering to ensure the package is actually owned by the user
          if (item.Authors?.Any(a => a.Equals(username, StringComparison.OrdinalIgnoreCase)) == true ||
              item.Owners?.Any(o => o.Equals(username, StringComparison.OrdinalIgnoreCase)) == true)
          {
            packages.Add(new NuGetPackageInfo
            {
              Id = item.Id,
              Title = item.Title,
              Description = item.Description,
              Version = item.Version,
              Authors = item.Authors ?? Array.Empty<string>(),
              Owners = item.Owners ?? Array.Empty<string>(),
              Tags = item.Tags,
              ProjectUrl = item.ProjectUrl,
              LicenseUrl = item.LicenseUrl,
              IconUrl = item.IconUrl,
              Versions = item.Versions,
              TotalDownloads = item.TotalDownloads,
              Published = item.Published,
              Verified = item.Verified
            });
          }
        }

        skip += take;

        // Check if we've retrieved all available results
        if (searchResult.Data.Length < take)
        {
          hasMoreResults = false;
        }
      }
      catch (HttpRequestException ex)
      {
        _logger.LogError(ex, "Error retrieving packages for user {Username}", username);
        hasMoreResults = false; // Stop on error
      }
    }
    return packages;
  }

  private async Task<string?> _DeletePackageVersion(PackageUpdateResource packageUpdateResource, string packageId, NuGetVersion version, string? apiKey = null)
  {
    try
    {
      _logger.LogInformation($"Deleting {packageId} {version}... ");

      await packageUpdateResource.Delete(
          packageId,
          version.ToString(),
          endpoint => apiKey ?? _apiKey,
          confirm => true,
          false,
          NuGet.Common.NullLogger.Instance);

      _logger.LogInformation("✓ Success");
      return null; // Return null to indicate success
    }
    catch (Exception ex)
    {
      var result = $"Failed to delete {packageId} {version}: {ex.Message}";
      _logger.LogError(ex, result);
      return result;
    }
  }
}
