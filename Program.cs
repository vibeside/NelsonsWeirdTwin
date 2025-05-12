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
		Directory.SetCurrentDirectory(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
		Env.Load("token.env");
		await TryLoadTriggers();
		string token;
#if PROD
		token = Environment.GetEnvironmentVariable("TOKEN");
#elif TEST
		token = Environment.GetEnvironmentVariable("TESTTOKEN");
#endif
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
		Client.UserBanned += Events.OnUserBanned;

		Client.SlashCommandExecuted += Events.SlashCommandSubmit;
		Client.ModalSubmitted += Events.ModalSubmit;
		Client.SelectMenuExecuted += Events.SelectMenuHandler;
		Client.AutocompleteExecuted += Events.AutoCompleteHandler;
		
		await Client.LoginAsync(TokenType.Bot, token);
		await Client.StartAsync();

		await Task.Delay(-1);
	}
	
	#region Triggers and Command Loading
	internal static async Task AddNewTrigger(TriggerItem trigger) 
	{
		TriggerItems.Add(trigger);
		await SaveTriggers();
	}
	internal static async Task RewriteWarns(List<WarnItem> warns)
	{
		await File.WriteAllTextAsync("warns.json",JsonConvert.SerializeObject(warns));
	}
    internal static async ValueTask<List<WarnItem>> TryLoadWarns()
    {
        // shouldnt error, but it may
        List<WarnItem> l;
		ITextChannel c = null;
#if PROD
		c ??=  await Client.GetChannelAsync(1360653812829913128) as ITextChannel; // Main server
#elif TEST
        c ??= await Client.GetChannelAsync(1368263313531732011) as ITextChannel; // Test server id
#endif
        try
        {
            l = JsonConvert.DeserializeObject<List<WarnItem>>(await File.ReadAllTextAsync("warns.json"));
			
        }
        catch (FileNotFoundException)
        {
            //Avoid creating a new warns.json in case the file does exist in the wrong location.
            //c.SendMessageAsync("Could not retrieve warns file. <@939127707034333224> recommend SSHing and checking.");
            Console.WriteLine("Could not retrieve warns file.");
            return null;
        }

        catch (JsonException)
        {
            //Again, avoid recreating just in case.
            //c.SendMessageAsync("Could not read warns.json. <@939127707034333224>");
            Console.WriteLine("Could not read warns.json");
            return null;
        }

        catch (Exception ex)
        {
            //c.SendMessageAsync("<@939127707034333224> Bot encountered error:" + ex.ToString());
            Console.WriteLine(ex.ToString());
            return null;
        }
        return l;
    }
	internal static async Task AddWarn(ulong user,Warn warn)
	{
		List<WarnItem> listOfWarns = await TryLoadWarns();
		WarnItem userWarn = listOfWarns.FirstOrDefault(x => x.User == user);
		//If the user doesnt have a recorded warn
		if (userWarn == null)
		{
			// make a new one
			userWarn = new WarnItem()
			{
				User = user,
				ExpiredWarns = 0
			};
			// add it to the full list of warns we just made
			listOfWarns.Add(userWarn);
		}
        userWarn.CurrentWarns.Add(warn);
		await File.WriteAllTextAsync("warns.json",JsonConvert.SerializeObject(listOfWarns,Formatting.Indented));
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
			$"Found {CommandsList.Count} {Utils.Plural(CommandsList.Count, "command")}.");

		var guild = Client.GetGuild(1349221936470687764); // S1 Modding
		guild ??= Client.GetGuild(1359858871270637762); // Bot Test Server
		guild ??= Client.GetGuild(1368263313531732008); // Pacas test server

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