using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using System.IO.Compression;

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

  public async Task<ToolResponse<NuGetPackageInfo>> GetPackageInfoAsync(string packageId, string? version = null)
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

      return package == null
      ? ToolResponse<NuGetPackageInfo>.Failure($"Package {packageId} not found")
        : ToolResponse<NuGetPackageInfo>.Success(package);
    }
    catch (Exception ex)
    {
      var errorMessage = $"Error getting package info for {packageId}: {ex.Message}";
      _logger.LogError(ex, errorMessage);
      return ToolResponse<NuGetPackageInfo>.Failure(errorMessage);
    }
  }

  public async Task<ToolResponse<NuGetSearchResult>> SearchPackagesAsync(string query, int skip = 0, int take = 20)
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

      return result != null
        ? ToolResponse<NuGetSearchResult>.Success(result)
        : ToolResponse<NuGetSearchResult>.Failure("No results found");
    }
    catch (Exception ex)
    {
      // Log the error and return a failure response
      var errorMessage = $"Error searching packages with query '{query}': {ex.Message}";
      _logger.LogError(ex, errorMessage);
      return ToolResponse<NuGetSearchResult>.Failure(errorMessage);
    }
  }

  public async Task<ToolResponse<string>> PublishPackageAsync(string packageFilePath, string? apiKey = null)
  {
    string? result = null;
    try
    {
      if (string.IsNullOrEmpty(apiKey) && string.IsNullOrEmpty(_apiKey))
      {
        result = "API key is required for publishing packages";
        _logger.LogError(result);
        return ToolResponse<string>.Failure(result);
      }

      if (!File.Exists(packageFilePath))
      {
        result = $"Package file does not exist: {packageFilePath}";
        _logger.LogError(result);
        return ToolResponse<string>.Failure(result);
      }

      if (Path.GetExtension(packageFilePath).ToLowerInvariant() != ".nupkg")
      {
        result = $"Invalid package file extension: {Path.GetExtension(packageFilePath)}. Expected .nupkg";
        _logger.LogError(result);
        return ToolResponse<string>.Failure(result);
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
      result = $"Package {Path.GetFileName(packageFilePath)} published successfully";
      _logger.LogInformation(result);
      return ToolResponse<string>.Success(result);
    }
    catch (Exception ex)
    {
      result = $"Error publishing package {Path.GetFileName(packageFilePath)}: {ex.Message}";
      _logger.LogError(ex, result);
      return ToolResponse<string>.Failure(result);
    }
  }

  public async Task<ToolResponse<string>> PublishSymbolPackageAsync(string symbolPackagePath, string? apiKey = null)
  {
    string? result = null;
    try
    {
      if (string.IsNullOrEmpty(apiKey) && string.IsNullOrEmpty(_apiKey))
      {
        result = "API key is required for publishing symbol packages";
        _logger.LogError(result);
        return ToolResponse<string>.Failure(result);
      }

      if (!File.Exists(symbolPackagePath))
      {
        result = $"Symbol package file does not exist: {symbolPackagePath}";
        _logger.LogError(result);
        return ToolResponse<string>.Failure(result);
      }

      var extension = Path.GetExtension(symbolPackagePath).ToLowerInvariant();
      if (extension != ".snupkg" && extension != ".symbols.nupkg")
      {
        result = $"Invalid symbol package file extension: {extension}. Expected .snupkg or .symbols.nupkg";
        _logger.LogError(result);
        return ToolResponse<string>.Failure(result);
      }

      var packageUpdateResource = await _sourceRepository.GetResourceAsync<PackageUpdateResource>();

      await packageUpdateResource.PushAsync(
          packagePaths: new[] { symbolPackagePath }, // No regular package
          symbolSource: null, // NuGet symbol server
          timeoutInSecond: 5 * 60,
          disableBuffering: false,
          getApiKey: _ => apiKey ?? _apiKey,
          getSymbolApiKey: _ => apiKey ?? _apiKey,
          noServiceEndpoint: false,
          skipDuplicate: false,
          allowInsecureConnections: false,
          allowSnupkg: true,
          log: NuGet.Common.NullLogger.Instance);

      result = $"Symbol package {Path.GetFileName(symbolPackagePath)} published successfully";
      _logger.LogInformation(result);
      return ToolResponse<string>.Success(result);
    }
    catch (Exception ex)
    {
      result = $"Error publishing symbol package {Path.GetFileName(symbolPackagePath)}: {ex.Message}";
      _logger.LogError(ex, result);
      return ToolResponse<string>.Failure(result);
    }
  }

  public async Task<ToolResponse<string>> DeletePackageAsync(string packageId, string? apiKey = null)
  {
    string? result = null;
    if (string.IsNullOrEmpty(apiKey) && string.IsNullOrEmpty(_apiKey))
    {
      result = "API key is required for deleting packages";
      _logger.LogError(result);
      return ToolResponse<string>.Failure(result);
    }

    if (string.IsNullOrEmpty(packageId))
    {
      result = "Package ID is required for deletion";
      _logger.LogError(result);
      return ToolResponse<string>.Failure(result);
    }

    // Get package update resource (for deletion)
    var packageUpdateResource = await _sourceRepository.GetResourceAsync<PackageUpdateResource>();
    if (packageUpdateResource == null)
    {
      result = "Could not get package update resource from the source. This source may not support package deletion.";
      _logger.LogError(result);
      return ToolResponse<string>.Failure(result);
    }

    // Get package metadata resource
    var packageMetadataResource = await _sourceRepository.GetResourceAsync<PackageMetadataResource>();
    if (packageMetadataResource == null)
    {
      result = "Could not get package metadata resource from the source. This source may not support package metadata retrieval.";
      _logger.LogError(result);
      return ToolResponse<string>.Failure(result);
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
      return ToolResponse<string>.Failure(result);
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
      result = $"Deletion complete. Successfully deleted: {deletedCount}";
      _logger.LogInformation(result);
      return ToolResponse<string>.Success(result);
    }

    result = $"Deletion complete. Successfully deleted: {deletedCount}, Failed to delete: {failedCount}\n{result}";
    _logger.LogWarning(result);

    // Return partial success if some deletions failed
    return ToolResponse<string>.PartialSuccess(result);
  }

  public async Task<ToolResponse<string>> DeletePackageVersionAsync(string packageId, string version, string? apiKey = null)
  {
    string? result = null;
    if (string.IsNullOrEmpty(apiKey) && string.IsNullOrEmpty(_apiKey))
    {
      result = "API key is required for deleting package versions";
      _logger.LogError(result);
      return ToolResponse<string>.Failure(result);
    }

    if (string.IsNullOrEmpty(packageId) || string.IsNullOrEmpty(version))
    {
      result = "Package ID and version are required for deletion";
      _logger.LogError(result);
      return ToolResponse<string>.Failure(result);
    }

    var packageUpdateResource = await _sourceRepository.GetResourceAsync<PackageUpdateResource>();
    if (packageUpdateResource == null)
    {
      result = "Could not get package update resource from the source. This source may not support package deletion.";
      _logger.LogError(result);
      return ToolResponse<string>.Failure(result);
    }

    return await _DeletePackageVersion(packageUpdateResource, packageId, new NuGetVersion(version), apiKey);
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

  public async Task<ToolResponse<List<string>>> ListPackageFilesAsync(string packageId, string? version = null)
  {
    try
    {
      if (string.IsNullOrEmpty(packageId))
      {
        return ToolResponse<List<string>>.Failure("Package ID is required");
      }

      // Get package metadata to find the download URL
      var packageMetadataResource = await _sourceRepository.GetResourceAsync<PackageMetadataResource>();
      if (packageMetadataResource == null)
      {
        return ToolResponse<List<string>>.Failure("Could not get package metadata resource from the source");
      }

      var packageMetadata = await packageMetadataResource.GetMetadataAsync(
          packageId,
          includePrerelease: true,
          includeUnlisted: false,
          new SourceCacheContext(),
          NuGet.Common.NullLogger.Instance,
          CancellationToken.None);

      if (!packageMetadata.Any())
      {
        return ToolResponse<List<string>>.Failure($"Package {packageId} not found");
      }

      // Find the specific version or use the latest
      var targetPackage = version != null
          ? packageMetadata.FirstOrDefault(p => p.Identity.Version.ToString().Equals(version, StringComparison.OrdinalIgnoreCase))
          : packageMetadata.OrderByDescending(p => p.Identity.Version).First();

      if (targetPackage == null)
      {
        return ToolResponse<List<string>>.Failure($"Version {version} of package {packageId} not found");
      }

      // Get download resource
      var downloadResource = await _sourceRepository.GetResourceAsync<DownloadResource>();
      if (downloadResource == null)
      {
        return ToolResponse<List<string>>.Failure("Could not get download resource from the source");
      }

      // Create a temporary directory for extraction
      var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
      Directory.CreateDirectory(tempDir);

      try
      {
        // Download the package
        var downloadResult = await downloadResource.GetDownloadResourceResultAsync(
            targetPackage.Identity,
            new PackageDownloadContext(new SourceCacheContext()),
            tempDir,
            NuGet.Common.NullLogger.Instance,
            CancellationToken.None);

        if (downloadResult.Status != DownloadResourceResultStatus.Available || downloadResult.PackageStream == null)
        {
          return ToolResponse<List<string>>.Failure($"Failed to download package {packageId} {targetPackage.Identity.Version}");
        }

        var files = new List<string>();

        // Extract and list files using System.IO.Compression.ZipArchive
        using (var archive = new System.IO.Compression.ZipArchive(downloadResult.PackageStream, System.IO.Compression.ZipArchiveMode.Read))
        {
          foreach (var entry in archive.Entries)
          {
            if (!string.IsNullOrEmpty(entry.Name)) // Skip directories
            {
              files.Add(entry.FullName);
            }
          }
        }

        files.Sort(); // Sort files alphabetically
        _logger.LogInformation($"Listed {files.Count} files in package {packageId} {targetPackage.Identity.Version}");
        return ToolResponse<List<string>>.Success(files);
      }
      finally
      {
        // Clean up temporary directory
        if (Directory.Exists(tempDir))
        {
          try
          {
            Directory.Delete(tempDir, true);
          }
          catch (Exception cleanupEx)
          {
            _logger.LogWarning(cleanupEx, "Failed to clean up temporary directory {TempDir}", tempDir);
          }
        }
      }
    }
    catch (Exception ex)
    {
      var errorMessage = $"Error listing files for package {packageId}{(version != null ? $" version {version}" : "")}: {ex.Message}";
      _logger.LogError(ex, errorMessage);
      return ToolResponse<List<string>>.Failure(errorMessage);
    }
  }

  private async Task<ToolResponse<string>> _DeletePackageVersion(PackageUpdateResource packageUpdateResource, string packageId, NuGetVersion version, string? apiKey = null)
  {
    string? result = null;
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

      result = $"Deleted {packageId} {version}";
      _logger.LogInformation(result);
      return ToolResponse<string>.Success(result);
    }
    catch (Exception ex)
    {
      result = $"Failed to delete {packageId} {version}: {ex.Message}";
      _logger.LogError(ex, result);
      return ToolResponse<string>.Failure(result);
    }
  }
}
