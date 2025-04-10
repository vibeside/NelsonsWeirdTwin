using Discord;
using Discord.WebSocket;

namespace NelsonsWeirdTwin.Commands;

internal class PingModTesters: Command
{
	internal override SlashCommandProperties CommandProperties =>
		new SlashCommandBuilder()
			.WithName("pingmodtesters")
			.WithDescription("Pings the Mod Testers role.")
			.Build();

	internal override async Task OnExecuted(DiscordSocketClient client, SocketSlashCommand context)
	{
		await context.RespondAsync("Pinging Mod Testers in 10sec...");
		
		await Task.Delay(10 * 1000);
		await context.DeleteOriginalResponseAsync();
		await context.FollowupAsync("<@&1356043224510103692>");
	}
}