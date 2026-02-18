using UtilityAi.Facts;
using UtilityAi.Utils;

namespace UtilityAi.Sensor.BuiltIn;

/// <summary>
/// Analyzes conversation history and publishes metadata about the conversation.
/// Tracks message count, duration, and conversation characteristics.
/// </summary>
public sealed class ConversationHistorySensor : ISensor
{
    private readonly int _maxHistoryToAnalyze;
    private readonly int _longConversationThreshold;

    /// <summary>
    /// Creates a conversation history sensor.
    /// </summary>
    /// <param name="maxHistoryToAnalyze">Maximum number of messages to analyze. Default is 100.</param>
    /// <param name="longConversationThreshold">Message count threshold for "long" conversations. Default is 20.</param>
    public ConversationHistorySensor(int maxHistoryToAnalyze = 100, int longConversationThreshold = 20)
    {
        _maxHistoryToAnalyze = maxHistoryToAnalyze;
        _longConversationThreshold = longConversationThreshold;
    }

    public Task SenseAsync(Runtime rt, CancellationToken ct)
    {
        var userMessages = rt.Bus.GetHistory<UserMessage>(maxItems: _maxHistoryToAnalyze);
        var assistantMessages = rt.Bus.GetHistory<AssistantMessage>(maxItems: _maxHistoryToAnalyze);

        var totalMessages = userMessages.Count + assistantMessages.Count;

        if (totalMessages == 0)
        {
            // No conversation yet
            rt.Bus.Publish(new ConversationMetadata(
                MessageCount: 0,
                Duration: TimeSpan.Zero,
                IsLongConversation: false));
            return Task.CompletedTask;
        }

        // Find first and last message timestamps
        var allTimestamps = userMessages.Select(m => m.Timestamp)
            .Concat(assistantMessages.Select(m => m.Timestamp))
            .OrderBy(t => t)
            .ToList();

        var firstMessageTime = allTimestamps.First();
        var lastMessageTime = allTimestamps.Last();
        var duration = lastMessageTime - firstMessageTime;

        rt.Bus.Publish(new ConversationMetadata(
            MessageCount: totalMessages,
            Duration: duration,
            IsLongConversation: totalMessages >= _longConversationThreshold,
            FirstMessageTime: firstMessageTime,
            LastMessageTime: lastMessageTime));

        return Task.CompletedTask;
    }
}
