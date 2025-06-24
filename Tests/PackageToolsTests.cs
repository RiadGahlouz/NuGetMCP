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
            var expectedResponse = ToolResponse<NuGetPackageInfo>.Success(expected);
            mockService.Setup(s => s.GetPackageInfoAsync(packageId, string.IsNullOrEmpty(version) ? null : version)).ReturnsAsync(expectedResponse);

            var result = await PackageTools.QueryPackage(mockService.Object, packageId, string.IsNullOrEmpty(version) ? null : version);
            Assert.Equal(expectedResponse, result);
        }

        [Fact]
        public async Task QueryPackage_ReturnsNull_WhenServiceReturnsNull()
        {
            var mockService = new Mock<INuGetApiService>();
            var failureResponse = ToolResponse<NuGetPackageInfo>.Failure("Package not found");
            mockService.Setup(s => s.GetPackageInfoAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(failureResponse);

            var result = await PackageTools.QueryPackage(mockService.Object, "pkg");
            Assert.Equal(ToolResponseResult.Failure, result.Result);
        }

        [Fact]
        public async Task SearchPackages_ReturnsSearchResult()
        {
            var mockService = new Mock<INuGetApiService>();
            var expected = new NuGetSearchResult { TotalHits = 1 };
            var expectedResponse = ToolResponse<NuGetSearchResult>.Success(expected);
            mockService.Setup(s => s.SearchPackagesAsync("query", 0, 20)).ReturnsAsync(expectedResponse);

            var result = await PackageTools.SearchPackages(mockService.Object, "query");
            Assert.Equal(expectedResponse, result);
        }

        [Fact]
        public async Task SearchPackages_ReturnsNull_WhenServiceReturnsNull()
        {
            var mockService = new Mock<INuGetApiService>();
            var failureResponse = ToolResponse<NuGetSearchResult>.Failure("Search failed");
            mockService.Setup(s => s.SearchPackagesAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(failureResponse);

            var result = await PackageTools.SearchPackages(mockService.Object, "query");
            Assert.Equal(ToolResponseResult.Failure, result.Result);
        }

        [Fact]
        public async Task PublishPackage_ReturnsTrue()
        {
            var mockService = new Mock<INuGetApiService>();
            mockService.Setup(s => s.PublishPackageAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(ToolResponse<string>.Success());
            var tempFile = Path.GetTempFileName();
            await File.WriteAllBytesAsync(tempFile, new byte[] { 1, 2, 3 });

            var result = await PackageTools.PublishPackage(mockService.Object, tempFile);
            Assert.Equal(ToolResponseResult.Success, result.Result);
            File.Delete(tempFile);
        }

        [Fact]
        public async Task PublishPackage_ReturnsFalse_WhenServiceReturnsFalse()
        {
            var mockService = new Mock<INuGetApiService>();
            mockService.Setup(s => s.PublishPackageAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(ToolResponse<string>.Failure("error"));
            var tempFile = Path.GetTempFileName();
            await File.WriteAllBytesAsync(tempFile, new byte[] { 1, 2, 3 });

            var result = await PackageTools.PublishPackage(mockService.Object, tempFile);
            Assert.Equal(ToolResponseResult.Failure, result.Result);
            File.Delete(tempFile);
        }

        [Fact]
        public async Task PublishPackage_ReturnsFailure_WhenFileDoesNotExist()
        {
            var mockService = new Mock<INuGetApiService>();
            var nonExistentFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".nupkg");
            mockService.Setup(s => s.PublishPackageAsync(nonExistentFile, It.IsAny<string>()))
                .ReturnsAsync(ToolResponse<string>.Failure("file not found"));
            var result = await PackageTools.PublishPackage(mockService.Object, nonExistentFile);
            Assert.Equal(ToolResponseResult.Failure, result.Result);
        }

        [Fact]
        public async Task PublishSymbolPackage_ReturnsSuccess()
        {
            var mockService = new Mock<INuGetApiService>();
            mockService.Setup(s => s.PublishSymbolPackageAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(ToolResponse<string>.Success());
            var tempFile = Path.GetTempFileName();
            var symbolFile = Path.ChangeExtension(tempFile, ".snupkg");
            File.Move(tempFile, symbolFile);
            await File.WriteAllBytesAsync(symbolFile, new byte[] { 1, 2, 3 });

            var result = await PackageTools.PublishSymbolPackage(mockService.Object, symbolFile);
            Assert.Equal(ToolResponseResult.Success, result.Result);
            File.Delete(symbolFile);
        }

        [Fact]
        public async Task PublishSymbolPackage_ReturnsFailure_WhenServiceReturnsFalse()
        {
            var mockService = new Mock<INuGetApiService>();
            mockService.Setup(s => s.PublishSymbolPackageAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(ToolResponse<string>.Failure("error"));
            var tempFile = Path.GetTempFileName();
            var symbolFile = Path.ChangeExtension(tempFile, ".snupkg");
            File.Move(tempFile, symbolFile);
            await File.WriteAllBytesAsync(symbolFile, new byte[] { 1, 2, 3 });

            var result = await PackageTools.PublishSymbolPackage(mockService.Object, symbolFile);
            Assert.Equal(ToolResponseResult.Failure, result.Result);
            File.Delete(symbolFile);
        }

        [Fact]
        public async Task UnlistPackage_ReturnsTrue()
        {
            var mockService = new Mock<INuGetApiService>();
            mockService.Setup(s => s.DeletePackageVersionAsync("pkg", "1.0.0", null))
                .ReturnsAsync(ToolResponse<string>.Success());

            var result = await PackageTools.DeletePackageVersion(mockService.Object, "pkg", "1.0.0");
            Assert.Equal(ToolResponseResult.Success, result.Result);
        }

        [Fact]
        public async Task UnlistPackage_ReturnsFalse_WhenServiceReturnsFalse()
        {
            var mockService = new Mock<INuGetApiService>();
            mockService.Setup(s => s.DeletePackageVersionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(ToolResponse<string>.Failure("error"));

            var result = await PackageTools.DeletePackageVersion(mockService.Object, "pkg", "1.0.0");
            Assert.Equal(ToolResponseResult.Failure, result.Result);
        }
    }
}
