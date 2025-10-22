namespace UtilityAi.Orchestration.Events;

public sealed record StopOrchestrationEvent(OrchestrationStopReason Reason, string? Message = null);
