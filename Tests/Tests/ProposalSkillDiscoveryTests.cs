using UtilityAi.Capabilities;
using Xunit;

namespace Tests;

public class ProposalSkillDiscoveryTests
{
    [Fact]
    public void DiscoverFromModuleDirectory_WithSkillMarkdown_ReturnsSkillWithScriptPath()
    {
        var moduleDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var skillDir = Path.Combine(moduleDir, "Skills", "weather");
        Directory.CreateDirectory(skillDir);
        var scriptPath = Path.Combine(skillDir, "weather.py");
        File.WriteAllText(scriptPath, "print('ok')");
        File.WriteAllText(
            Path.Combine(skillDir, "Skill.md"),
            """
            # WeatherLookup
            Looks up weather from an external provider.
            Script: weather.py
            """);

        try
        {
            var skills = ProposalSkillDiscovery.DiscoverFromModuleDirectory(moduleDir);

            var skill = Assert.Single(skills);
            Assert.Equal("WeatherLookup", skill.Name);
            Assert.Contains("Looks up weather", skill.Description);
            Assert.Equal(Path.GetFullPath(scriptPath), skill.ScriptPath);
        }
        finally
        {
            Directory.Delete(moduleDir, recursive: true);
        }
    }

    [Fact]
    public void DiscoverFromModuleDirectory_WhenSkillsFolderMissing_ReturnsEmpty()
    {
        var moduleDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(moduleDir);

        try
        {
            var skills = ProposalSkillDiscovery.DiscoverFromModuleDirectory(moduleDir);
            Assert.Empty(skills);
        }
        finally
        {
            Directory.Delete(moduleDir, recursive: true);
        }
    }
}
