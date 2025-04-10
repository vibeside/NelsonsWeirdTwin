using Discord;
using Discord.WebSocket;

namespace NelsonsWeirdTwin.Commands;

abstract class Command
{
	internal virtual SlashCommandProperties CommandProperties { get; set; }
	internal virtual string[] ModalIDs { get; set; } = [];

	internal async Task RegisterCommand(DiscordSocketClient client, SocketGuild guild = null)
	{
		if (guild != null)
			await guild.CreateApplicationCommandAsync(CommandProperties);
		else
		{
			Console.WriteLine("WARNING: Registering global commands because a guild was not provided.");
			await client.CreateGlobalApplicationCommandAsync(CommandProperties);
		}
		
		Console.WriteLine($"Registered \"{CommandProperties.Name}\".");
	}

	internal virtual async Task OnExecuted(DiscordSocketClient client, SocketSlashCommand context)
	{
		await context.RespondAsync("Not implemented.");
	}
	
	internal virtual async Task OnModalSubmitted(DiscordSocketClient client, SocketModal context)
	{
		await context.RespondAsync("Not implemented.");
	}
}