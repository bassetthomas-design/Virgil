using FluentAssertions;
using System.Collections.Generic;
using Virgil.Services;
using Xunit;

namespace Virgil.Tests;

public class RamboSafetyTests
{
    [Fact]
    public void BuildSafeDuplicateDeletionPlan_ShouldKeepAtLeastOneCopyPerGroup()
    {
        var groups = new List<DuplicateGroup>
        {
            new()
            {
                Files = new List<DuplicateFileItem>
                {
                    new() { Path = "a", IsSelected = true },
                    new() { Path = "b", IsSelected = true },
                    new() { Path = "c", IsSelected = true }
                },
                Count = 3,
                SizeBytes = 100
            }
        };

        var plan = RamboModeService.BuildSafeDuplicateDeletionPlan(groups);

        plan.Should().HaveCount(2);
    }
}
