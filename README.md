# NuGet MCP Server

A Model Context Protocol (MCP) server that provides NuGet package management capabilities through a standardized interface.

## Features

- **Get Package Information**: Retrieve detailed information about specific NuGet packages
- **Search Packages**: Search the NuGet repository with query terms
- **Multiple Package Search**: Get information for multiple packages by their IDs
- **Package Versions**: List all available versions of a package
- **Publish Packages**: Upload new packages to NuGet (requires API key)
- **Manage Packages**: Unlist packages from NuGet (requires API key)

## Setup
### Usage with VSCode
Use one of the methods below. Once you complete that flow, toggle Agent mode (located by the Copilot Chat text input) and the server will start.

#### Running from Source
1. Clone the project:
   ```bash
   git clone https://github.com/RiadGahlouz/NuGetMCP.git
   ```

1. (Optional) Set your NuGet API key as an environment variable for publishing:
   ```bash
   export NUGET_API_KEY=your_api_key_here
   ```

3. Setup your MCP host (e.g: through .`vscode/mcp.json`):
```json
  { 
    "inputs": [
    {
      "type": "promptString",
      "id": "nuget_api_key",
      "description": "NuGet API Key",
      "password": true
    }],
    "servers": {
      "NuGetMCP": {
        "type": "stdio",
        "command": "dotnet",
        "args": [
          "run",
          "--project",
          "PATH/TO/THE/PROJECT/NuGetMCP/NuGetMCP.csproj"],
        "env": {
          "NUGET_API_KEY": "${input:nuget_api_key}"
        }
      }
    }
  }
```
### Running as a docker container
Work in progress..

## Available Tools

### QueryPackage
Get detailed information about a specific NuGet package.

**Parameters:**
- `packageId` (required): The package ID to get information for
- `version` (optional): Specific version to get info for

### SearchPackages
Search for NuGet packages using a query string.

**Parameters:**
- `query` (required): Search query
- `skip` (optional): Number of results to skip (default: 0)
- `take` (optional): Number of results to return (default: 20, max: 100)

### PublishPackage
Publish a NuGet package (requires API key).

**Parameters:**
- `packageFilePath` (required): Path to the .nupkg file to publish
- `apiKey` (required): NuGet API key for publishing

### UnlistPackage
Unlist a specific version of a NuGet package (requires API key).

**Parameters:**
- `packageId` (required): The package ID to unlist
- `version` (required): The version to unlist
- `apiKey` (required): NuGet API key for package management

## Configuration

The server can be configured through environment variables:

- `NUGET_API_KEY`: Your NuGet API key for publishing and managing packages
- `ASPNETCORE_ENVIRONMENT`: Set to `Development` for development mode

## Requirements

- .NET 8.0 or later
- Internet connection for NuGet API access
- Valid NuGet API key for publishing/managing packages

## Notes

- The server uses the official NuGet API endpoints
- Search functionality uses the Azure Search service provided by NuGet
- Publishing and unlisting operations require a valid NuGet API key
- All operations are performed asynchronously for better performance

## Future Development
More toolsets will be added in the near future. Some ideas:
- [ ] User/Organisation metadata retrieval
- [ ] Package deletion
- [ ] Symbol package publishing
- [ ] List files inside a package
- [ ] Get a specific file inside a package
- [ ] Get Package README