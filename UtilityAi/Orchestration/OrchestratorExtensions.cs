using System.Reflection;
using UtilityAi.Capabilities;
using UtilityAi.Sensor;

namespace UtilityAi.Orchestration;

/// <summary>
/// Extension methods for UtilityAiOrchestrator to support attribute-based registration.
/// </summary>
public static class OrchestratorExtensions
{
    /// <summary>
    /// Discovers and registers all capability modules marked with [Capability] attribute from the specified assemblies.
    /// </summary>
    /// <param name="orchestrator">The orchestrator to register modules to.</param>
    /// <param name="assemblies">Assemblies to scan for capability modules. If empty, scans calling assembly.</param>
    /// <returns>The orchestrator instance for fluent chaining.</returns>
    /// <remarks>
    /// Modules are registered in order of:
    /// 1. Priority (highest first)
    /// 2. Dependencies (dependencies registered before dependents)
    /// 3. Discovery order (stable sort)
    /// </remarks>
    public static UtilityAiOrchestrator DiscoverCapabilities(
        this UtilityAiOrchestrator orchestrator,
        params Assembly[] assemblies)
    {
        if (assemblies.Length == 0)
        {
            assemblies = new[] { Assembly.GetCallingAssembly() };
        }

        var discovered = new List<(Type Type, CapabilityAttribute Attr, int DiscoveryOrder)>();
        int order = 0;

        foreach (var assembly in assemblies)
        {
            var types = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && typeof(ICapabilityModule).IsAssignableFrom(t));

            foreach (var type in types)
            {
                var attr = type.GetCustomAttribute<CapabilityAttribute>();
                if (attr is null || !attr.Enabled) continue;

                discovered.Add((type, attr, order++));
            }
        }

        // Sort by priority (desc), then by discovery order (stable sort)
        var sorted = discovered
            .OrderByDescending(x => x.Attr.Priority)
            .ThenBy(x => x.DiscoveryOrder)
            .ToList();

        // Resolve dependencies using topological sort
        var resolved = TopologicalSort(sorted);

        // Instantiate, wrap with filters if needed, and register
        foreach (var (type, _, _) in resolved)
        {
            var instance = (ICapabilityModule)Activator.CreateInstance(type)!;

            // Wrap with filter if module has ActiveWhen or RequiresFact attributes
            var hasActiveWhen = type.GetCustomAttributes<ActiveWhenAttribute>().Any();
            var hasRequiresFact = type.GetCustomAttributes()
                .Any(attr => attr.GetType().IsGenericType &&
                            attr.GetType().GetGenericTypeDefinition() == typeof(RequiresFactAttribute<>));

            if (hasActiveWhen || hasRequiresFact)
            {
                instance = new CapabilityFilterWrapper(instance);
            }

            orchestrator.AddModule(instance);
        }

        return orchestrator;
    }

    /// <summary>
    /// Discovers and registers all sensors marked with [Sensor] attribute from the specified assemblies.
    /// </summary>
    public static UtilityAiOrchestrator DiscoverSensors(
        this UtilityAiOrchestrator orchestrator,
        params Assembly[] assemblies)
    {
        if (assemblies.Length == 0)
        {
            assemblies = new[] { Assembly.GetCallingAssembly() };
        }

        foreach (var assembly in assemblies)
        {
            var types = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && typeof(ISensor).IsAssignableFrom(t));

            foreach (var type in types)
            {
                // Future: check for [Sensor] attribute when we create it
                var instance = (ISensor)Activator.CreateInstance(type)!;
                orchestrator.AddSensor(instance);
            }
        }

        return orchestrator;
    }

    /// <summary>
    /// Simple topological sort to handle DependsOn relationships.
    /// </summary>
    private static List<(Type Type, CapabilityAttribute Attr, int Order)> TopologicalSort(
        List<(Type Type, CapabilityAttribute Attr, int Order)> items)
    {
        var result = new List<(Type, CapabilityAttribute, int)>();
        var visited = new HashSet<Type>();
        var visiting = new HashSet<Type>();
        var lookup = items.ToDictionary(x => x.Type);

        void Visit(Type type)
        {
            if (visited.Contains(type)) return;
            if (visiting.Contains(type))
                throw new InvalidOperationException($"Circular dependency detected involving {type.Name}");

            visiting.Add(type);

            if (lookup.TryGetValue(type, out var item) && item.Attr.DependsOn is not null)
            {
                foreach (var dep in item.Attr.DependsOn)
                {
                    if (lookup.ContainsKey(dep))
                    {
                        Visit(dep);
                    }
                }
            }

            visiting.Remove(type);
            visited.Add(type);

            if (lookup.ContainsKey(type))
            {
                result.Add(lookup[type]);
            }
        }

        foreach (var (type, _, _) in items)
        {
            Visit(type);
        }

        return result;
    }
}
