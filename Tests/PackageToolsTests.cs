using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Moq;

namespace NuGetMCP.Tests
{
    public class PackageToolsTests : IDisposable
    {
        private readonly List<string> _tempFiles = new();
        private readonly Mock<INuGetApiService> _mockService = new();

        public void Dispose()
        {
            // Clean up any temporary files created during tests
            foreach (var file in _tempFiles)
            {
                if (File.Exists(file))
                {
                    try { File.Delete(file); } catch { /* Ignore cleanup errors */ }
                }
            }
        }

        private string CreateTempFile(string extension = ".tmp", byte[]? content = null)
        {
            var tempFile = Path.GetTempFileName();
            var targetFile = Path.ChangeExtension(tempFile, extension);
            
            if (targetFile != tempFile)
            {
                File.Move(tempFile, targetFile);
            }
            
            if (content != null)
            {
                File.WriteAllBytes(targetFile, content);
            }
            
            _tempFiles.Add(targetFile);
            return targetFile;
        }

        #region QueryPackage Tests

        [Theory]
        [InlineData("Newtonsoft.Json", null)]
        [InlineData("Newtonsoft.Json", "13.0.3")]
        [InlineData("Microsoft.Extensions.Logging", "8.0.0")]
        public async Task QueryPackage_WithValidInput_ReturnsExpectedPackageInfo(string packageId, string? version)
        {
            // Arrange
            var expectedPackage = TestDataBuilder.CreatePackageInfo(packageId, version ?? "1.0.0");
            var expectedResponse = ToolResponse<NuGetPackageInfo>.Success(expectedPackage);
            
            _mockService.Setup(s => s.GetPackageInfoAsync(packageId, version))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await PackageTools.QueryPackage(_mockService.Object, packageId, version);

            // Assert
            Assert.Equal(ToolResponseResult.Success, result.Result);
            Assert.Equal(expectedPackage.Id, result.Payload?.Id);
            Assert.Equal(expectedPackage.Version, result.Payload?.Version);
            _mockService.Verify(s => s.GetPackageInfoAsync(packageId, version), Times.Once);
        }

        [Fact]
        public async Task QueryPackage_WhenPackageNotFound_ReturnsFailureResponse()
        {
            // Arrange
            const string packageId = "NonExistentPackage";
            var failureResponse = ToolResponse<NuGetPackageInfo>.Failure("Package not found");
            
            _mockService.Setup(s => s.GetPackageInfoAsync(packageId, null))
                       .ReturnsAsync(failureResponse);

            // Act
            var result = await PackageTools.QueryPackage(_mockService.Object, packageId);

            // Assert
            Assert.Equal(ToolResponseResult.Failure, result.Result);
            Assert.Equal("Package not found", result.ErrorMessage);
            Assert.Null(result.Payload);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task QueryPackage_WithEmptyOrWhitespacePackageId_ShouldCallService(string packageId)
        {
            // Arrange
            var failureResponse = ToolResponse<NuGetPackageInfo>.Failure("Invalid package ID");
            _mockService.Setup(s => s.GetPackageInfoAsync(packageId, null))
                       .ReturnsAsync(failureResponse);

            // Act
            var result = await PackageTools.QueryPackage(_mockService.Object, packageId);

            // Assert
            Assert.Equal(ToolResponseResult.Failure, result.Result);
            _mockService.Verify(s => s.GetPackageInfoAsync(packageId, null), Times.Once);
        }

        #endregion

        #region SearchPackages Tests

        [Theory]
        [InlineData("logging", 0, 20)]
        [InlineData("json", 10, 50)]
        [InlineData("microsoft", 0, 100)]
        public async Task SearchPackages_WithValidParameters_ReturnsSearchResults(string query, int skip, int take)
        {
            // Arrange
            var searchResult = TestDataBuilder.CreateSearchResult(totalHits: 150, resultCount: take);
            var expectedResponse = ToolResponse<NuGetSearchResult>.Success(searchResult);
            
            _mockService.Setup(s => s.SearchPackagesAsync(query, skip, take))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await PackageTools.SearchPackages(_mockService.Object, query, skip, take);

            // Assert
            Assert.Equal(ToolResponseResult.Success, result.Result);
            Assert.Equal(searchResult.TotalHits, result.Payload?.TotalHits);
            Assert.Equal(take, result.Payload?.Data?.Length);
            _mockService.Verify(s => s.SearchPackagesAsync(query, skip, take), Times.Once);
        }

        [Fact]
        public async Task SearchPackages_WithDefaultParameters_UsesCorrectDefaults()
        {
            // Arrange
            const string query = "test";
            var searchResult = TestDataBuilder.CreateSearchResult();
            var expectedResponse = ToolResponse<NuGetSearchResult>.Success(searchResult);
            
            _mockService.Setup(s => s.SearchPackagesAsync(query, 0, 20))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await PackageTools.SearchPackages(_mockService.Object, query);

            // Assert
            Assert.Equal(ToolResponseResult.Success, result.Result);
            _mockService.Verify(s => s.SearchPackagesAsync(query, 0, 20), Times.Once);
        }

        [Fact]
        public async Task SearchPackages_WhenNoResults_ReturnsEmptyResult()
        {
            // Arrange
            const string query = "veryspecificquerywithnoResults";
            var emptyResult = TestDataBuilder.CreateSearchResult(totalHits: 0, resultCount: 0);
            var expectedResponse = ToolResponse<NuGetSearchResult>.Success(emptyResult);
            
            _mockService.Setup(s => s.SearchPackagesAsync(query, 0, 20))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await PackageTools.SearchPackages(_mockService.Object, query);

            // Assert
            Assert.Equal(ToolResponseResult.Success, result.Result);
            Assert.Equal(0, result.Payload?.TotalHits);
            Assert.Empty(result.Payload?.Data ?? Array.Empty<NuGetPackageInfo>());
        }

        [Fact]
        public async Task SearchPackages_WhenServiceFails_ReturnsFailureResponse()
        {
            // Arrange
            const string query = "test";
            var failureResponse = ToolResponse<NuGetSearchResult>.Failure("Search service unavailable");
            
            _mockService.Setup(s => s.SearchPackagesAsync(query, 0, 20))
                       .ReturnsAsync(failureResponse);

            // Act
            var result = await PackageTools.SearchPackages(_mockService.Object, query);

            // Assert
            Assert.Equal(ToolResponseResult.Failure, result.Result);
            Assert.Equal("Search service unavailable", result.ErrorMessage);
        }

        #endregion

        #region PublishPackage Tests

        [Fact]
        public async Task PublishPackage_WithValidPackage_ReturnsSuccess()
        {
            // Arrange
            var packageFile = CreateTempFile(".nupkg", new byte[] { 1, 2, 3, 4, 5 });
            const string apiKey = "test-api-key";
            var successResponse = ToolResponse<string>.Success("Package published successfully");
            
            _mockService.Setup(s => s.PublishPackageAsync(packageFile, apiKey))
                       .ReturnsAsync(successResponse);

            // Act
            var result = await PackageTools.PublishPackage(_mockService.Object, packageFile, apiKey);

            // Assert
            Assert.Equal(ToolResponseResult.Success, result.Result);
            Assert.Contains("published successfully", result.Payload);
            _mockService.Verify(s => s.PublishPackageAsync(packageFile, apiKey), Times.Once);
        }

        [Fact]
        public async Task PublishPackage_WithoutApiKey_CallsServiceWithNullApiKey()
        {
            // Arrange
            var packageFile = CreateTempFile(".nupkg", new byte[] { 1, 2, 3 });
            var successResponse = ToolResponse<string>.Success("Package published");
            
            _mockService.Setup(s => s.PublishPackageAsync(packageFile, null))
                       .ReturnsAsync(successResponse);

            // Act
            var result = await PackageTools.PublishPackage(_mockService.Object, packageFile);

            // Assert
            Assert.Equal(ToolResponseResult.Success, result.Result);
            _mockService.Verify(s => s.PublishPackageAsync(packageFile, null), Times.Once);
        }

        [Fact]
        public async Task PublishPackage_WhenServiceFails_ReturnsFailureResponse()
        {
            // Arrange
            var packageFile = CreateTempFile(".nupkg");
            var failureResponse = ToolResponse<string>.Failure("Invalid API key");
            
            _mockService.Setup(s => s.PublishPackageAsync(packageFile, It.IsAny<string>()))
                       .ReturnsAsync(failureResponse);

            // Act
            var result = await PackageTools.PublishPackage(_mockService.Object, packageFile, "invalid-key");

            // Assert
            Assert.Equal(ToolResponseResult.Failure, result.Result);
            Assert.Equal("Invalid API key", result.ErrorMessage);
        }

        #endregion

        #region PublishSymbolPackage Tests

        [Theory]
        [InlineData(".snupkg")]
        [InlineData(".symbols.nupkg")]
        public async Task PublishSymbolPackage_WithValidSymbolPackage_ReturnsSuccess(string extension)
        {
            // Arrange
            var symbolFile = CreateTempFile(extension, new byte[] { 1, 2, 3, 4, 5 });
            const string apiKey = "test-api-key";
            var successResponse = ToolResponse<string>.Success($"Symbol package published successfully");
            
            _mockService.Setup(s => s.PublishSymbolPackageAsync(symbolFile, apiKey))
                       .ReturnsAsync(successResponse);

            // Act
            var result = await PackageTools.PublishSymbolPackage(_mockService.Object, symbolFile, apiKey);

            // Assert
            Assert.Equal(ToolResponseResult.Success, result.Result);
            Assert.Contains("published successfully", result.Payload);
            _mockService.Verify(s => s.PublishSymbolPackageAsync(symbolFile, apiKey), Times.Once);
        }

        [Fact]
        public async Task PublishSymbolPackage_WhenServiceFails_ReturnsFailureResponse()
        {
            // Arrange
            var symbolFile = CreateTempFile(".snupkg");
            var failureResponse = ToolResponse<string>.Failure("Symbol server unavailable");
            
            _mockService.Setup(s => s.PublishSymbolPackageAsync(symbolFile, It.IsAny<string>()))
                       .ReturnsAsync(failureResponse);

            // Act
            var result = await PackageTools.PublishSymbolPackage(_mockService.Object, symbolFile);

            // Assert
            Assert.Equal(ToolResponseResult.Failure, result.Result);
            Assert.Equal("Symbol server unavailable", result.ErrorMessage);
        }

        #endregion

        #region DeletePackage Tests

        [Fact]
        public async Task DeletePackage_WithValidPackageId_ReturnsSuccess()
        {
            // Arrange
            const string packageId = "TestPackage";
            const string apiKey = "test-api-key";
            var successResponse = ToolResponse<string>.Success($"Package {packageId} deleted successfully");
            
            _mockService.Setup(s => s.DeletePackageAsync(packageId, apiKey))
                       .ReturnsAsync(successResponse);

            // Act
            var result = await PackageTools.DeletePackage(_mockService.Object, packageId, apiKey);

            // Assert
            Assert.Equal(ToolResponseResult.Success, result.Result);
            Assert.Contains("deleted successfully", result.Payload);
            _mockService.Verify(s => s.DeletePackageAsync(packageId, apiKey), Times.Once);
        }

        [Fact]
        public async Task DeletePackageVersion_WithValidInputs_ReturnsSuccess()
        {
            // Arrange
            const string packageId = "TestPackage";
            const string version = "1.0.0";
            const string apiKey = "test-api-key";
            var successResponse = ToolResponse<string>.Success($"Package {packageId} {version} deleted");
            
            _mockService.Setup(s => s.DeletePackageVersionAsync(packageId, version, apiKey))
                       .ReturnsAsync(successResponse);

            // Act
            var result = await PackageTools.DeletePackageVersion(_mockService.Object, packageId, version, apiKey);

            // Assert
            Assert.Equal(ToolResponseResult.Success, result.Result);
            Assert.Contains("deleted", result.Payload);
            _mockService.Verify(s => s.DeletePackageVersionAsync(packageId, version, apiKey), Times.Once);
        }

        [Fact]
        public async Task DeletePackageVersion_WhenUnauthorized_ReturnsFailureResponse()
        {
            // Arrange
            const string packageId = "TestPackage";
            const string version = "1.0.0";
            var failureResponse = ToolResponse<string>.Failure("Unauthorized to delete package");
            
            _mockService.Setup(s => s.DeletePackageVersionAsync(packageId, version, It.IsAny<string>()))
                       .ReturnsAsync(failureResponse);

            // Act
            var result = await PackageTools.DeletePackageVersion(_mockService.Object, packageId, version);

            // Assert
            Assert.Equal(ToolResponseResult.Failure, result.Result);
            Assert.Equal("Unauthorized to delete package", result.ErrorMessage);
        }

        #endregion

        #region ListPackageFiles Tests

        [Fact]
        public async Task ListPackageFiles_WithValidPackage_ReturnsFileList()
        {
            // Arrange
            const string packageId = "TestPackage";
            const string version = "1.0.0";
            var expectedFiles = new List<string>
            {
                "lib/net6.0/TestPackage.dll",
                "lib/net6.0/TestPackage.pdb",
                "TestPackage.nuspec",
                "readme.txt",
                "icon.png"
            };
            var successResponse = ToolResponse<List<string>>.Success(expectedFiles);
            
            _mockService.Setup(s => s.ListPackageFilesAsync(packageId, version))
                       .ReturnsAsync(successResponse);

            // Act
            var result = await PackageTools.ListPackageFiles(_mockService.Object, packageId, version);

            // Assert
            Assert.Equal(ToolResponseResult.Success, result.Result);
            Assert.Equal(expectedFiles.Count, result.Payload?.Count);
            Assert.Contains("lib/net6.0/TestPackage.dll", result.Payload ?? new List<string>());
            Assert.Contains("TestPackage.nuspec", result.Payload ?? new List<string>());
            _mockService.Verify(s => s.ListPackageFilesAsync(packageId, version), Times.Once);
        }

        [Fact]
        public async Task ListPackageFiles_WithoutVersion_UsesLatestVersion()
        {
            // Arrange
            const string packageId = "TestPackage";
            var expectedFiles = new List<string> { "lib/net6.0/TestPackage.dll" };
            var successResponse = ToolResponse<List<string>>.Success(expectedFiles);
            
            _mockService.Setup(s => s.ListPackageFilesAsync(packageId, null))
                       .ReturnsAsync(successResponse);

            // Act
            var result = await PackageTools.ListPackageFiles(_mockService.Object, packageId);

            // Assert
            Assert.Equal(ToolResponseResult.Success, result.Result);
            _mockService.Verify(s => s.ListPackageFilesAsync(packageId, null), Times.Once);
        }

        [Fact]
        public async Task ListPackageFiles_WhenPackageNotFound_ReturnsFailureResponse()
        {
            // Arrange
            const string packageId = "NonExistentPackage";
            var failureResponse = ToolResponse<List<string>>.Failure("Package not found");
            
            _mockService.Setup(s => s.ListPackageFilesAsync(packageId, It.IsAny<string>()))
                       .ReturnsAsync(failureResponse);

            // Act
            var result = await PackageTools.ListPackageFiles(_mockService.Object, packageId);

            // Assert
            Assert.Equal(ToolResponseResult.Failure, result.Result);
            Assert.Equal("Package not found", result.ErrorMessage);
            Assert.Null(result.Payload);
        }

        [Fact]
        public async Task ListPackageFiles_WithEmptyPackage_ReturnsEmptyList()
        {
            // Arrange
            const string packageId = "EmptyPackage";
            var emptyFiles = new List<string>();
            var successResponse = ToolResponse<List<string>>.Success(emptyFiles);
            
            _mockService.Setup(s => s.ListPackageFilesAsync(packageId, null))
                       .ReturnsAsync(successResponse);

            // Act
            var result = await PackageTools.ListPackageFiles(_mockService.Object, packageId);

            // Assert
            Assert.Equal(ToolResponseResult.Success, result.Result);
            Assert.Empty(result.Payload ?? new List<string>());
        }

        #endregion
    }

    /// <summary>
    /// Test data builder for creating consistent test objects
    /// </summary>
    public static class TestDataBuilder
    {
        public static NuGetPackageInfo CreatePackageInfo(string id = "TestPackage", string version = "1.0.0")
        {
            return new NuGetPackageInfo
            {
                Id = id,
                Version = version,
                Title = $"{id} - Test Package",
                Description = $"Test description for {id}",
                Authors = new[] { "Test Author" },
                Owners = new[] { "Test Owner" },
                Tags = new[] { "test", "sample" },
                TotalDownloads = 1000,
                Published = DateTime.UtcNow.AddDays(-30),
                Verified = true,
                ProjectUrl = $"https://github.com/test/{id.ToLower()}",
                LicenseUrl = "https://opensource.org/licenses/MIT",
                Versions = new List<NuGetPackageVersion>
                {
                    new NuGetPackageVersion { Version = version, Downloads = 500 }
                }
            };
        }

        public static NuGetSearchResult CreateSearchResult(int totalHits = 10, int resultCount = 10)
        {
            var packages = new List<NuGetPackageInfo>();
            for (int i = 0; i < resultCount; i++)
            {
                packages.Add(CreatePackageInfo($"Package{i}", $"1.{i}.0"));
            }

            return new NuGetSearchResult
            {
                TotalHits = totalHits,
                Data = packages.ToArray()
            };
        }
    }
}
