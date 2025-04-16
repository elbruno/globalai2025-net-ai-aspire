using Azure;
using Azure.AI.Projects;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace AgentLabs_11;

public sealed class SearchPlugin
{
    [KernelFunction, Description("Search by Bing")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1024:Use properties where appropriate", Justification = "Too smart")]
    public async Task<string> Search([Description("search Item")]
            string searchItem)
    {
        // read settings from user secrets
        var config = new ConfigurationBuilder().AddUserSecrets<Program>().Build();
        var cnnstring = config["aifoundryproject_cnnstring"];
        var tenantid = config["aifoundryproject_tenantid"];
        var searchagentid = config["aifoundryproject_searchagentid"];
        var bingsearchconnectionName = config["aifoundryproject_groundingcnnname"];

        searchagentid = "asst_ajqadWtAfwXHhRn19cIuZx1U"; // simple agent
        //searchagentid = "asst_mxgEKRrdjXjp12lDPPF8WoPl"; // spanish agent
        //searchagentid = "asst_1ztS08BMlBRTZdbxjbUdgkXg"; // online researcher agent
        //searchagentid = "asst_hHsTsNkLntry3yxkE3zqEiy6"; // warhammer eshoplite agent

        // Adding the custom headers policy
        var clientOptions = new AIProjectClientOptions();
        clientOptions.AddPolicy(new CustomHeadersPolicy(), HttpPipelinePosition.PerCall);

        // create credential
        var options = new DefaultAzureCredentialOptions();
        if (!string.IsNullOrEmpty(tenantid))
            options.TenantId = tenantid;
        AIProjectClient projectClient = new AIProjectClient(cnnstring, new DefaultAzureCredential(options), clientOptions);


        AgentsClient agentClient = projectClient.GetAgentsClient();
        Agent searchOnlineAgent = null;

        if (string.IsNullOrEmpty(searchagentid))
        {
            string connectionId = "";
            var tools = new List<ToolDefinition>();

            if (!string.IsNullOrEmpty(bingsearchconnectionName))
            {
                ConnectionResponse bingConnection = await projectClient.GetConnectionsClient().GetConnectionAsync(bingsearchconnectionName);
                connectionId = bingConnection.Id;
                ToolConnectionList connectionList = new ToolConnectionList
                {
                    ConnectionList = { new ToolConnection(connectionId) }
                };
                BingGroundingToolDefinition bingGroundingTool = new BingGroundingToolDefinition(connectionList);
                tools.Add(bingGroundingTool);
            }

            var agentResponse = agentClient.CreateAgent(
                model: "gpt-4-1106-preview",
                name: "my-assistant",
                instructions: "You are a helpful assistant that search online for information.",
                tools: tools);
            searchOnlineAgent = agentResponse.Value;
        }
        else
        {
            searchOnlineAgent = agentClient.GetAgent(searchagentid).Value;
        }

        // Create thread for communication
        var threadResponse = await agentClient.CreateThreadAsync();
        AgentThread thread = threadResponse.Value;

        // Create message to thread
        var messageResponse = await agentClient.CreateMessageAsync(
            thread.Id,
            MessageRole.User,
            $"{searchItem}");
        ThreadMessage message = messageResponse.Value;

        // Run the agent
        var runResponse = await agentClient.CreateRunAsync(thread, searchOnlineAgent);

        do
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500));
            runResponse = await agentClient.GetRunAsync(thread.Id, runResponse.Value.Id);
        }
        while (runResponse.Value.Status == RunStatus.Queued
            || runResponse.Value.Status == RunStatus.InProgress);

        var afterRunMessagesResponse = await agentClient.GetMessagesAsync(thread.Id);
        var messages = afterRunMessagesResponse.Value.Data;

        string searchResult = "";
        Console.WriteLine("==========================");
        Console.WriteLine($"Search for '{searchItem}'");
        foreach (ThreadMessage threadMessage in messages)
        {
            Console.WriteLine($"{threadMessage.CreatedAt:yyyy-MM-dd HH:mm:ss} - {threadMessage.Role,10}: ");
            if (threadMessage.Role.ToString().ToLower() == "assistant")
            {
                foreach (MessageContent contentItem in threadMessage.ContentItems)
                {
                    if (contentItem is MessageTextContent textItem)
                    {
                        searchResult += textItem.Text + "\n";
                    }
                }
            }
        }
        Console.WriteLine($"Search result:");
        Console.WriteLine(searchResult);
        Console.WriteLine("==========================");

        return searchResult;
    }
}