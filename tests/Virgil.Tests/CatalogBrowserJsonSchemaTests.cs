using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace Virgil.Tests;

public class CatalogBrowserJsonSchemaTests
{
    private static readonly string BrowsersCatalogPath = Path.Combine(
        Path.GetDirectoryName(typeof(CatalogBrowserJsonSchemaTests).Assembly.Location) ?? string.Empty,
        "..", "..", "..", "..", "..",
        "docs", "spec", "capabilities", "catalog", "browsers.json"
    );

    [Fact]
    public void BrowsersJson_ShouldExist()
    {
        // Assert
        Assert.True(File.Exists(BrowsersCatalogPath), $"browsers.json should exist at path: {BrowsersCatalogPath}");
    }

    [Fact]
    public void BrowsersJson_ShouldBeValidJson()
    {
        // Arrange
        var jsonContent = File.ReadAllText(BrowsersCatalogPath);

        // Act & Assert
        var exception = Record.Exception(() => JsonDocument.Parse(jsonContent));
        Assert.Null(exception);
    }

    [Fact]
    public void BrowsersJson_ShouldHaveRequiredPackMetadata()
    {
        // Arrange
        var jsonContent = File.ReadAllText(BrowsersCatalogPath);
        var doc = JsonDocument.Parse(jsonContent);
        var root = doc.RootElement;

        // Assert
        Assert.True(root.TryGetProperty("name", out _), "Pack must have 'name' property");
        Assert.True(root.TryGetProperty("version", out _), "Pack must have 'version' property");
        Assert.True(root.TryGetProperty("capabilitySchemaVersion", out _), "Pack must have 'capabilitySchemaVersion' property");
        Assert.True(root.TryGetProperty("domain", out _), "Pack must have 'domain' property");
        Assert.True(root.TryGetProperty("description", out _), "Pack must have 'description' property");
        Assert.True(root.TryGetProperty("capabilities", out _), "Pack must have 'capabilities' property");
    }

    [Fact]
    public void BrowsersJson_ShouldHaveCapabilitiesArray()
    {
        // Arrange
        var jsonContent = File.ReadAllText(BrowsersCatalogPath);
        var doc = JsonDocument.Parse(jsonContent);
        var root = doc.RootElement;

        // Act
        var capabilities = root.GetProperty("capabilities");

        // Assert
        Assert.Equal(JsonValueKind.Array, capabilities.ValueKind);
        Assert.True(capabilities.GetArrayLength() > 0, "capabilities array should not be empty");
    }

    [Fact]
    public void BrowsersJson_AllCapabilities_ShouldHaveRequiredFields()
    {
        // Arrange
        var jsonContent = File.ReadAllText(BrowsersCatalogPath);
        var doc = JsonDocument.Parse(jsonContent);
        var capabilities = doc.RootElement.GetProperty("capabilities");

        var requiredFields = new[] { "id", "title", "level", "domain", "requiresAdmin", "supportsDryRun", "rollback", "risk", "paramsSchema", "description", "tags" };

        // Act & Assert
        foreach (var capability in capabilities.EnumerateArray())
        {
            foreach (var field in requiredFields)
            {
                Assert.True(
                    capability.TryGetProperty(field, out _),
                    $"Capability must have required field '{field}'. Missing in capability: {(capability.TryGetProperty("id", out var id) ? id.GetString() : "unknown")}"
                );
            }
        }
    }

    [Fact]
    public void BrowsersJson_AllCapabilities_ShouldHaveUniqueIds()
    {
        // Arrange
        var jsonContent = File.ReadAllText(BrowsersCatalogPath);
        var doc = JsonDocument.Parse(jsonContent);
        var capabilities = doc.RootElement.GetProperty("capabilities");

        // Act
        var ids = new HashSet<string>();
        var duplicates = new List<string>();

        foreach (var capability in capabilities.EnumerateArray())
        {
            var id = capability.GetProperty("id").GetString();
            if (id != null)
            {
                if (!ids.Add(id))
                {
                    duplicates.Add(id);
                }
            }
        }

        // Assert
        Assert.Empty(duplicates);
    }

    [Fact]
    public void BrowsersJson_AllCapabilities_ShouldHaveSupportsDryRunTrue()
    {
        // Arrange
        var jsonContent = File.ReadAllText(BrowsersCatalogPath);
        var doc = JsonDocument.Parse(jsonContent);
        var capabilities = doc.RootElement.GetProperty("capabilities");

        // Act & Assert
        foreach (var capability in capabilities.EnumerateArray())
        {
            var id = capability.GetProperty("id").GetString();
            var supportsDryRun = capability.GetProperty("supportsDryRun").GetBoolean();
            
            Assert.True(supportsDryRun, $"Capability '{id}' must have supportsDryRun=true");
        }
    }

    [Fact]
    public void BrowsersJson_AllCapabilities_ShouldHaveValidLevel()
    {
        // Arrange
        var jsonContent = File.ReadAllText(BrowsersCatalogPath);
        var doc = JsonDocument.Parse(jsonContent);
        var capabilities = doc.RootElement.GetProperty("capabilities");
        var validLevels = new[] { "CORE", "ADVANCED", "EXPERT", "BONUS" };

        // Act & Assert
        foreach (var capability in capabilities.EnumerateArray())
        {
            var id = capability.GetProperty("id").GetString();
            var level = capability.GetProperty("level").GetString();
            
            Assert.Contains(level, validLevels, StringComparer.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void BrowsersJson_AllCapabilities_ShouldHaveValidRisk()
    {
        // Arrange
        var jsonContent = File.ReadAllText(BrowsersCatalogPath);
        var doc = JsonDocument.Parse(jsonContent);
        var capabilities = doc.RootElement.GetProperty("capabilities");
        var validRisks = new[] { "LOW", "MEDIUM", "HIGH", "CRITICAL" };

        // Act & Assert
        foreach (var capability in capabilities.EnumerateArray())
        {
            var id = capability.GetProperty("id").GetString();
            var risk = capability.GetProperty("risk").GetString();
            
            Assert.Contains(risk, validRisks, StringComparer.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void BrowsersJson_AllCapabilities_ShouldNotHaveUnexpectedFields()
    {
        // Arrange
        var jsonContent = File.ReadAllText(BrowsersCatalogPath);
        var doc = JsonDocument.Parse(jsonContent);
        var capabilities = doc.RootElement.GetProperty("capabilities");

        var expectedFields = new HashSet<string> { "id", "title", "level", "domain", "requiresAdmin", "supportsDryRun", "rollback", "risk", "paramsSchema", "description", "tags" };

        // Act & Assert
        foreach (var capability in capabilities.EnumerateArray())
        {
            var id = capability.GetProperty("id").GetString();
            var actualFields = new HashSet<string>();
            
            foreach (var property in capability.EnumerateObject())
            {
                actualFields.Add(property.Name);
            }

            var unexpectedFields = actualFields.Except(expectedFields).ToList();
            
            Assert.Empty(unexpectedFields);
        }
    }

    [Fact]
    public void BrowsersJson_AllCapabilities_ShouldHaveValidTagsArray()
    {
        // Arrange
        var jsonContent = File.ReadAllText(BrowsersCatalogPath);
        var doc = JsonDocument.Parse(jsonContent);
        var capabilities = doc.RootElement.GetProperty("capabilities");

        // Act & Assert
        foreach (var capability in capabilities.EnumerateArray())
        {
            var id = capability.GetProperty("id").GetString();
            var tags = capability.GetProperty("tags");
            
            Assert.Equal(JsonValueKind.Array, tags.ValueKind);
            
            // All capabilities in browsers.json should have browsers, cleaning, and dry-run tags
            var tagsList = tags.EnumerateArray().Select(t => t.GetString()).ToList();
            Assert.Contains("browsers", tagsList);
            Assert.Contains("cleaning", tagsList);
            Assert.Contains("dry-run", tagsList);
        }
    }

    [Fact]
    public void BrowsersJson_AllCapabilities_ShouldHaveValidParamsSchema()
    {
        // Arrange
        var jsonContent = File.ReadAllText(BrowsersCatalogPath);
        var doc = JsonDocument.Parse(jsonContent);
        var capabilities = doc.RootElement.GetProperty("capabilities");

        // Act & Assert
        foreach (var capability in capabilities.EnumerateArray())
        {
            var id = capability.GetProperty("id").GetString();
            var paramsSchema = capability.GetProperty("paramsSchema");
            
            Assert.Equal(JsonValueKind.Object, paramsSchema.ValueKind);
            Assert.True(paramsSchema.TryGetProperty("type", out var type), $"Capability '{id}' paramsSchema must have 'type' property");
            Assert.Equal("object", type.GetString());
        }
    }

    [Fact]
    public void BrowsersJson_ShouldContainAllRequiredCapabilities()
    {
        // Arrange
        var jsonContent = File.ReadAllText(BrowsersCatalogPath);
        var doc = JsonDocument.Parse(jsonContent);
        var capabilities = doc.RootElement.GetProperty("capabilities");

        var requiredCapabilityIds = new[]
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
        var actualIds = capabilities.EnumerateArray()
            .Select(c => c.GetProperty("id").GetString())
            .ToHashSet();

        // Assert
        foreach (var requiredId in requiredCapabilityIds)
        {
            Assert.Contains(requiredId, actualIds);
        }
    }
}
