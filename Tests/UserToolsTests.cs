using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Moq;

namespace NuGetMCP.Tests
{
    public class UserToolsTests
    {
        private readonly Mock<INuGetApiService> _mockService = new();

        [Theory]
        [InlineData("microsoft")]
        [InlineData("newtonsoft")]
        [InlineData("testuser")]
        public async Task GetUserPackages_WithValidUsername_ReturnsPackageList(string username)
        {
            // Arrange
            var expectedPackages = new List<NuGetPackageInfo>
            {
                TestDataBuilder.CreatePackageInfo($"{username}.Package1", "1.0.0"),
                TestDataBuilder.CreatePackageInfo($"{username}.Package2", "2.0.0"),
                TestDataBuilder.CreatePackageInfo($"{username}.Package3", "1.5.0")
            };

            _mockService.Setup(s => s.GetUserPackagesAsync(username))
                       .ReturnsAsync(expectedPackages);

            // Act
            var result = await UserTools.GetUserPackages(_mockService.Object, username);

            // Assert
            Assert.Equal(expectedPackages.Count, result.Count);
            Assert.All(result, package => Assert.Contains(username, package.Id));
            _mockService.Verify(s => s.GetUserPackagesAsync(username), Times.Once);
        }

        [Fact]
        public async Task GetUserPackages_WithNonExistentUser_ReturnsEmptyList()
        {
            // Arrange
            const string username = "nonexistentuser";
            var emptyPackages = new List<NuGetPackageInfo>();

            _mockService.Setup(s => s.GetUserPackagesAsync(username))
                       .ReturnsAsync(emptyPackages);

            // Act
            var result = await UserTools.GetUserPackages(_mockService.Object, username);

            // Assert
            Assert.Empty(result);
            _mockService.Verify(s => s.GetUserPackagesAsync(username), Times.Once);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task GetUserPackages_WithEmptyOrWhitespaceUsername_ShouldCallService(string username)
        {
            // Arrange
            var emptyPackages = new List<NuGetPackageInfo>();
            _mockService.Setup(s => s.GetUserPackagesAsync(username))
                       .ReturnsAsync(emptyPackages);

            // Act
            var result = await UserTools.GetUserPackages(_mockService.Object, username);

            // Assert
            Assert.Empty(result);
            _mockService.Verify(s => s.GetUserPackagesAsync(username), Times.Once);
        }

        [Fact]
        public async Task GetUserPackages_WithManyPackages_ReturnsAllPackages()
        {
            // Arrange
            const string username = "prolificuser";
            var manyPackages = new List<NuGetPackageInfo>();
            
            // Create 50 packages to test handling of larger result sets
            for (int i = 0; i < 50; i++)
            {
                manyPackages.Add(TestDataBuilder.CreatePackageInfo($"{username}.Package{i:D2}", $"{i / 10 + 1}.{i % 10}.0"));
            }

            _mockService.Setup(s => s.GetUserPackagesAsync(username))
                       .ReturnsAsync(manyPackages);

            // Act
            var result = await UserTools.GetUserPackages(_mockService.Object, username);

            // Assert
            Assert.Equal(50, result.Count);
            Assert.All(result, package => Assert.Contains(username, package.Id));
            _mockService.Verify(s => s.GetUserPackagesAsync(username), Times.Once);
        }

        [Fact]
        public async Task GetUserPackages_VerifyServiceMethodCalled()
        {
            // Arrange
            const string username = "testuser";
            var packages = new List<NuGetPackageInfo>();
            
            _mockService.Setup(s => s.GetUserPackagesAsync(It.IsAny<string>()))
                       .ReturnsAsync(packages);

            // Act
            await UserTools.GetUserPackages(_mockService.Object, username);

            // Assert
            _mockService.Verify(s => s.GetUserPackagesAsync(username), Times.Once);
            _mockService.VerifyNoOtherCalls();
        }
    }
}
