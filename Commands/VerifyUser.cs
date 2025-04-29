using Discord;
using Discord.WebSocket;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NelsonsWeirdTwin.Commands
{
	internal class VerifyUser : Command
	{
		internal override SlashCommandProperties CommandProperties => 
			new SlashCommandBuilder()
				.WithName("verify")
				.WithDescription("Adds the verified dev role to a user and removes the unverified role.")
				.AddOption(
					"user",
					ApplicationCommandOptionType.User,
					"User to verify",
					isRequired: true
				)
				.Build();
        
		internal override async Task OnExecuted(DiscordSocketClient client, SocketSlashCommand context)
		{
			StringBuilder v = new StringBuilder();
            
            if (context.Data.Options.First().Value is SocketGuildUser user)
			{
				// magic number for unverified
				// 1354831227152109590
				// magic for verified
				// 1357079958954049717
				await user.AddRoleAsync(1357079958954049717);
				await user.RemoveRoleAsync(1354831227152109590);
                v.Append("==================================================================\n");
                v.Append($"Verified {user.GlobalName} ({user.Mention}) at {DateTime.UtcNow}\n");
                v.Append($"Command ran by: {context.User.GlobalName} ({context.User.Mention})\n");
				await context.RespondAsync($"<@{user.Id}> has been marked as verified!", ephemeral: true);
				File.AppendAllText("VerifyLog.txt", v.ToString());
				return;
            }
            
			await context.RespondAsync("Couldn't find user!", ephemeral:true);
		}
	}
}