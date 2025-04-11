using Discord;
using Discord.WebSocket;

namespace NelsonsWeirdTwin.Commands;

internal class PingModTesters: Command
{
	internal override SlashCommandProperties CommandProperties => // This holds command properties, you can use SlashCommandBuilder to build it
		new SlashCommandBuilder()
			.WithName("pingmodtesters")
			.WithDescription("Pings the Mod Testers role.")
			.Build();

	internal override async Task OnExecuted(DiscordSocketClient client, SocketSlashCommand context) // This is called when the command is executed.
	{ 
		await context.RespondAsync("<@&1356043224510103692>"); // We just respond with a mention to the Mod Testers role.
	}
}