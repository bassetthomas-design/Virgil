using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace Virgil.Tests;

public class CatalogBrowserJsonSchemaTests
{
    private const string BrowsersJsonPath = "docs/spec/capabilities/catalog/browsers.json";

    private static string GetRepositoryRoot()
    {
        var currentDir = Directory.GetCurrentDirectory();
        while (currentDir != null && !File.Exists(Path.Combine(currentDir, "Virgil.sln")))
        {
            currentDir = Directory.GetParent(currentDir)?.FullName;
        }
        return currentDir ?? throw new InvalidOperationException("Cannot find repository root");
    }

    [Fact]
    public void BrowsersJson_ShouldExist()
    {
        // Arrange
        var repoRoot = GetRepositoryRoot();
        var filePath = Path.Combine(repoRoot, BrowsersJsonPath);

        // Assert
        Assert.True(File.Exists(filePath), $"browsers.json should exist at {filePath}");
    }

    [Fact]
    public void BrowsersJson_ShouldBeValidJson()
    {
        // Arrange
        var repoRoot = GetRepositoryRoot();
        var filePath = Path.Combine(repoRoot, BrowsersJsonPath);
        var jsonContent = File.ReadAllText(filePath);

        // Act & Assert
        var exception = Record.Exception(() => JsonDocument.Parse(jsonContent));
        Assert.Null(exception);
    }

    [Fact]
    public void BrowsersJson_ShouldHaveRequiredRootFields()
    {
        // Arrange
        var repoRoot = GetRepositoryRoot();
        var filePath = Path.Combine(repoRoot, BrowsersJsonPath);
        var jsonContent = File.ReadAllText(filePath);
        var doc = JsonDocument.Parse(jsonContent);
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
        var repoRoot = GetRepositoryRoot();
        var filePath = Path.Combine(repoRoot, BrowsersJsonPath);
        var jsonContent = File.ReadAllText(filePath);
        var doc = JsonDocument.Parse(jsonContent);
        var root = doc.RootElement;

        // Act
        var capabilities = root.GetProperty("capabilities");

        // Assert
        Assert.Equal(JsonValueKind.Array, capabilities.ValueKind);
    }

    [Fact]
    public void BrowsersJson_ShouldContainExpectedCapabilities()
    {
        // Arrange
        var repoRoot = GetRepositoryRoot();
        var filePath = Path.Combine(repoRoot, BrowsersJsonPath);
        var jsonContent = File.ReadAllText(filePath);
        var doc = JsonDocument.Parse(jsonContent);
        var capabilities = doc.RootElement.GetProperty("capabilities");

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
                actualIds.Add(idProp.GetString()!);
            }
        }

        // Assert
        foreach (var expectedId in expectedIds)
        {
            Assert.Contains(expectedId, actualIds);
        }
    }

    [Fact]
    public void BrowsersJson_AllCapabilitiesShouldHaveRequiredFields()
    {
        // Arrange
        var repoRoot = GetRepositoryRoot();
        var filePath = Path.Combine(repoRoot, BrowsersJsonPath);
        var jsonContent = File.ReadAllText(filePath);
        var doc = JsonDocument.Parse(jsonContent);
        var capabilities = doc.RootElement.GetProperty("capabilities");

        var requiredFields = new[] { "id", "title", "description", "level", "risk", "supportsDryRun", "rollback", "paramsSchema", "tags" };

        // Act & Assert
        foreach (var capability in capabilities.EnumerateArray())
        {
            foreach (var field in requiredFields)
            {
                Assert.True(
                    capability.TryGetProperty(field, out _),
                    $"Capability should have '{field}' property. Missing in: {(capability.TryGetProperty("id", out var id) ? id.GetString() : "unknown")}"
                );
            }
        }
    }

    [Fact]
    public void BrowsersJson_AllCapabilitiesShouldHaveDryRunEnabled()
    {
        // Arrange
        var repoRoot = GetRepositoryRoot();
        var filePath = Path.Combine(repoRoot, BrowsersJsonPath);
        var jsonContent = File.ReadAllText(filePath);
        var doc = JsonDocument.Parse(jsonContent);
        var capabilities = doc.RootElement.GetProperty("capabilities");

        // Act & Assert
        foreach (var capability in capabilities.EnumerateArray())
        {
            var id = capability.GetProperty("id").GetString();
            var supportsDryRun = capability.GetProperty("supportsDryRun").GetBoolean();
            
            Assert.True(supportsDryRun, $"Capability {id} should have supportsDryRun=true");
        }
    }

    [Fact]
    public void BrowsersJson_AllCapabilityIdsShouldBeUnique()
    {
        // Arrange
        var repoRoot = GetRepositoryRoot();
        var filePath = Path.Combine(repoRoot, BrowsersJsonPath);
        var jsonContent = File.ReadAllText(filePath);
        var doc = JsonDocument.Parse(jsonContent);
        var capabilities = doc.RootElement.GetProperty("capabilities");

        // Act
        var ids = new List<string>();
        foreach (var capability in capabilities.EnumerateArray())
        {
            var id = capability.GetProperty("id").GetString();
            ids.Add(id!);
        }

        // Assert
        var duplicates = ids.GroupBy(x => x)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.Empty(duplicates);
    }

    [Fact]
    public void BrowsersJson_ParamsSchemaShouldBeValidObject()
    {
        // Arrange
        var repoRoot = GetRepositoryRoot();
        var filePath = Path.Combine(repoRoot, BrowsersJsonPath);
        var jsonContent = File.ReadAllText(filePath);
        var doc = JsonDocument.Parse(jsonContent);
        var capabilities = doc.RootElement.GetProperty("capabilities");

        // Act & Assert
        foreach (var capability in capabilities.EnumerateArray())
        {
            var id = capability.GetProperty("id").GetString();
            var paramsSchema = capability.GetProperty("paramsSchema");
            
            Assert.Equal(JsonValueKind.Object, paramsSchema.ValueKind);
            Assert.True(paramsSchema.TryGetProperty("type", out var typeProp), $"paramsSchema in {id} should have 'type' property");
            Assert.Equal("object", typeProp.GetString());
        }
    }

    [Fact]
    public void BrowsersJson_TagsShouldBeArrayAndContainExpectedTags()
    {
        // Arrange
        var repoRoot = GetRepositoryRoot();
        var filePath = Path.Combine(repoRoot, BrowsersJsonPath);
        var jsonContent = File.ReadAllText(filePath);
        var doc = JsonDocument.Parse(jsonContent);
        var capabilities = doc.RootElement.GetProperty("capabilities");

        // Act & Assert
        foreach (var capability in capabilities.EnumerateArray())
        {
            var id = capability.GetProperty("id").GetString();
            var tags = capability.GetProperty("tags");
            
            Assert.Equal(JsonValueKind.Array, tags.ValueKind);
            
            var tagsList = new List<string>();
            foreach (var tag in tags.EnumerateArray())
            {
                tagsList.Add(tag.GetString()!);
            }
            
            // All browser capabilities should have "browsers", "cleaning", and "dry-run" tags
            Assert.Contains("browsers", tagsList, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("cleaning", tagsList, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("dry-run", tagsList, StringComparer.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void BrowsersJson_RiskLevelShouldBeValid()
    {
        // Arrange
        var repoRoot = GetRepositoryRoot();
        var filePath = Path.Combine(repoRoot, BrowsersJsonPath);
        var jsonContent = File.ReadAllText(filePath);
        var doc = JsonDocument.Parse(jsonContent);
        var capabilities = doc.RootElement.GetProperty("capabilities");
        var validRiskLevels = new[] { "LOW", "MEDIUM", "HIGH", "CRITICAL" };

        // Act & Assert
        foreach (var capability in capabilities.EnumerateArray())
        {
            var id = capability.GetProperty("id").GetString();
            var risk = capability.GetProperty("risk").GetString();
            
            Assert.Contains(risk, validRiskLevels, StringComparer.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void BrowsersJson_LevelShouldBeValid()
    {
        // Arrange
        var repoRoot = GetRepositoryRoot();
        var filePath = Path.Combine(repoRoot, BrowsersJsonPath);
        var jsonContent = File.ReadAllText(filePath);
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
    public void BrowsersJson_RollbackShouldBeArray()
    {
        // Arrange
        var repoRoot = GetRepositoryRoot();
        var filePath = Path.Combine(repoRoot, BrowsersJsonPath);
        var jsonContent = File.ReadAllText(filePath);
        var doc = JsonDocument.Parse(jsonContent);
        var capabilities = doc.RootElement.GetProperty("capabilities");

        // Act & Assert
        foreach (var capability in capabilities.EnumerateArray())
        {
            var id = capability.GetProperty("id").GetString();
            var rollback = capability.GetProperty("rollback");
            
            Assert.Equal(JsonValueKind.Array, rollback.ValueKind);
        }
    }

    [Fact]
    public void BrowsersJson_ShouldNotContainUnexpectedRootFields()
    {
        // Arrange
        var repoRoot = GetRepositoryRoot();
        var filePath = Path.Combine(repoRoot, BrowsersJsonPath);
        var jsonContent = File.ReadAllText(filePath);
        var doc = JsonDocument.Parse(jsonContent);
        var root = doc.RootElement;

        var expectedFields = new HashSet<string>
        {
            "name", "version", "generatedAt", "capabilitySchemaVersion", 
            "domain", "description", "capabilities"
        };

        // Act
        var actualFields = new List<string>();
        foreach (var prop in root.EnumerateObject())
        {
            actualFields.Add(prop.Name);
        }

        // Assert
        foreach (var field in actualFields)
        {
            Assert.Contains(field, expectedFields);
        }
    }
}
