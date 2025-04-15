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

	public static List<TriggerItem> TriggerItems = [];
	private static readonly List<Command> CommandsList = [];

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
		Client.Ready += OnReady;
		
		Client.MessageReceived += MessageReceived;

		Client.SlashCommandExecuted += SlashCommandSubmitted;
		Client.ModalSubmitted += ModalSubmitted;

		Client.SelectMenuExecuted += SelectMenuHandler;

		Client.AutocompleteExecuted += AutoCompleteHandler;
		
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
		if(msg.Channel.Id == 1357125993717825667 )
		{
			// move inside here so i can do an else instead of an else if
			// delete if no attachment or embed
			if (msg.Attachments.Count == 0 && msg.Embeds.Count == 0)
			{
				await Task.Delay(10000); // wait 10 seconds, then recheck the embeds count
				if (msg.Embeds.Count == 0)
				{
					await msg.DeleteAsync();
				}
			}
			else
			{
				if (msg.Channel is ITextChannel c)
				{
					await c.CreateThreadAsync($"{msg.Author.Username}'s mod showoff thread.", message: msg);
				}
				else
				{
					await msg.Channel.SendMessageAsync("Shits null bud.");
				}
			}
			
		}
		else if (msg is not SocketUserMessage || msg.Author is { IsBot: true } or { IsWebhook: true })
		{
			return;
		}
		
		foreach (var k in TriggerItems.Where( // Select any items where...
			        k => k.Aliases.Any( // ...any of the aliases...
				    t => msg.Content.Contains(t, StringComparison.CurrentCultureIgnoreCase) // ...are in the message.
			        ))) {
			await msg.Channel.SendMessageAsync(k.Response, allowedMentions: AllowedMentions.None);
			break;
		}
	}
	private static async Task SlashCommandSubmitted(SocketSlashCommand command)
	{
		if(command.User is { IsBot: true } or { IsWebhook: true }) return;
		var commandName = command.Data.Name;
		
		foreach (var cmd in CommandsList.Where(cmd => cmd.CommandProperties.Name.Value == commandName))
		{
			try
			{
				await cmd.OnExecuted(Client, command);
			} catch (Exception e)
			{
				Console.WriteLine($"An error occurred while executing command \"{commandName}\"!");
				Console.WriteLine(e);
				
				await command.ModifyOriginalResponseAsync(properties =>
				{
					properties.Content = $"An error occurred while executing command \"{commandName}\":\n```\n{e.Message}\n```";
				});
			}
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
		
		await modal.RespondAsync($"Modal {modal.Data.CustomId} not found!");
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
    #endregion
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