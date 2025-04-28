using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NelsonsWeirdTwin.Commands
{
    internal class KillBot : Command
    {
        internal override SlashCommandProperties CommandProperties =>
        new SlashCommandBuilder()
            .WithName("killbot")
            .WithDescription("KILL THE BOT MUAHAHAHAHAHA")
            .Build();

        internal async override Task OnExecuted(DiscordSocketClient client, SocketSlashCommand context)
        {
            await context.DeferAsync();
            if (Program.OwnerIDs.Select(id => id).All(id => id != context.User.Id))
            {
                await context.DeleteOriginalResponseAsync();
                return;
            }
            await context.RespondAsync("eugh im dying ah");
            Program.BotActive = false;
            await Program.Client.StopAsync();
            await Program.Client.LogoutAsync();
            Program.SaveTriggers();
        }
    }
}
