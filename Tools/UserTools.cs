using System.ComponentModel;
using ModelContextProtocol.Server;

[McpServerToolType]
public class UserTools
{
  [McpServerTool, Description("Queries all the packages for a given user.")]
  public static async Task<List<NuGetPackageInfo>> GetUserPackages(
    INuGetApiService nuGetService,
    [Description("The username to query for packages")] string username)
  {
    return await nuGetService.GetUserPackagesAsync(username);
  }
}