using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace Virgil.Tests;

public class CatalogBrowsersJsonValidationTests
{
    private const string BrowsersJsonPath = "../../../../../docs/spec/capabilities/catalog/browsers.json";
    
    private static readonly HashSet<string?> ValidLevels = new() { "CORE", "ADVANCED", "EXPERT", "BONUS" };
    private static readonly HashSet<string?> ValidRisks = new() { "LOW", "MEDIUM", "HIGH", "CRITICAL" };
    private static readonly string[] RequiredCapabilityFields = { "id", "title", "description", "level", "risk", "dry_run", "rollback", "inputs", "example", "tags" };

    [Fact]
    public void BrowsersJson_ShouldExist()
    {
        // Arrange & Act
        var fullPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, BrowsersJsonPath));

        // Assert
        Assert.True(File.Exists(fullPath), $"browsers.json should exist at path: {fullPath}");
    }

    [Fact]
    public void BrowsersJson_ShouldBeValidJson()
    {
        // Arrange
        var fullPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, BrowsersJsonPath));
        var jsonContent = File.ReadAllText(fullPath);

        // Act & Assert
        var exception = Record.Exception(() => JsonDocument.Parse(jsonContent));
        Assert.Null(exception);
    }

    [Fact]
    public void BrowsersJson_ShouldHaveRequiredRootFields()
    {
        // Arrange
        var fullPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, BrowsersJsonPath));
        var jsonContent = File.ReadAllText(fullPath);
        using var doc = JsonDocument.Parse(jsonContent);
        var root = doc.RootElement;

        // Assert
        Assert.True(root.TryGetProperty("name", out _), "Root should have 'name' property");
        Assert.True(root.TryGetProperty("version", out _), "Root should have 'version' property");
        Assert.True(root.TryGetProperty("generatedAt", out _), "Root should have 'generatedAt' property");
        Assert.True(root.TryGetProperty("capabilitySchemaVersion", out _), "Root should have 'capabilitySchemaVersion' property");
        Assert.True(root.TryGetProperty("domain", out _), "Root should have 'domain' property");
        Assert.True(root.TryGetProperty("description", out _), "Root should have 'description' property");
        Assert.True(root.TryGetProperty("capabilities", out _), "Root should have 'capabilities' property");
    }

    [Fact]
    public void BrowsersJson_CapabilitiesShouldBeArray()
    {
        // Arrange
        var fullPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, BrowsersJsonPath));
        var jsonContent = File.ReadAllText(fullPath);
        using var doc = JsonDocument.Parse(jsonContent);
        var root = doc.RootElement;

        // Act
        var capabilities = root.GetProperty("capabilities");

        // Assert
        Assert.Equal(JsonValueKind.Array, capabilities.ValueKind);
    }

    [Fact]
    public void BrowsersJson_ShouldHaveAllRequiredCapabilities()
    {
        // Arrange
        var fullPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, BrowsersJsonPath));
        var jsonContent = File.ReadAllText(fullPath);
        using var doc = JsonDocument.Parse(jsonContent);
        var root = doc.RootElement;
        var capabilities = root.GetProperty("capabilities");

        var expectedCapabilityIds = new HashSet<string>
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
        var actualIds = new HashSet<string>();
        foreach (var capability in capabilities.EnumerateArray())
        {
            if (capability.TryGetProperty("id", out var idElement))
            {
                var id = idElement.GetString();
                if (!string.IsNullOrEmpty(id))
                {
                    actualIds.Add(id);
                }
            }
        }

        // Assert
        Assert.Equal(expectedCapabilityIds.Count, actualIds.Count);
        foreach (var expectedId in expectedCapabilityIds)
        {
            Assert.Contains(expectedId, actualIds);
        }
    }

    [Fact]
    public void BrowsersJson_AllCapabilitiesShouldHaveRequiredFields()
    {
        // Arrange
        var fullPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, BrowsersJsonPath));
        var jsonContent = File.ReadAllText(fullPath);
        using var doc = JsonDocument.Parse(jsonContent);
        var root = doc.RootElement;
        var capabilities = root.GetProperty("capabilities");

        // Act & Assert
        foreach (var capability in capabilities.EnumerateArray())
        {
            var id = capability.TryGetProperty("id", out var idElement) ? idElement.GetString() : "unknown";

            foreach (var field in RequiredCapabilityFields)
            {
                Assert.True(
                    capability.TryGetProperty(field, out _),
                    $"Capability '{id}' should have '{field}' field"
                );
            }
        }
    }

    [Fact]
    public void BrowsersJson_AllCapabilitiesShouldHaveDryRunTrue()
    {
        // Arrange
        var fullPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, BrowsersJsonPath));
        var jsonContent = File.ReadAllText(fullPath);
        using var doc = JsonDocument.Parse(jsonContent);
        var root = doc.RootElement;
        var capabilities = root.GetProperty("capabilities");

        // Act & Assert
        foreach (var capability in capabilities.EnumerateArray())
        {
            var id = capability.TryGetProperty("id", out var idElement) ? idElement.GetString() : "unknown";
            
            Assert.True(
                capability.TryGetProperty("dry_run", out var dryRunElement),
                $"Capability '{id}' should have 'dry_run' field"
            );
            
            Assert.True(
                dryRunElement.GetBoolean(),
                $"Capability '{id}' should have dry_run set to true"
            );
        }
    }

    [Fact]
    public void BrowsersJson_AllCapabilityIdsShouldBeUnique()
    {
        // Arrange
        var fullPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, BrowsersJsonPath));
        var jsonContent = File.ReadAllText(fullPath);
        using var doc = JsonDocument.Parse(jsonContent);
        var root = doc.RootElement;
        var capabilities = root.GetProperty("capabilities");

        // Act
        var ids = new List<string>();
        foreach (var capability in capabilities.EnumerateArray())
        {
            if (capability.TryGetProperty("id", out var idElement))
            {
                var id = idElement.GetString();
                if (!string.IsNullOrEmpty(id))
                {
                    ids.Add(id);
                }
            }
        }

        // Assert
        var duplicates = ids.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.Empty(duplicates);
    }

    [Fact]
    public void BrowsersJson_AllCapabilitiesShouldHaveValidLevel()
    {
        // Arrange
        var fullPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, BrowsersJsonPath));
        var jsonContent = File.ReadAllText(fullPath);
        using var doc = JsonDocument.Parse(jsonContent);
        var root = doc.RootElement;
        var capabilities = root.GetProperty("capabilities");

        // Act & Assert
        foreach (var capability in capabilities.EnumerateArray())
        {
            var id = capability.TryGetProperty("id", out var idElement) ? idElement.GetString() : "unknown";
            
            Assert.True(
                capability.TryGetProperty("level", out var levelElement),
                $"Capability '{id}' should have 'level' field"
            );

            var level = levelElement.GetString();
            Assert.Contains(level, ValidLevels);
        }
    }

    [Fact]
    public void BrowsersJson_AllCapabilitiesShouldHaveValidRisk()
    {
        // Arrange
        var fullPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, BrowsersJsonPath));
        var jsonContent = File.ReadAllText(fullPath);
        using var doc = JsonDocument.Parse(jsonContent);
        var root = doc.RootElement;
        var capabilities = root.GetProperty("capabilities");

        // Act & Assert
        foreach (var capability in capabilities.EnumerateArray())
        {
            var id = capability.TryGetProperty("id", out var idElement) ? idElement.GetString() : "unknown";
            
            Assert.True(
                capability.TryGetProperty("risk", out var riskElement),
                $"Capability '{id}' should have 'risk' field"
            );

            var risk = riskElement.GetString();
            Assert.Contains(risk, ValidRisks);
        }
    }

    [Fact]
    public void BrowsersJson_AllCapabilitiesShouldHaveNonEmptyStrings()
    {
        // Arrange
        var fullPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, BrowsersJsonPath));
        var jsonContent = File.ReadAllText(fullPath);
        using var doc = JsonDocument.Parse(jsonContent);
        var root = doc.RootElement;
        var capabilities = root.GetProperty("capabilities");

        var stringFields = new[] { "id", "title", "description", "level", "risk", "rollback" };

        // Act & Assert
        foreach (var capability in capabilities.EnumerateArray())
        {
            var id = capability.TryGetProperty("id", out var idElement) ? idElement.GetString() : "unknown";

            foreach (var field in stringFields)
            {
                if (capability.TryGetProperty(field, out var fieldElement))
                {
                    var value = fieldElement.GetString();
                    Assert.False(
                        string.IsNullOrWhiteSpace(value),
                        $"Capability '{id}' field '{field}' should not be empty or whitespace"
                    );
                }
            }
        }
    }

    [Fact]
    public void BrowsersJson_AllCapabilitiesShouldHaveValidInputsSchema()
    {
        // Arrange
        var fullPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, BrowsersJsonPath));
        var jsonContent = File.ReadAllText(fullPath);
        using var doc = JsonDocument.Parse(jsonContent);
        var root = doc.RootElement;
        var capabilities = root.GetProperty("capabilities");

        // Act & Assert
        foreach (var capability in capabilities.EnumerateArray())
        {
            var id = capability.TryGetProperty("id", out var idElement) ? idElement.GetString() : "unknown";
            
            Assert.True(
                capability.TryGetProperty("inputs", out var inputsElement),
                $"Capability '{id}' should have 'inputs' field"
            );

            Assert.Equal(JsonValueKind.Object, inputsElement.ValueKind);
            
            Assert.True(
                inputsElement.TryGetProperty("type", out var typeElement),
                $"Capability '{id}' inputs should have 'type' field"
            );
            
            Assert.Equal("object", typeElement.GetString());
        }
    }

    [Fact]
    public void BrowsersJson_AllCapabilitiesShouldHaveValidExample()
    {
        // Arrange
        var fullPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, BrowsersJsonPath));
        var jsonContent = File.ReadAllText(fullPath);
        using var doc = JsonDocument.Parse(jsonContent);
        var root = doc.RootElement;
        var capabilities = root.GetProperty("capabilities");

        // Act & Assert
        foreach (var capability in capabilities.EnumerateArray())
        {
            var id = capability.TryGetProperty("id", out var idElement) ? idElement.GetString() : "unknown";
            
            Assert.True(
                capability.TryGetProperty("example", out var exampleElement),
                $"Capability '{id}' should have 'example' field"
            );

            Assert.Equal(JsonValueKind.Object, exampleElement.ValueKind);
        }
    }

    [Fact]
    public void BrowsersJson_AllCapabilitiesShouldHaveTags()
    {
        // Arrange
        var fullPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, BrowsersJsonPath));
        var jsonContent = File.ReadAllText(fullPath);
        using var doc = JsonDocument.Parse(jsonContent);
        var root = doc.RootElement;
        var capabilities = root.GetProperty("capabilities");

        // Act & Assert
        foreach (var capability in capabilities.EnumerateArray())
        {
            var id = capability.TryGetProperty("id", out var idElement) ? idElement.GetString() : "unknown";
            
            Assert.True(
                capability.TryGetProperty("tags", out var tagsElement),
                $"Capability '{id}' should have 'tags' field"
            );

            Assert.Equal(JsonValueKind.Array, tagsElement.ValueKind);
            
            // Tags should contain "browsers", "cleaning", and "dry-run"
            var tags = tagsElement.EnumerateArray().Select(t => t.GetString()).ToList();
            Assert.Contains("browsers", tags);
            Assert.Contains("cleaning", tags);
            Assert.Contains("dry-run", tags);
        }
    }

    [Fact]
    public void BrowsersJson_ShouldNotHaveUnexpectedRootFields()
    {
        // Arrange
        var fullPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, BrowsersJsonPath));
        var jsonContent = File.ReadAllText(fullPath);
        using var doc = JsonDocument.Parse(jsonContent);
        var root = doc.RootElement;

        var expectedFields = new HashSet<string>
        {
            "name", "version", "generatedAt", "capabilitySchemaVersion", 
            "domain", "description", "capabilities"
        };

        // Act
        var actualFields = new HashSet<string>();
        foreach (var property in root.EnumerateObject())
        {
            actualFields.Add(property.Name);
        }

        // Assert
        var unexpectedFields = actualFields.Except(expectedFields).ToList();
        Assert.Empty(unexpectedFields);
    }
}
