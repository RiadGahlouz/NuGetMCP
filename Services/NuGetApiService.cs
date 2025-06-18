using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public class NuGetApiService
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

  public async Task<bool> PublishPackageAsync(byte[] packageData, string? apiKey = null)
  {
    try
    {
      if (string.IsNullOrEmpty(apiKey) && string.IsNullOrEmpty(_apiKey))
      {
        _logger.LogError("API key is required for publishing packages");
        return false;
      }

      var publishUrl = "https://www.nuget.org/api/v2/package";
      using var content = new MultipartFormDataContent();
      using var packageContent = new ByteArrayContent(packageData);
      packageContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
      content.Add(packageContent, "package", "package.nupkg");

      var request = new HttpRequestMessage(HttpMethod.Put, publishUrl)
      {
        Content = content
      };

      request.Headers.Add("X-NuGet-ApiKey", apiKey ?? _apiKey);

      var response = await _httpClient.SendAsync(request);
      return response.IsSuccessStatusCode;
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