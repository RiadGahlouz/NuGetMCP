public class NuGetPackageInfo
{
  public string Id { get; set; } = string.Empty;
  public string Version { get; set; } = string.Empty;
  public string Title { get; set; } = string.Empty;
  public string Description { get; set; } = string.Empty;
  public string[] Authors { get; set; } = Array.Empty<string>();
  public string[] Owners { get; set; } = Array.Empty<string>();
  public long TotalDownloads { get; set; }
  public bool Verified { get; set; }
  public string[] Tags { get; set; } = Array.Empty<string>();
  public DateTime Published { get; set; }
  public string ProjectUrl { get; set; } = string.Empty;
  public string LicenseUrl { get; set; } = string.Empty;
  public string IconUrl { get; set; } = string.Empty;
  public List<NuGetPackageVersion> Versions { get; set; } = new List<NuGetPackageVersion>();
}