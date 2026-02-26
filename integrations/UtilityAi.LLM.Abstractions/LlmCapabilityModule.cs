using System.Runtime.InteropServices;
using UtilityAi.Capabilities;
using UtilityAi.Consideration;
using UtilityAi.Consideration.General;
using UtilityAi.Orchestration;
using UtilityAi.Utils;
using System.Diagnostics;
using System.Text.Json;

namespace UtilityAi.LLM.Abstractions;

/// <summary>
/// Base class for creating LLM-powered capability modules.
/// Handles common patterns like conversation history building and error handling.
/// </summary>
public abstract class LlmCapabilityModule : ICapabilityModule
{
    private static readonly TimeSpan ScriptExecutionTimeout = TimeSpan.FromSeconds(30);
    private static readonly HashSet<string> AllowedScriptExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ps1", ".py", ".sh", ".exe", ".bat", ".cmd"
    };

    protected readonly ILlmProvider Provider;
    protected readonly LlmModuleConfiguration Configuration;

    protected LlmCapabilityModule(ILlmProvider provider, LlmModuleConfiguration? configuration = null)
    {
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        Configuration = configuration ?? new LlmModuleConfiguration();
    }

    public abstract IEnumerable<Proposal> Propose(Runtime rt);

    /// <summary>
    /// Builds conversation history from EventBus for a specific message type.
    /// </summary>
    protected List<LlmMessage> BuildConversationHistory<TMessage>(
        Runtime rt,
        Func<TMessage, string> messageSelector,
        Func<TMessage, LlmRole> roleSelector,
        int maxMessages = 10)
        where TMessage : notnull
    {
        var history = rt.Bus.GetHistory<TMessage>(maxItems: maxMessages);
        return history
            .Select(e => new LlmMessage(roleSelector(e.Value), messageSelector(e.Value)))
            .ToList();
    }

    /// <summary>
    /// Creates a proposal that calls the LLM with the given configuration.
    /// </summary>
    protected Proposal CreateLlmProposal(
        string proposalId,
        Runtime rt,
        Func<Runtime, List<LlmMessage>> messagesBuilder,
        LlmOptions? options = null,
        IReadOnlyList<ProposalSkill>? skills = null,
        params IConsideration[] considerations)
    {
        var resolvedSkills = skills ?? ProposalSkillDiscovery.DiscoverForModule(GetType().Name);
        var builder = ProposalHelper.For(proposalId);

        // Add considerations one by one
        foreach (var consideration in considerations)
        {
            builder = builder.WithConsideration(consideration);
        }

        foreach (var skill in resolvedSkills)
        {
            builder = builder.WithSkill(skill);
        }

        return builder
            .WithAction(async ct =>
            {
                try
                {
                    var messages = messagesBuilder(rt);
                    var request = new LlmRequest(messages, MergeOptionsWithSkills(options ?? Configuration.DefaultOptions, resolvedSkills));

                    LlmResponse response;
                    if (Configuration.EnableRetry)
                    {
                        response = await RetryAsync(
                            () => Provider.CompleteAsync(request, ct),
                            Configuration.MaxRetries,
                            Configuration.RetryDelayMs,
                            ct);
                    }
                    else
                    {
                        response = await Provider.CompleteAsync(request, ct);
                    }

                    foreach (var result in await ExecuteSkillScriptsAsync(response.ToolCalls, resolvedSkills, ct))
                    {
                        rt.Bus.Publish(result);
                    }

                    // Invoke user-defined response handler
                    await Configuration.OnResponseReceived?.Invoke(rt, response, ct)!;
                }
                catch (Exception ex)
                {
                    // Invoke user-defined error handler
                    Configuration.OnError?.Invoke(rt, ex);

                    // Re-throw if user didn't handle it
                    if (!Configuration.SuppressErrors)
                        throw;
                }
            })
            .Build();
    }

    private static LlmOptions? MergeOptionsWithSkills(LlmOptions? options, IReadOnlyList<ProposalSkill> skills)
    {
        if (skills.Count == 0)
            return options;

        var skillTools = skills.Select(BuildToolFromSkill).ToList();
        if (options == null)
            return new LlmOptions(Tools: skillTools);

        var mergedTools = new List<LlmTool>();
        if (options.Tools != null)
            mergedTools.AddRange(options.Tools);
        mergedTools.AddRange(skillTools);

        return options with { Tools = mergedTools };
    }

    private static LlmTool BuildToolFromSkill(ProposalSkill skill)
    {
        var scriptHint = string.IsNullOrWhiteSpace(skill.ScriptPath) ? string.Empty : $" Script: {skill.ScriptPath}.";
        return new LlmTool(
            Name: ToToolName(skill.Name),
            Description: $"{skill.Description}{scriptHint}",
            ParametersSchema: JsonDocument.Parse("""{"type":"object","additionalProperties":true}"""));
    }

    private static string ToToolName(string name)
    {
        var chars = new char[name.Length];
        for (var i = 0; i < name.Length; i++)
            chars[i] = char.IsLetterOrDigit(name[i]) ? char.ToLowerInvariant(name[i]) : '_';

        var cleaned = new string(chars).Trim('_');
        return string.IsNullOrWhiteSpace(cleaned) ? "skill_tool" : cleaned;
    }

    private static async Task<IReadOnlyList<SkillExecutionResult>> ExecuteSkillScriptsAsync(
        IReadOnlyList<LlmToolCall>? toolCalls,
        IReadOnlyList<ProposalSkill> skills,
        CancellationToken ct)
    {
        if (toolCalls == null || toolCalls.Count == 0 || skills.Count == 0)
            return Array.Empty<SkillExecutionResult>();

        var skillsByToolName = skills
            .GroupBy(skill => ToToolName(skill.Name), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var results = new List<SkillExecutionResult>();

        foreach (var call in toolCalls)
        {
            if (!skillsByToolName.TryGetValue(call.Name, out var skill) || string.IsNullOrWhiteSpace(skill.ScriptPath))
                continue;

            results.Add(await ExecuteScriptAsync(skill, call.ArgumentsJson, ct));
        }

        return results;
    }

    private static async Task<SkillExecutionResult> ExecuteScriptAsync(ProposalSkill skill, string argumentsJson, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(skill.ScriptPath))
            return new SkillExecutionResult(skill.Name, skill.ScriptPath, -1, string.Empty, "Script file not configured.");

        var scriptPath = skill.ScriptPath;
        if (!Path.IsPathRooted(scriptPath))
            return new SkillExecutionResult(skill.Name, scriptPath, -1, string.Empty, "Script path must be absolute.");

        try
        {
            // Parse and dispose immediately to validate that tool arguments are valid JSON.
            using var _ = JsonDocument.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson);
        }
        catch (JsonException ex)
        {
            return new SkillExecutionResult(skill.Name, scriptPath, -1, string.Empty, $"Invalid tool arguments JSON: {ex.Message}");
        }

        var extension = Path.GetExtension(scriptPath);
        if (string.IsNullOrWhiteSpace(extension))
            return new SkillExecutionResult(skill.Name, scriptPath, -1, string.Empty, "Script file extension is required.");

        extension = extension.ToLowerInvariant();
        if (!AllowedScriptExtensions.Contains(extension))
            return new SkillExecutionResult(skill.Name, scriptPath, -1, string.Empty, $"Script extension '{extension}' is not allowed.");

        if (!File.Exists(scriptPath))
            return new SkillExecutionResult(skill.Name, scriptPath, -1, string.Empty, "Script file not found.");

        var interpreter = extension switch
        {
            ".ps1" => ResolveInterpreterPath(GetPreferredPowerShellPaths()),
            ".py" => ResolveInterpreterPath(GetPreferredPythonPaths()),
            ".sh" => ResolveInterpreterPath(GetPreferredBashPaths()),
            _ => null
        };

        if (extension is ".ps1" or ".py" or ".sh" && interpreter == null)
            return new SkillExecutionResult(skill.Name, scriptPath, -1, string.Empty, $"No trusted interpreter found for '{extension}' scripts.");

        var startInfo = extension switch
        {
            ".ps1" => new ProcessStartInfo(interpreter!),
            ".py" => new ProcessStartInfo(interpreter!),
            ".sh" => new ProcessStartInfo(interpreter!),
            _ => new ProcessStartInfo(scriptPath)
        };

        switch (extension)
        {
            case ".ps1":
                startInfo.ArgumentList.Add("-File");
                startInfo.ArgumentList.Add(scriptPath);
                startInfo.ArgumentList.Add("--args-json");
                startInfo.ArgumentList.Add(argumentsJson);
                break;
            case ".py":
            case ".sh":
                startInfo.ArgumentList.Add(scriptPath);
                startInfo.ArgumentList.Add(argumentsJson);
                break;
            default:
                startInfo.ArgumentList.Add(argumentsJson);
                break;
        }

        startInfo.UseShellExecute = false;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;

        Process? process = null;
        try
        {
            process = new Process { StartInfo = startInfo };
            process.Start();

            var stdOutTask = process.StandardOutput.ReadToEndAsync(ct);
            var stdErrTask = process.StandardError.ReadToEndAsync(ct);
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(ScriptExecutionTimeout);
            await process.WaitForExitAsync(timeoutCts.Token);

            return new SkillExecutionResult(
                SkillName: skill.Name,
                ScriptPath: scriptPath,
                ExitCode: process.ExitCode,
                StandardOutput: await stdOutTask,
                StandardError: await stdErrTask);
        }
        catch (OperationCanceledException)
        {
            if (process is { HasExited: false })
            {
                try { process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { /* Best effort cleanup: process may already have exited. */ }
            }
            return new SkillExecutionResult(skill.Name, scriptPath, -1, string.Empty, $"Script execution timed out after {ScriptExecutionTimeout.TotalSeconds:0} seconds.");
        }
        catch (Exception ex)
        {
            return new SkillExecutionResult(skill.Name, scriptPath, -1, string.Empty, $"Script execution failed: {ex.Message}");
        }
        finally
        {
            process?.Dispose();
        }
    }

    private static string? ResolveInterpreterPath(IEnumerable<string> preferredPaths)
    {
        foreach (var preferredPath in preferredPaths)
        {
            if (File.Exists(preferredPath))
                return preferredPath;
        }

        return null;
    }

    private static IEnumerable<string> GetPreferredPowerShellPaths()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new[] { @"C:\Program Files\PowerShell\7\pwsh.exe", @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe" }
            : new[] { "/usr/bin/pwsh", "/usr/local/bin/pwsh" };
    }

    private static IEnumerable<string> GetPreferredPythonPaths()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new[] { @"C:\Python311\python.exe", @"C:\Python310\python.exe" }
            : new[] { "/usr/bin/python3", "/usr/local/bin/python3" };
    }

    private static IEnumerable<string> GetPreferredBashPaths()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Array.Empty<string>()
            : new[] { "/usr/bin/bash", "/bin/bash" };
    }

    private static async Task<T> RetryAsync<T>(
        Func<Task<T>> operation,
        int maxRetries,
        int delayMs,
        CancellationToken ct)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                return await operation();
            }
            catch when (i < maxRetries - 1)
            {
                await Task.Delay(delayMs * (i + 1), ct); // Exponential backoff
            }
        }

        return await operation(); // Last attempt without catch
    }
}

/// <summary>
/// Configuration for LLM capability modules.
/// </summary>
public record LlmModuleConfiguration(
    LlmOptions? DefaultOptions = null,
    bool EnableRetry = true,
    int MaxRetries = 3,
    int RetryDelayMs = 1000,
    bool SuppressErrors = false,
    Func<Runtime, LlmResponse, CancellationToken, Task>? OnResponseReceived = null,
    Action<Runtime, Exception>? OnError = null);
