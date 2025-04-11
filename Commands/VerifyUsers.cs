using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NelsonsWeirdTwin.Commands
{
    internal class VerifyUsers : Command
    {
        internal override SlashCommandProperties CommandProperties =>
        new SlashCommandBuilder()
            .WithName("verifyuser")
            .WithDescription("Verifies user thats input")
            .AddOption(
                "user",
                ApplicationCommandOptionType.User,
                "User to be verified",
                isRequired:true)
            .Build();
        internal async override Task OnExecuted(DiscordSocketClient client, SocketSlashCommand context)
        {
            if (context.Data.Options.First().Value is SocketGuildUser user)
            {
                // magic number for unverified
                // 1354831227152109590
                // magic for verified
                // 1357079958954049717
                await user.AddRoleAsync(1357079958954049717);
                await user.RemoveRoleAsync(1354831227152109590);
                await context.RespondAsync($"<@{user.Id}> has been verified!", ephemeral: true);
                return;
            }
            else
            {
                await context.RespondAsync("Couldn't find user!",ephemeral:true);
            }
            
        }
    }
}
