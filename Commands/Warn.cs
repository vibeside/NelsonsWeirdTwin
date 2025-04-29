using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NelsonsWeirdTwin.Commands
{
    internal class Warn : Command
    {
        // TODO Warn JSON
        // TODO Warn Logic
        internal override SlashCommandProperties CommandProperties =>
            new SlashCommandBuilder()
            .WithName("warn")
            .WithDescription("Warns a user.")
            .AddOption(
                new SlashCommandOptionBuilder()
                .WithName("user")
                .WithDescription("User to warn")
                .WithType(ApplicationCommandOptionType.User)
                .WithRequired(true)
                )
            .Build();
        internal override async Task OnExecuted(DiscordSocketClient client, SocketSlashCommand context)
        {
            await Task.Delay(5);
        }
    }
}
