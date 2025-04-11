using System;
using System.Collections.Generic;
using Discord;
using Discord.WebSocket;
using DotNetEnv;
using Newtonsoft.Json;
using System.Reflection;
using NelsonsWeirdTwin.Commands;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Discord.Interactions;

namespace NelsonsWeirdTwin;

internal static class Program
{
	public static DiscordSocketClient Client;

	public static Dictionary<string, string> TriggersResponsesDict = new();

	private static readonly List<Command> CommandsList = [];

	private static async Task Main()
	{
		Env.Load("token.env");
		await TryLoadTriggers();

		var token = Environment.GetEnvironmentVariable("TOKEN");

        if (string.IsNullOrEmpty(token))
		{
			Console.WriteLine("TOKEN not found in environment variables, or .env file. Please set it and try again.");
			Console.ReadLine(); // wait for response from me to close
			return;
		}

		var config = new DiscordSocketConfig
		{
			GatewayIntents = GatewayIntents.All
		};
		Client = new DiscordSocketClient(config);

		
		Client.Log += message => {
			Console.WriteLine(message.Message);
			return Task.CompletedTask;
		};
		Client.Ready += OnReady;
		
		Client.MessageReceived += MessageReceived;
		
		Client.SlashCommandExecuted += SlashCommandHandler;
		Client.AutocompleteExecuted += AutoCompleteHandler;
		Client.ModalSubmitted += ModalSubmitted;

		await Client.LoginAsync(TokenType.Bot, token);
		await Client.StartAsync();
        while (true)
        {
			switch (Console.ReadLine())
			{
				case "ex":
					await Client.StopAsync();
					await Client.LogoutAsync();

					return;
				case "u":
					await TryLoadTriggers();
					break;
			}
		}
	}

	#region Events
	private static async Task OnReady()
	{
		await LoadCommands();
		Console.WriteLine("Client is ready.");
	}

	private static async Task MessageReceived(SocketMessage msg)
	{
		// if message is sent without attachment in #mod-showoff, remove it
		if(msg.Channel.Id == 1357125993717825667 && msg.Attachments.Count == 0) await msg.DeleteAsync();
		if (msg is not SocketUserMessage || msg.Author is { IsBot: true } or { IsWebhook: true })
		{
			//Console.WriteLine(msg.Type);
			//Console.WriteLine(msg.Author.IsBot);
			//Console.WriteLine(msg.Author.IsWebhook);
			// debugging stuff...
			return;
		}
		
		foreach (var k in TriggersResponsesDict.Keys.Where(k => msg.Content.Contains(k, StringComparison.CurrentCultureIgnoreCase)))
		{
			await msg.Channel.SendMessageAsync(TriggersResponsesDict[k], allowedMentions: AllowedMentions.None);
			return; // only respond to the first trigger found
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

	private static async Task AutoCompleteHandler(SocketAutocompleteInteraction context)
	{
		if(context.User is { IsBot: true } or { IsWebhook: true }) return;
		var commandName = context.Data.CommandName;
		
		foreach (var cmd in CommandsList.Where(cmd => cmd.CommandProperties.Name.Value == commandName))
		{
			await cmd.OnAutocompleteResultsRequested(Client, context);
			return;
		}
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
	#endregion

	internal static async Task<string> AddTriggerAndResponse(string trigger, string response) 
	{
		TriggersResponsesDict.Add(trigger, response);
		await SaveTriggers();
		
		return $"Trigger \"{trigger}\" was added.";
	}

	internal static async Task TryLoadTriggers()
	{
		try
		{
			TriggersResponsesDict =
				JsonConvert.DeserializeObject<Dictionary<string, string>>(await File.ReadAllTextAsync("triggers.json"));
		} catch (FileNotFoundException)
		{
			Console.WriteLine("Triggers file not found. Creating a new one.");
			await SaveTriggers();
		}
		catch (JsonException)
		{
			Console.WriteLine("Triggers file is corrupted. Creating a new one.");
			await SaveTriggers();
		}
		catch (Exception e)
		{
			Console.WriteLine($"An error occurred while loading triggers: {e.Message}");
		}
	}
	
	internal static async Task SaveTriggers()
	{
		await File.WriteAllTextAsync("triggers.json", JsonConvert.SerializeObject(TriggersResponsesDict));
	}
	
	private static async Task LoadCommands()
	{
		var assembly = Assembly.GetExecutingAssembly(); // Get the current assembly...
		var types = assembly.GetTypes() // ...get all types...
			.Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(Command))) // ...and filter them to only include classes that inherit from Command.
			.ToList();
		if (types.Count == 0)
		{
			Console.WriteLine("WARNING: No commands found.");
			return;
		}
		
		foreach (var type in types)
		{
			var command = (Command)Activator.CreateInstance(type); // Create an instance of the command...
			if (command == null) continue;

			if (command.CommandProperties == null)
			{
				Console.WriteLine($"Command \"{type.Name}\" does not have a CommandProperties property. Skipping.");
				continue;
			}
			CommandsList.Add(command); // ...and add it to the list of commands.
		}
		
		Console.WriteLine($"Loaded {CommandsList.Count} command" + (CommandsList.Count != 1 ? "s" : "") + ".");
		
		foreach (var command in CommandsList)
		{
			var guild = Client.GetGuild(1349221936470687764); // S1 Modding
			guild ??= Client.GetGuild(1359858871270637762); // Bot Test Server
			await command.RegisterCommand(Client, guild);
		}
	}
}