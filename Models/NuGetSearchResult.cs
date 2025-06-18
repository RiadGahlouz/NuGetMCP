public class NuGetSearchResult
{
    public int TotalHits { get; set; }
    public NuGetPackageInfo[] Data { get; set; } = Array.Empty<NuGetPackageInfo>();
}