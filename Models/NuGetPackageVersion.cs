using System.Text.Json.Serialization;

public class NuGetPackageVersion
{
    public string Version { get; set; } = string.Empty;
    public long Downloads { get; set; }
    [JsonPropertyName("@id")]
    public string RegistrationUrl { get; set; }
}