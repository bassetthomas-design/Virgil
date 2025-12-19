using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace Virgil.Tests;

public class CatalogBrowserJsonSchemaTests
{
    private static string FindBrowsersCatalogPath()
    {
        var baseDir = AppContext.BaseDirectory;
        var currentDir = new DirectoryInfo(baseDir);
        
        // Search upwards for the project root (where .sln file exists)
        while (currentDir != null && !File.Exists(Path.Combine(currentDir.FullName, "Virgil.sln")))
        {
            currentDir = currentDir.Parent;
        }
        
        if (currentDir == null)
        {
            throw new InvalidOperationException($"Could not find project root from: {baseDir}");
        }
        
        return Path.Combine(currentDir.FullName, "docs", "spec", "capabilities", "catalog", "browsers.json");
    }
    
    private static JsonDocument LoadBrowsersCatalog()
    {
        var fullPath = FindBrowsersCatalogPath();
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"browsers.json not found at: {fullPath}");
        }
        
        var json = File.ReadAllText(fullPath);
        return JsonDocument.Parse(json);
    }

    [Fact]
    public void BrowsersCatalog_ShouldExist()
    {
        // Arrange & Act
        var fullPath = FindBrowsersCatalogPath();
        
        // Assert
        Assert.True(File.Exists(fullPath), $"browsers.json should exist at: {fullPath}");
    }

    [Fact]
    public void BrowsersCatalog_ShouldBeValidJson()
    {
        // Arrange & Act
        var exception = Record.Exception(() => LoadBrowsersCatalog());
        
        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void BrowsersCatalog_ShouldHaveRequiredTopLevelFields()
    {
        // Arrange
        using var doc = LoadBrowsersCatalog();
        var root = doc.RootElement;
        
        // Act & Assert
        Assert.True(root.TryGetProperty("name", out _), "Missing 'name' field");
        Assert.True(root.TryGetProperty("version", out _), "Missing 'version' field");
        Assert.True(root.TryGetProperty("generatedAt", out _), "Missing 'generatedAt' field");
        Assert.True(root.TryGetProperty("capabilitySchemaVersion", out _), "Missing 'capabilitySchemaVersion' field");
        Assert.True(root.TryGetProperty("domain", out _), "Missing 'domain' field");
        Assert.True(root.TryGetProperty("description", out _), "Missing 'description' field");
        Assert.True(root.TryGetProperty("capabilities", out _), "Missing 'capabilities' field");
    }

    [Fact]
    public void BrowsersCatalog_Capabilities_ShouldBeArray()
    {
        // Arrange
        using var doc = LoadBrowsersCatalog();
        var root = doc.RootElement;
        
        // Act
        var capabilities = root.GetProperty("capabilities");
        
        // Assert
        Assert.Equal(JsonValueKind.Array, capabilities.ValueKind);
    }

    [Fact]
    public void BrowsersCatalog_Capabilities_ShouldNotBeEmpty()
    {
        // Arrange
        using var doc = LoadBrowsersCatalog();
        var root = doc.RootElement;
        
        // Act
        var capabilities = root.GetProperty("capabilities");
        var count = capabilities.GetArrayLength();
        
        // Assert
        Assert.True(count > 0, "Capabilities array should not be empty");
    }

    [Fact]
    public void BrowsersCatalog_AllCapabilities_ShouldHaveRequiredFields()
    {
        // Arrange
        using var doc = LoadBrowsersCatalog();
        var root = doc.RootElement;
        var capabilities = root.GetProperty("capabilities");
        
        // Act & Assert
        foreach (var capability in capabilities.EnumerateArray())
        {
            var id = capability.TryGetProperty("id", out var idProp) ? idProp.GetString() : "unknown";
            
            Assert.True(capability.TryGetProperty("id", out _), $"Capability missing 'id' field");
            Assert.True(capability.TryGetProperty("title", out _), $"Capability '{id}' missing 'title' field");
            Assert.True(capability.TryGetProperty("description", out _), $"Capability '{id}' missing 'description' field");
            Assert.True(capability.TryGetProperty("level", out _), $"Capability '{id}' missing 'level' field");
            Assert.True(capability.TryGetProperty("domain", out _), $"Capability '{id}' missing 'domain' field");
            Assert.True(capability.TryGetProperty("requiresAdmin", out _), $"Capability '{id}' missing 'requiresAdmin' field");
            Assert.True(capability.TryGetProperty("supportsDryRun", out _), $"Capability '{id}' missing 'supportsDryRun' field");
            Assert.True(capability.TryGetProperty("rollback", out _), $"Capability '{id}' missing 'rollback' field");
            Assert.True(capability.TryGetProperty("risk", out _), $"Capability '{id}' missing 'risk' field");
            Assert.True(capability.TryGetProperty("paramsSchema", out _), $"Capability '{id}' missing 'paramsSchema' field");
            Assert.True(capability.TryGetProperty("tags", out _), $"Capability '{id}' missing 'tags' field");
        }
    }

    [Fact]
    public void BrowsersCatalog_AllCapabilities_ShouldHaveUniqueIds()
    {
        // Arrange
        using var doc = LoadBrowsersCatalog();
        var root = doc.RootElement;
        var capabilities = root.GetProperty("capabilities");
        
        // Act
        var ids = new List<string>();
        foreach (var capability in capabilities.EnumerateArray())
        {
            if (capability.TryGetProperty("id", out var idProp))
            {
                var id = idProp.GetString();
                Assert.NotNull(id);
                Assert.DoesNotContain(id, ids);
                ids.Add(id);
            }
        }
        
        // Assert
        Assert.True(ids.Count > 0, "Should have at least one capability with an ID");
    }

    [Fact]
    public void BrowsersCatalog_AllCapabilities_ShouldSupportDryRun()
    {
        // Arrange
        using var doc = LoadBrowsersCatalog();
        var root = doc.RootElement;
        var capabilities = root.GetProperty("capabilities");
        
        // Act & Assert
        foreach (var capability in capabilities.EnumerateArray())
        {
            var id = capability.TryGetProperty("id", out var idProp) ? idProp.GetString() : "unknown";
            
            Assert.True(capability.TryGetProperty("supportsDryRun", out var dryRunProp), 
                $"Capability '{id}' missing 'supportsDryRun' field");
            Assert.True(dryRunProp.GetBoolean(), 
                $"Capability '{id}' must have supportsDryRun set to true");
        }
    }

    [Fact]
    public void BrowsersCatalog_AllCapabilities_ShouldNotHaveUnexpectedFields()
    {
        // Arrange
        using var doc = LoadBrowsersCatalog();
        var root = doc.RootElement;
        var capabilities = root.GetProperty("capabilities");
        
        var expectedFields = new HashSet<string>
        {
            "id", "title", "description", "level", "domain", 
            "requiresAdmin", "supportsDryRun", "rollback", "risk", 
            "paramsSchema", "tags"
        };
        
        // Act & Assert
        foreach (var capability in capabilities.EnumerateArray())
        {
            var id = capability.TryGetProperty("id", out var idProp) ? idProp.GetString() : "unknown";
            
            foreach (var property in capability.EnumerateObject())
            {
                Assert.True(expectedFields.Contains(property.Name), 
                    $"Capability '{id}' has unexpected field: '{property.Name}'");
            }
        }
    }

    [Fact]
    public void BrowsersCatalog_AllCapabilities_RollbackShouldBeArray()
    {
        // Arrange
        using var doc = LoadBrowsersCatalog();
        var root = doc.RootElement;
        var capabilities = root.GetProperty("capabilities");
        
        // Act & Assert
        foreach (var capability in capabilities.EnumerateArray())
        {
            var id = capability.TryGetProperty("id", out var idProp) ? idProp.GetString() : "unknown";
            
            if (capability.TryGetProperty("rollback", out var rollbackProp))
            {
                Assert.True(rollbackProp.ValueKind == JsonValueKind.Array, 
                    $"Capability '{id}' rollback should be an array");
            }
        }
    }

    [Fact]
    public void BrowsersCatalog_AllCapabilities_TagsShouldBeArray()
    {
        // Arrange
        using var doc = LoadBrowsersCatalog();
        var root = doc.RootElement;
        var capabilities = root.GetProperty("capabilities");
        
        // Act & Assert
        foreach (var capability in capabilities.EnumerateArray())
        {
            var id = capability.TryGetProperty("id", out var idProp) ? idProp.GetString() : "unknown";
            
            if (capability.TryGetProperty("tags", out var tagsProp))
            {
                Assert.True(tagsProp.ValueKind == JsonValueKind.Array, 
                    $"Capability '{id}' tags should be an array");
            }
        }
    }

    [Fact]
    public void BrowsersCatalog_AllCapabilities_ParamsSchemaShouldBeObject()
    {
        // Arrange
        using var doc = LoadBrowsersCatalog();
        var root = doc.RootElement;
        var capabilities = root.GetProperty("capabilities");
        
        // Act & Assert
        foreach (var capability in capabilities.EnumerateArray())
        {
            var id = capability.TryGetProperty("id", out var idProp) ? idProp.GetString() : "unknown";
            
            if (capability.TryGetProperty("paramsSchema", out var schemaProp))
            {
                Assert.True(schemaProp.ValueKind == JsonValueKind.Object, 
                    $"Capability '{id}' paramsSchema should be an object");
            }
        }
    }

    [Fact]
    public void BrowsersCatalog_AllCapabilities_ShouldHaveValidRiskLevel()
    {
        // Arrange
        using var doc = LoadBrowsersCatalog();
        var root = doc.RootElement;
        var capabilities = root.GetProperty("capabilities");
        var validRiskLevels = new[] { "LOW", "MEDIUM", "HIGH", "CRITICAL" };
        
        // Act & Assert
        foreach (var capability in capabilities.EnumerateArray())
        {
            var id = capability.TryGetProperty("id", out var idProp) ? idProp.GetString() : "unknown";
            
            if (capability.TryGetProperty("risk", out var riskProp))
            {
                var risk = riskProp.GetString();
                Assert.NotNull(risk);
                Assert.Contains(risk, validRiskLevels);
            }
        }
    }

    [Fact]
    public void BrowsersCatalog_AllCapabilities_ShouldHaveValidLevel()
    {
        // Arrange
        using var doc = LoadBrowsersCatalog();
        var root = doc.RootElement;
        var capabilities = root.GetProperty("capabilities");
        var validLevels = new[] { "CORE", "ADVANCED", "EXPERT", "BONUS" };
        
        // Act & Assert
        foreach (var capability in capabilities.EnumerateArray())
        {
            var id = capability.TryGetProperty("id", out var idProp) ? idProp.GetString() : "unknown";
            
            if (capability.TryGetProperty("level", out var levelProp))
            {
                var level = levelProp.GetString();
                Assert.NotNull(level);
                Assert.Contains(level, validLevels);
            }
        }
    }

    [Fact]
    public void BrowsersCatalog_ShouldContainExpectedCapabilities()
    {
        // Arrange
        using var doc = LoadBrowsersCatalog();
        var root = doc.RootElement;
        var capabilities = root.GetProperty("capabilities");
        
        var expectedIds = new[]
        {
            "CLEAN_BROWSER_COOKIES_SELECTIVE",
            "CLEAN_BROWSER_HISTORY",
            "CLEAN_BROWSER_STORAGE_LOCAL",
            "CLEAN_BROWSER_STORAGE_SESSION",
            "CLEAN_BROWSER_EXTENSIONS_LIST",
            "CLEAN_BROWSER_CACHE_PER_PROFILE",
            "CLEAN_BROWSER_SESSIONS_PRESERVE_LOGGED_IN",
            "CLEAN_BROWSER_PROFILES_INACTIVE",
            "CLEAN_BROWSER_DOWNLOADS_LIST",
            "CLEAN_BROWSER_FORM_AUTOFILL"
        };
        
        // Act
        var actualIds = new List<string>();
        foreach (var capability in capabilities.EnumerateArray())
        {
            if (capability.TryGetProperty("id", out var idProp))
            {
                var id = idProp.GetString();
                Assert.NotNull(id);
                actualIds.Add(id);
            }
        }
        
        // Assert
        foreach (var expectedId in expectedIds)
        {
            Assert.Contains(expectedId, actualIds);
        }
    }
}
