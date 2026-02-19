using Azure.AI.Agents.Persistent;
using Azure.AI.Projects;
using Azure.Identity;

namespace UtilityAi.Maf;

/// <summary>
/// Client for creating and managing Azure MAF agents.
/// Provides a simple wrapper around Azure OpenAI client initialization.
/// </summary>
public sealed class MafClient
{
    private readonly AIProjectClient _client;
    private readonly string? _modelDeploymentName = System.Environment.GetEnvironmentVariable("MODEL_DEPLOYMENT_NAME");

    /// <summary>
    /// Creates a new MAF client using Azure CLI credentials.
    /// </summary>
    /// <param name="endpoint">Azure OpenAI endpoint URL</param>
    public MafClient(string endpoint)
        : this(new Uri(endpoint), new AzureCliCredential())
    {
    }

    /// <summary>
    /// Creates a new MAF client with custom credentials.
    /// </summary>
    /// <param name="endpoint">Azure OpenAI endpoint URI</param>
    /// <param name="credential">Azure credential (e.g., AzureCliCredential, DefaultAzureCredential)</param>
    public MafClient(Uri endpoint, Azure.Core.TokenCredential credential, string? modelDeploymentName = null)
    {
        _modelDeploymentName =
            modelDeploymentName ?? System.Environment.GetEnvironmentVariable("MODEL_DEPLOYMENT_NAME");
        _client = new AIProjectClient((endpoint), credential);
    }

    public PersistentAgentsClient GetAgentsClient()
    {
        return _client.GetPersistentAgentsClient();
    }

    public PersistentAgent CreateAgent(string name, string instructions)
    {
        // Step 1: Create an agent
        return GetAgentsClient()
            .Administration.CreateAgent(
                model: _modelDeploymentName,
                name: name,
                instructions: instructions
            );
    }
}