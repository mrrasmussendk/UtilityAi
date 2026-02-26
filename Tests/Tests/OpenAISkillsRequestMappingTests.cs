using System.Text.Json.Nodes;
using UtilityAi.LLM.Abstractions;
using UtilityAi.LLM.OpenAI;
using Xunit;

namespace Tests;

public class OpenAISkillsRequestMappingTests
{
    [Fact]
    public void BuildResponsesApiRequestBody_WithSkillReference_MapsShellEnvironmentSkills()
    {
        var body = OpenAIProvider.BuildResponsesApiRequestBody(
            model: "gpt-4.1-mini",
            messages: new List<LlmMessage> { LlmMessage.User("hello") },
            options: new LlmOptions(
                OpenAiSkills: new OpenAiSkillsOptions(
                    EnvironmentType: OpenAiSkillEnvironmentType.ContainerAuto,
                    References: new[] { new OpenAiSkillReference("skill_123", "latest") })));

        var tools = (JsonArray)body["tools"]!;
        var shellTool = tools
            .Select(node => (JsonObject)node!)
            .Single(tool => tool["type"]!.GetValue<string>() == "shell");

        Assert.Equal("container_auto", shellTool["environment"]!["type"]!.GetValue<string>());
        var skills = (JsonArray)shellTool["environment"]!["skills"]!;
        Assert.Single(skills);
        Assert.Equal("skill_reference", skills[0]!["type"]!.GetValue<string>());
        Assert.Equal("skill_123", skills[0]!["skill_id"]!.GetValue<string>());
        Assert.Equal("latest", skills[0]!["version"]!.GetValue<string>());
    }

    [Fact]
    public void BuildResponsesApiRequestBody_WithInlineSkill_MapsInlineSkillPayload()
    {
        var body = OpenAIProvider.BuildResponsesApiRequestBody(
            model: "gpt-4.1-mini",
            messages: new List<LlmMessage> { LlmMessage.User("hello") },
            options: new LlmOptions(
                OpenAiSkills: new OpenAiSkillsOptions(
                    EnvironmentType: OpenAiSkillEnvironmentType.Local,
                    Inline: new[] { new OpenAiInlineSkill("YmFzZTY0LXppcA==") })));

        var tools = (JsonArray)body["tools"]!;
        var shellTool = tools
            .Select(node => (JsonObject)node!)
            .Single(tool => tool["type"]!.GetValue<string>() == "shell");

        Assert.Equal("local", shellTool["environment"]!["type"]!.GetValue<string>());
        var skills = (JsonArray)shellTool["environment"]!["skills"]!;
        Assert.Single(skills);
        Assert.Equal("inline", skills[0]!["type"]!.GetValue<string>());
        Assert.Equal("YmFzZTY0LXppcA==", skills[0]!["bundle"]!.GetValue<string>());
    }
}
