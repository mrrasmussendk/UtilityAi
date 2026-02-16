using Example.TaskManagement.Considerations;
using UtilityAi.Capabilities;
using UtilityAi.Consideration;
using UtilityAi.Utils;

namespace Example.TaskManagement.Modules;

/// <summary>
/// Validates pending tasks to ensure they meet system requirements.
/// </summary>
public sealed class ValidationModule : ICapabilityModule
{
    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        var queue = rt.Bus.GetOrDefault<TaskQueue>();
        if (queue is null) yield break;

        var pendingTasks = queue.Tasks.Where(t => t.Status == TaskStatus.Pending).ToList();

        foreach (var task in pendingTasks)
        {
            yield return new Proposal(
                id: $"validate.{task.Id}",
                cons: new IConsideration[]
                {
                    // Higher priority tasks are more urgent to validate
                    new FixedValue("priority", (int)task.Priority / 4.0),

                    // Older tasks need validation sooner
                    new FixedValue("age",
                        Math.Min(1.0, (DateTime.UtcNow - task.SubmittedAt).TotalSeconds / 60.0))
                },
                act: async ct =>
                {
                    // Simulate validation work
                    await Task.Delay(50, ct);

                    // Update task status
                    var updatedTasks = queue.Tasks.Select(t =>
                        t.Id == task.Id
                            ? t with { Status = TaskStatus.Validated }
                            : t
                    ).ToList();

                    rt.Bus.Publish(new TaskQueue(updatedTasks));
                    rt.Bus.Publish(new TaskValidated(task.Id));

                    Console.WriteLine($"    ✓ Validated task: {task.Name}");
                }
            );
        }
    }
}
