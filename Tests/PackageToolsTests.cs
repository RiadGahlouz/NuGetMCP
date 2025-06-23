using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Moq;

namespace NuGetMCP.Tests
{
    public class PackageToolsTests
    {
        [Theory]
        [InlineData("pkg", "")]
        [InlineData("pkg", "1.0.0")]
        public async Task QueryPackage_ReturnsPackageInfo(string packageId, string version)
        {
            var mockService = new Mock<INuGetApiService>();
            var expected = new NuGetPackageInfo { Id = packageId, Version = version ?? string.Empty };
            mockService.Setup(s => s.GetPackageInfoAsync(packageId, string.IsNullOrEmpty(version) ? null : version)).ReturnsAsync(expected);

            var result = await PackageTools.QueryPackage(mockService.Object, packageId, string.IsNullOrEmpty(version) ? null : version);
            Assert.Equal(expected, result);
        }

        [Fact]
        public async Task QueryPackage_ReturnsNull_WhenServiceReturnsNull()
        {
            var mockService = new Mock<INuGetApiService>();
            mockService.Setup(s => s.GetPackageInfoAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync((NuGetPackageInfo?)null);

            var result = await PackageTools.QueryPackage(mockService.Object, "pkg");
            Assert.Null(result);
        }

        [Fact]
        public async Task SearchPackages_ReturnsSearchResult()
        {
            var mockService = new Mock<INuGetApiService>();
            var expected = new NuGetSearchResult { TotalHits = 1 };
            mockService.Setup(s => s.SearchPackagesAsync("query", 0, 20)).ReturnsAsync(expected);

            var result = await PackageTools.SearchPackages(mockService.Object, "query");
            Assert.Equal(expected, result);
        }

        [Fact]
        public async Task SearchPackages_ReturnsNull_WhenServiceReturnsNull()
        {
            var mockService = new Mock<INuGetApiService>();
            mockService.Setup(s => s.SearchPackagesAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync((NuGetSearchResult?)null);

            var result = await PackageTools.SearchPackages(mockService.Object, "query");
            Assert.Null(result);
        }

        [Fact]
        public async Task PublishPackage_ReturnsTrue()
        {
            var mockService = new Mock<INuGetApiService>();
            mockService.Setup(s => s.PublishPackageAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
            var tempFile = Path.GetTempFileName();
            await File.WriteAllBytesAsync(tempFile, new byte[] { 1, 2, 3 });

            var result = await PackageTools.PublishPackage(mockService.Object, tempFile);
            Assert.True(result);
            File.Delete(tempFile);
        }

        [Fact]
        public async Task PublishPackage_ReturnsFalse_WhenServiceReturnsFalse()
        {
            var mockService = new Mock<INuGetApiService>();
            mockService.Setup(s => s.PublishPackageAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);
            var tempFile = Path.GetTempFileName();
            await File.WriteAllBytesAsync(tempFile, new byte[] { 1, 2, 3 });

            var result = await PackageTools.PublishPackage(mockService.Object, tempFile);
            Assert.False(result);
            File.Delete(tempFile);
        }

        [Fact]
        public async Task PublishPackage_ThrowsFileNotFoundException_WhenFileDoesNotExist()
        {
            var mockService = new Mock<INuGetApiService>();
            var nonExistentFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".nupkg");
            var result = await PackageTools.PublishPackage(mockService.Object, nonExistentFile);
            Assert.False(result);
        }

        [Fact]
        public async Task UnlistPackage_ReturnsTrue()
        {
            var mockService = new Mock<INuGetApiService>();
            mockService.Setup(s => s.DeletePackageVersionAsync("pkg", "1.0.0", null)).ReturnsAsync(true);

            var result = await PackageTools.DeletePackageVersion(mockService.Object, "pkg", "1.0.0");
            Assert.True(result);
        }

        [Fact]
        public async Task UnlistPackage_ReturnsFalse_WhenServiceReturnsFalse()
        {
            var mockService = new Mock<INuGetApiService>();
            mockService.Setup(s => s.DeletePackageVersionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);

            var result = await PackageTools.DeletePackageVersion(mockService.Object, "pkg", "1.0.0");
            Assert.False(result);
        }
    }
}
