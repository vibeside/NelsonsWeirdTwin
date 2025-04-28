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

namespace NelsonsWeirdTwin;

// TODO:
// Warns JSONDB
// Warn clearing when banned into discord chat(ID:1366470152727560233)
// Expired warns var
// user specific commands

internal static class Program
{
	public static bool BotActive = true;
	public static DiscordSocketClient Client;

	public static List<TriggerItem> TriggerItems = [];
	internal static readonly List<Command> CommandsList = [];
	internal static readonly List<ulong> WatchList = [];

	internal static readonly ulong[] OwnerIDs =
	[
		939127707034333224, // @stupidrepo
		1295119273030586471, // @coolpaca
	];

	internal static readonly ulong[] IgnoredRoleIds =
	[
		1366191861194293279,
		1366213334587801650
	];
	
	private static async Task Main()
	{
		Env.Load("token.env");
		await TryLoadTriggers();

		var token = Environment.GetEnvironmentVariable("TOKEN");

		if (string.IsNullOrEmpty(token))
		{
			Console.WriteLine("TOKEN not found in environment variables, or .env file. Please set it and try again.");
			Console.ReadLine(); // Pull a bash "pause" fr
			return;
		}

		var config = new DiscordSocketConfig
		{
			GatewayIntents = GatewayIntents.All
		};
		Client = new DiscordSocketClient(config);

		Client.Log += message =>
		{
			Console.WriteLine(message.Message);
			return Task.CompletedTask;
		};
		Client.Ready += Events.OnReady;
		
		Client.MessageReceived += Events.MessageReceived;

		Client.SlashCommandExecuted += Events.SlashCommandSubmit;
		Client.ModalSubmitted += Events.ModalSubmit;
		Client.SelectMenuExecuted += Events.SelectMenuHandler;
		Client.AutocompleteExecuted += Events.AutoCompleteHandler;
		
		await Client.LoginAsync(TokenType.Bot, token);
		await Client.StartAsync();
		
		while (BotActive)
		{
			switch (Console.ReadLine())
			{
				case "ex":
					BotActive = false;
					await Client.StopAsync();
					await Client.LogoutAsync();
					SaveTriggers();
					break;
				case "u":
					await TryLoadTriggers();
					break;
				case "c":
					Console.Clear();
					break;
				default:
					Console.WriteLine("Arg not recognized");
					break;
			}
		}
	}
	
	#region Triggers and Command Loading
	internal static async Task AddNewTrigger(TriggerItem trigger) 
	{
		TriggerItems.Add(trigger);
		await SaveTriggers();
	}

	internal static async Task TryLoadTriggers()
	{
		try
		{
			TriggerItems =
				JsonConvert.DeserializeObject<List<TriggerItem>>(await File.ReadAllTextAsync("triggers.json"));
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
		await File.WriteAllTextAsync("triggers.json", JsonConvert.SerializeObject(TriggerItems, Formatting.Indented));
	}

	internal static async Task LoadCommands(bool reregister = false)
	{
		var assembly = Assembly.GetExecutingAssembly(); // Get the current assembly...
		var types = assembly.GetTypes() // ...get all types...
			.Where(t => t.IsClass && !t.IsAbstract &&
			            t.IsSubclassOf(
				            typeof(Command))) // ...and filter them to only include classes that inherit from Command.
			.ToList();
		if (types.Count == 0)
		{
			Console.WriteLine("WARNING: No commands found.");
			return;
		}

		CommandsList.Clear();
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

		Console.WriteLine(
			$"Found {CommandsList.Count} {Utils.Plural(CommandsList.Count, "command", "commands")}.");

		var guild = Client.GetGuild(1349221936470687764); // S1 Modding
		guild ??= Client.GetGuild(1359858871270637762); // Bot Test Server

		if (reregister)
		{
			await Client.BulkOverwriteGlobalApplicationCommandsAsync([]);
			await guild.DeleteApplicationCommandsAsync();
			
			Console.WriteLine("Deleted all commands. Re-registering in 5 seconds...");
			await Task.Delay(5000);
		}
		
		await guild.BulkOverwriteApplicationCommandAsync(CommandsList.Select(c => c.CommandProperties).ToArray<ApplicationCommandProperties>());
		Console.WriteLine($"Finished registering {CommandsList.Count} {Utils.Plural(CommandsList.Count, "command", "commands")}!");
	}
}
#endregion