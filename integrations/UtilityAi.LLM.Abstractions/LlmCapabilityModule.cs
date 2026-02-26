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
        var cleaned = new string(name.Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '_').ToArray()).Trim('_');
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
        if (string.IsNullOrWhiteSpace(skill.ScriptPath) || !File.Exists(skill.ScriptPath))
            return new SkillExecutionResult(skill.Name, skill.ScriptPath, -1, string.Empty, "Script file not found.");

        var scriptPath = skill.ScriptPath;
        var extension = Path.GetExtension(scriptPath).ToLowerInvariant();
        var startInfo = extension switch
        {
            ".ps1" => new ProcessStartInfo("pwsh"),
            ".py" => new ProcessStartInfo("python"),
            ".sh" => new ProcessStartInfo("bash"),
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

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var stdOutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stdErrTask = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        return new SkillExecutionResult(
            SkillName: skill.Name,
            ScriptPath: scriptPath,
            ExitCode: process.ExitCode,
            StandardOutput: await stdOutTask,
            StandardError: await stdErrTask);
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
