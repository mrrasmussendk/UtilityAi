namespace Example.TaskManagement;

/// <summary>
/// Represents a task in the system with its metadata and state.
/// </summary>
public sealed record TaskItem(
    string Id,
    string Name,
    TaskPriority Priority,
    TaskStatus Status,
    int ResourceCost,
    DateTime SubmittedAt,
    string[]? DependsOn = null
);

public enum TaskPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum TaskStatus
{
    Pending,
    Validated,
    Prioritized,
    Executing,
    Completed,
    Failed
}

/// <summary>
/// Represents the queue of tasks being managed.
/// </summary>
public sealed record TaskQueue(List<TaskItem> Tasks);

/// <summary>
/// Represents system resource availability.
/// </summary>
public sealed record SystemResources(
    int AvailableCpu,
    int AvailableMemory,
    int MaxParallelTasks
);

/// <summary>
/// Signal indicating the priority strategy.
/// </summary>
public sealed record PriorityMode(string Mode);

/// <summary>
/// Fact published when validation completes.
/// </summary>
public sealed record TaskValidated(string TaskId);

/// <summary>
/// Fact published when prioritization completes.
/// </summary>
public sealed record TaskPrioritized(string TaskId, double UrgencyScore);

/// <summary>
/// Fact published when a task starts executing.
/// </summary>
public sealed record TaskExecutionStarted(string TaskId, DateTime StartTime);

/// <summary>
/// Fact published when a task completes.
/// </summary>
public sealed record TaskCompleted(string TaskId, DateTime CompletedAt, bool Success);

/// <summary>
/// Signal indicating resource constraints.
/// </summary>
public sealed record ResourceConstraint(string Type, int Available, int Required);
