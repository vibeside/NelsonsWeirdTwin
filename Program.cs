using Discord;
using Discord.WebSocket;
using DotNetEnv;
using Newtonsoft.Json;
using System.Reflection;
using NelsonsWeirdTwin.Commands;

namespace NelsonsWeirdTwin;

internal static class Program
{
	private static DiscordSocketClient Client;

	public static Dictionary<string, string> TriggersResponsesDict = new();

	private static readonly List<Command> CommandsList = [];

	private static async Task Main()
	{
		Env.Load();
		try
		{
			TriggersResponsesDict =
				JsonConvert.DeserializeObject<Dictionary<string, string>>(await File.ReadAllTextAsync("triggers.json"));
		}
		catch (Exception e)
		{
			Console.WriteLine($"triggers.json not found, or some other error occured. creating a new one. ({e.GetType().Name})");

			TriggersResponsesDict = new Dictionary<string, string>();
			TriggersResponsesDict.Add("test", "test content");
			TriggersResponsesDict.Add("test2", "test2 content");
			await File.WriteAllTextAsync("triggers.json", JsonConvert.SerializeObject(TriggersResponsesDict));
		}

		var token = Environment.GetEnvironmentVariable("TOKEN");
		if (string.IsNullOrEmpty(token))
		{
			Console.WriteLine("TOKEN not found in environment variables, or .env file. Please set it and try again.");
			return;
		}

		var config = new DiscordSocketConfig
		{
			GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent
		};
		Client = new DiscordSocketClient(config);
		Client.Log += message => {
			Console.WriteLine(message.Message);
			return Task.CompletedTask;
		};
		Client.Ready += Client_Ready;
		
		Client.MessageReceived += MessageReceived;
		
		Client.SlashCommandExecuted += SlashCommandHandler;
		Client.ModalSubmitted += ModalSubmitted;

		await Client.LoginAsync(TokenType.Bot, token);
		await Client.StartAsync();
		
		var running = true;
		while (running)
		{
			switch (Console.ReadLine())
			{
				case "ex":
					await Client.StopAsync();
					await Client.LogoutAsync();
					
					running = false;
					break;
				case "u":
					TriggersResponsesDict = JsonConvert.DeserializeObject<Dictionary<string, string>>(await File.ReadAllTextAsync("triggers.json"));
					break;
			}
		}
	}

	private static async Task Client_Ready()
	{
		await LoadCommands();
		Console.WriteLine("Client is ready.");
	}

	private static async Task MessageReceived(SocketMessage msg)
	{
		if (msg is not SocketUserMessage || msg.Author is { IsBot: true } or { IsWebhook: true })
		{
			Console.WriteLine(msg.Type);
			Console.WriteLine(msg.Author.IsBot);
			Console.WriteLine(msg.Author.IsWebhook);
			return;
		};
		
		foreach (var k in TriggersResponsesDict.Keys.Where(k => msg.Content.Contains(k)))
		{
			await msg.Channel.SendMessageAsync(TriggersResponsesDict[k]);
		}
	}

	internal static Task<string> AddTriggerAndResponse(string trigger, string response) 
	{
		var fixedResponse = response
			.Replace(@"\\","\\")
			.Replace("\\n","\n");
		
		TriggersResponsesDict.Add(trigger, fixedResponse);
		SaveTriggers();
		
		return Task.FromResult($"Trigger \"{trigger}\" was added.");
	}
	
	internal static Task SaveTriggers()
	{
		File.WriteAllText("triggers.json", JsonConvert.SerializeObject(TriggersResponsesDict));
		return Task.CompletedTask;
	}
	
	private static async Task LoadCommands()
	{
		var assembly = Assembly.GetExecutingAssembly();
		var types = assembly.GetTypes()
			.Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(Command)))
			.ToList();
		if (types.Count == 0)
		{
			Console.WriteLine("WARNING: No commands found.");
			return;
		}
		
		foreach (var type in types)
		{
			var command = (Command)Activator.CreateInstance(type);
			if (command == null) continue;

			if (command.CommandProperties == null)
			{
				Console.WriteLine($"Command \"{type.Name}\" does not have a CommandProperties property. Skipping.");
				continue;
			}
			CommandsList.Add(command);
		}
		
		Console.WriteLine($"Loaded {CommandsList.Count} command" + (CommandsList.Count != 1 ? "s" : "") + ".");
		
		foreach (var command in CommandsList)
		{
			var guild = Client.GetGuild(1349221936470687764) ?? Client.GetGuild(1359858871270637762);
			await command.RegisterCommand(Client, guild);
		}
	}

	private static async Task SlashCommandHandler(SocketSlashCommand command)
	{
		if(command.User is { IsBot: true } or { IsWebhook: true }) return;
		var commandName = command.Data.Name;
		
		foreach (var cmd in CommandsList.Where(cmd => cmd.CommandProperties.Name.Value == commandName))
		{
			await cmd.OnExecuted(Client, command);
			return;
		}
		
		await command.RespondAsync($"Command \"{commandName}\" not found. What the flip flop is happening here?!");
	}
	
	private static async Task ModalSubmitted(SocketModal modal)
	{
		if (modal.User is { IsBot: true } or { IsWebhook: true }) return;
		
		foreach (var cmd in CommandsList.Where(cmd => cmd.ModalIDs.Contains(modal.Data.CustomId)))
		{
			await cmd.OnModalSubmitted(Client, modal);
			return;
		}
		
		await modal.RespondAsync($"Modal \"{modal.Data.CustomId}\" not found!!!!");
	}
}