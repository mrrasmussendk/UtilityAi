using System.Text;

namespace UtilityAi.Capabilities;

/// <summary>
/// Represents a skill that can be attached to a proposal and exposed to LLM tool-calling.
/// </summary>
public sealed record ProposalSkill(
    string Name,
    string Description,
    string SourcePath,
    string? ScriptPath = null);

/// <summary>
/// Discovers proposal skills from the folder structure: Module/Skills/&lt;skill&gt;/Skill.md.
/// </summary>
public static class ProposalSkillDiscovery
{
    /// <summary>
    /// Discovers skills under a module directory.
    /// </summary>
    public static IReadOnlyList<ProposalSkill> DiscoverFromModuleDirectory(string moduleDirectory)
    {
        if (string.IsNullOrWhiteSpace(moduleDirectory))
            throw new ArgumentException("Module directory must be provided.", nameof(moduleDirectory));

        var skillsRoot = Path.Combine(moduleDirectory, "Skills");
        if (!Directory.Exists(skillsRoot))
            return Array.Empty<ProposalSkill>();

        return DiscoverFromSkillsDirectory(skillsRoot);
    }

    /// <summary>
    /// Discovers skills by module name from common runtime locations.
    /// </summary>
    public static IReadOnlyList<ProposalSkill> DiscoverForModule(string moduleName)
    {
        if (string.IsNullOrWhiteSpace(moduleName))
            throw new ArgumentException("Module name must be provided.", nameof(moduleName));

        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), moduleName),
            Path.Combine(AppContext.BaseDirectory, moduleName),
            Path.Combine(AppContext.BaseDirectory, "Modules", moduleName)
        };

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var result = DiscoverFromModuleDirectory(candidate);
            if (result.Count > 0)
                return result;
        }

        return Array.Empty<ProposalSkill>();
    }

    private static IReadOnlyList<ProposalSkill> DiscoverFromSkillsDirectory(string skillsDirectory)
    {
        var result = new List<ProposalSkill>();
        var skillFiles = Directory.GetFiles(skillsDirectory, "Skill.md", SearchOption.AllDirectories);

        foreach (var skillFile in skillFiles)
        {
            var skill = ParseSkillFile(skillFile);
            if (skill != null)
                result.Add(skill);
        }

        return result;
    }

    private static ProposalSkill? ParseSkillFile(string skillFile)
    {
        var content = File.ReadAllText(skillFile);
        if (string.IsNullOrWhiteSpace(content))
            return null;

        var folderName = new DirectoryInfo(Path.GetDirectoryName(skillFile)!).Name;
        var lines = content.Split('\n');

        string? name = null;
        string? script = null;
        var descriptionBuilder = new StringBuilder();

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (name == null && line.StartsWith("#", StringComparison.Ordinal))
            {
                name = line.TrimStart('#').Trim();
                continue;
            }

            if (line.StartsWith("Script:", StringComparison.OrdinalIgnoreCase))
            {
                script = line["Script:".Length..].Trim();
                continue;
            }

            if (!line.StartsWith("#", StringComparison.Ordinal))
            {
                if (descriptionBuilder.Length > 0)
                    descriptionBuilder.Append(' ');
                descriptionBuilder.Append(line);
            }
        }

        var resolvedScriptPath = ResolveScriptPath(skillFile, script);
        return new ProposalSkill(
            Name: string.IsNullOrWhiteSpace(name) ? folderName : name,
            Description: descriptionBuilder.Length > 0 ? descriptionBuilder.ToString() : $"Skill from {folderName}",
            SourcePath: skillFile,
            ScriptPath: resolvedScriptPath
        );
    }

    private static string? ResolveScriptPath(string skillFilePath, string? scriptPath)
    {
        if (string.IsNullOrWhiteSpace(scriptPath))
            return null;

        if (Path.IsPathRooted(scriptPath))
            return scriptPath;

        var skillDirectory = Path.GetDirectoryName(skillFilePath)!;
        return Path.GetFullPath(Path.Combine(skillDirectory, scriptPath));
    }
}
