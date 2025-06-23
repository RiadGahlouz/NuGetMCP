using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;

public class NuGetApiService : INuGetApiService
{
  private const string NUGET_SEARCH_URL = "https://azuresearch-usnc.nuget.org/query";
  private readonly HttpClient _httpClient;
  private readonly ILogger<NuGetApiService> _logger;
  private readonly string _apiKey;

  public NuGetApiService(IHttpClientFactory httpClientFactory, ILogger<NuGetApiService> logger, string? apiKey = null)
  {
    _httpClient = httpClientFactory.CreateClient();
    _logger = logger;
    _apiKey = apiKey ?? string.Empty;

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

      // Create source repository
      var source = new PackageSource("https://api.nuget.org/v3/index.json");
      var sourceRepository = new SourceRepository(source, Repository.Provider.GetCoreV3());

      var packageUpdateResource = await sourceRepository.GetResourceAsync<PackageUpdateResource>();

      await packageUpdateResource.PushAsync(
          new[] { packageFilePath },
          symbolSource: null,
          timeoutInSecond: 5 * 60,
          disableBuffering: false,
          getApiKey: _ => apiKey,
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

  public async Task<bool> UnlistPackageAsync(string packageId, string version, string? apiKey = null)
  {
    try
    {
      if (string.IsNullOrEmpty(apiKey) && string.IsNullOrEmpty(_apiKey))
      {
        _logger.LogError("API key is required for unlisting packages");
        return false;
      }

      var unlistUrl = $"https://www.nuget.org/api/v2/package/{packageId}/{version}";
      var request = new HttpRequestMessage(HttpMethod.Delete, unlistUrl);
      request.Headers.Add("X-NuGet-ApiKey", apiKey ?? _apiKey);

      var response = await _httpClient.SendAsync(request);
      return response.IsSuccessStatusCode;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error unlisting package {PackageId} version {Version}", packageId, version);
      return false;
    }
  }
}