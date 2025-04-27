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
using Discord.Rest;

namespace NelsonsWeirdTwin;

internal static class Program
{
	public static DiscordSocketClient Client;

	public static readonly List<Command> CommandsList = [];
	private static Dictionary<ulong,bool> watchList = [];

	private static async Task Main()
	{
		Env.Load("token.env");
		await TriggerCommands.TryLoadTriggers();

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
		Client.Ready += OnReady;
		
		Client.MessageReceived += MessageReceived;

		Client.SlashCommandExecuted += Events.SlashCommandSubmitted;
		Client.ModalSubmitted += Events.ModalSubmitted;

		Client.SelectMenuExecuted += SelectMenuHandler;

		Client.AutocompleteExecuted += Events.AutoCompleteHandler;
		
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
					await TriggerCommands.TryLoadTriggers();
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

    



    #region Events
    private static async Task OnReady()
	{
		await LoadCommands();
        RolePicker.roles.Add(Client.Guilds.First().GetRole(1359944109350981733));
        RolePicker.roles.Add(Client.Guilds.First().GetRole(1359943967810256896));
        Console.WriteLine("Client is ready.");
	}

    private static async Task SelectMenuHandler(SocketMessageComponent c)
	{
		// Gave*
        SocketGuildUser u = c.User as SocketGuildUser;
		if (u == null) await c.RespondAsync("Couldn't find user");
		else
		{
			// Gave*user*the
            // Acceptable roles:
            // Mono
            // Il2Cpp
			List<IRole> chosenRoles = RolePicker.roles.Where(r => c.Data.Values.Contains(r.Id.ToString())).ToList();
			List<IRole> removedRoles = RolePicker.roles.Where(r => !c.Data.Values.Contains(r.Id.ToString())).ToList();
			// foreach role in chosen roles
			string chosenRolesJoined = string.Join(", ", chosenRoles.ConvertAll(x => $"<@&{x.Id}>"));
			string removedRolesJoined = string.Join(", ", removedRoles.ConvertAll(x => $"<@&{x.Id}>"));

			string addedRolesPart = chosenRoles.Count == 0 
				? "Didn't give any roles" 
				: $"Gave {u.Username} the roles {chosenRolesJoined}";

			string removedRolesPart = removedRoles.Count == 0
				? ""
				: $"and removed the roles {removedRolesJoined}";

            string successMessage = $"{addedRolesPart} {removedRolesPart}";
			await u.RemoveRolesAsync(removedRoles);
            await u.AddRolesAsync(chosenRoles);
            await c.RespondAsync(successMessage,ephemeral: true,allowedMentions:AllowedMentions.None);
        }
    }
    private static async Task MessageReceived(SocketMessage msg)
	{
		// if message is sent without attachment in #mod-showoff, remove it
		if (msg is not SocketUserMessage || msg.Author is { IsBot: true } or { IsWebhook: true })
		{
			return;
		}
		
		foreach (var k in TriggerCommands.TriggerItems.Where( // Select any items where...
			        k => k.Aliases.Any( // ...any of the aliases...
				    t => msg.Content.Contains(t, StringComparison.CurrentCultureIgnoreCase) // ...are in the message.
			        ))) {
			await msg.Channel.SendMessageAsync(k.Response, allowedMentions: AllowedMentions.None);
			break;
		}
	}
	

	
    #endregion
    #region Triggers and Command Loading
    
	
	private static async Task LoadCommands()
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
			$"Loaded {CommandsList.Count} {Utils.Plural(CommandsList.Count, "command", "commands")}.");

		foreach (var command in CommandsList)
		{
			var guild = Client.GetGuild(1349221936470687764); // S1 Modding
			guild ??= Client.GetGuild(1359858871270637762); // Bot Test Server

			await command.RegisterCommand(Client, guild);
		}
	}
}
#endregion