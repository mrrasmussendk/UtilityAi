using System.Diagnostics;
using UtilityAi.Facts;
using UtilityAi.Utils;

namespace UtilityAi.Sensor.BuiltIn;

/// <summary>
/// Monitors system resource usage (CPU and memory) and publishes metrics.
/// Useful for resource-aware decision making and throttling.
/// </summary>
public sealed class ResourceSensor : ISensor
{
    private readonly Process _currentProcess;
    private DateTimeOffset _lastCheck;
    private TimeSpan _lastTotalProcessorTime;

    public ResourceSensor()
    {
        _currentProcess = Process.GetCurrentProcess();
        _lastCheck = DateTimeOffset.UtcNow;
        _lastTotalProcessorTime = _currentProcess.TotalProcessorTime;
    }

    public Task SenseAsync(Runtime rt, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var currentTotalProcessorTime = _currentProcess.TotalProcessorTime;

        // Calculate CPU usage percentage
        var cpuUsedMs = (currentTotalProcessorTime - _lastTotalProcessorTime).TotalMilliseconds;
        var totalMsPassed = (now - _lastCheck).TotalMilliseconds;
        var cpuPercent = totalMsPassed > 0
            ? (cpuUsedMs / (Environment.ProcessorCount * totalMsPassed)) * 100.0
            : 0.0;

        // Get memory usage in MB
        _currentProcess.Refresh();
        var memoryMb = _currentProcess.WorkingSet64 / (1024.0 * 1024.0);

        // Update last check values
        _lastCheck = now;
        _lastTotalProcessorTime = currentTotalProcessorTime;

        // Publish resource usage
        rt.Bus.Publish(new ResourceUsage(
            CpuPercent: Math.Round(cpuPercent, 2),
            MemoryMegabytes: Math.Round(memoryMb, 2),
            Timestamp: now));

        return Task.CompletedTask;
    }
}
