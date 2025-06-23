using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Moq;

namespace NuGetMCP.Tests
{
    public class UserToolsTests
    {
        [Fact]
        public async Task GetUserPackages_ReturnsPackageList()
        {
            var mockService = new Mock<INuGetApiService>();
            var expected = new List<NuGetPackageInfo> { new NuGetPackageInfo { Id = "pkg", Version = "1.0.0" } };
            mockService.Setup(s => s.GetUserPackagesAsync("user")).ReturnsAsync(expected);

            var result = await UserTools.GetUserPackages(mockService.Object, "user");
            Assert.Equal(expected, result);
        }

        [Fact]
        public async Task GetUserPackages_ReturnsEmptyList_WhenNoPackages()
        {
            var mockService = new Mock<INuGetApiService>();
            mockService.Setup(s => s.GetUserPackagesAsync("user")).ReturnsAsync(new List<NuGetPackageInfo>());

            var result = await UserTools.GetUserPackages(mockService.Object, "user");
            Assert.Empty(result);
        }
    }
}
