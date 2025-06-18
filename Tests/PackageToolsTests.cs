using System.Threading.Tasks;
using Xunit;
using Moq;
using System.IO;

namespace NuGetMCP.Tests
{
    public class PackageToolsTests
    {
        [Fact]
        public async Task QueryPackage_ReturnsPackageInfo()
        {
            var mockService = new Mock<INuGetApiService>();
            var expected = new NuGetPackageInfo();
            mockService.Setup(s => s.GetPackageInfoAsync("pkg", null)).ReturnsAsync(expected);

            var result = await PackageTools.QueryPackage(mockService.Object, "pkg");
            Assert.Equal(expected, result);
        }

        [Fact]
        public async Task SearchPackages_ReturnsSearchResult()
        {
            var mockService = new Mock<INuGetApiService>();
            var expected = new NuGetSearchResult();
            mockService.Setup(s => s.SearchPackagesAsync("query", 0, 20)).ReturnsAsync(expected);

            var result = await PackageTools.SearchPackages(mockService.Object, "query");
            Assert.Equal(expected, result);
        }

        [Fact]
        public async Task PublishPackage_ReturnsTrue()
        {
            var mockService = new Mock<INuGetApiService>();
            mockService.Setup(s => s.PublishPackageAsync(It.IsAny<byte[]>(), null)).ReturnsAsync(true);
            var tempFile = Path.GetTempFileName();
            await File.WriteAllBytesAsync(tempFile, new byte[] { 1, 2, 3 });

            var result = await PackageTools.PublishPackage(mockService.Object, tempFile);
            Assert.True(result);
        }

        [Fact]
        public async Task UnlistPackage_ReturnsTrue()
        {
            var mockService = new Mock<INuGetApiService>();
            mockService.Setup(s => s.UnlistPackageAsync("pkg", "1.0.0", null)).ReturnsAsync(true);

            var result = await PackageTools.UnlistPackage(mockService.Object, "pkg", "1.0.0");
            Assert.True(result);
        }
    }
}
