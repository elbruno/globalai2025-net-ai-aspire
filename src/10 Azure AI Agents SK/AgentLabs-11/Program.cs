using AgentLabs_11;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;

#pragma warning disable SKEXP0001, SKEXP0010, SKEXP0110

const string HostName = "SeachAssistant";
const string HostInstructions = "Search information using the search query provided by the user.";

// read settings from user secrets
var config = new ConfigurationBuilder().AddUserSecrets<Program>().Build();
var deployment = "gpt-4o-mini";
var endpoint = config["AZURE_OPENAI_ENDPOINT"];
var key = config["AZURE_OPENAI_APIKEY"];

var kernel = Kernel.CreateBuilder()
    .AddAzureOpenAIChatCompletion(deployment, endpoint, key)
    .Build();


ChatCompletionAgent agent =
            new()
            {
                Instructions = HostInstructions,
                Name = HostName,
                Kernel = kernel,
                Arguments = new KernelArguments(new AzureOpenAIPromptExecutionSettings() { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() }),
            };


KernelPlugin plugin = KernelPluginFactory.CreateFromType<SearchPlugin>();
agent.Kernel.Plugins.Add(plugin);

ChatHistory chat = [];
// var input = "Who is Bruno Capuano?";
var input = "search online for the top 3 trending products in camping and outdoor activities";

chat.Add(new ChatMessageContent(AuthorRole.User, input));
Console.WriteLine($"# {AuthorRole.User}: '{input}'");

var agentContent = agent.InvokeAsync(chat);

Console.WriteLine($"# {AuthorRole.Assistant}: ");
await foreach (var message in agentContent)
{
    chat.Add(message);
    Console.WriteLine($"# {message.AuthorName}: '{message.Content}'");
}