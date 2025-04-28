using Discord;
using Discord.WebSocket;
using System.Linq;
using System.Threading.Tasks;

namespace NelsonsWeirdTwin.Commands
{
	internal class ReregisterCommandsCommand : Command
	{
		internal override SlashCommandProperties CommandProperties => 
			new SlashCommandBuilder()
				.WithName("re-register")
				.WithDescription("Sometimes the bot's commands can fuck up and duplicate. This command fixes that.")
				.Build();
        
		internal override async Task OnExecuted(DiscordSocketClient client, SocketSlashCommand context)
		{
			await context.DeferAsync();
			if (Program.OwnerIDs.Select(id => (ulong)id).All(id => id != context.User.Id))
			{
				await context.DeleteOriginalResponseAsync();
				return;
			}
			
			await Program.LoadCommands(true);
		}
	}
}