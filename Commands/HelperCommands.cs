using Discord;
using Discord.WebSocket;
using NelsonsWeirdTwin.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NelsonsWeirdTwin.Commands
{
    internal class HelperCommands : Command
    {
        internal override SlashCommandProperties CommandProperties =>
        new SlashCommandBuilder()
            .WithName("helper")
            .WithDescription("helpful subcommands")
            .AddOption(
                new SlashCommandOptionBuilder()
                    .WithName("killbot")
                    .WithDescription("Kills the bot MUAHAHAHAHAHA")
                    .WithType(ApplicationCommandOptionType.SubCommand)
            )
            .AddOption(
                new SlashCommandOptionBuilder()
                    .WithName("updatetriggers")
                    .WithDescription("Updates the triggers if they happen to be manually changed.")
                    .WithType(ApplicationCommandOptionType.SubCommand)
            )
            .Build();

        internal async override Task OnExecuted(DiscordSocketClient client, SocketSlashCommand context)
        {
            await context.DeferAsync();
            if (Program.OwnerIDs.Select(id => id).All(id => id != context.User.Id))
            {
                await context.DeleteOriginalResponseAsync();
                return;
            }
            switch (context.Data.Options.First().Name)
            {
                case "killbot":
                    await Killbot(context);
                    break;
                case "updatetriggers":
                    await context.DeleteOriginalResponseAsync();
                    await Program.TryLoadTriggers();
                    break;
            }
            
        }
        internal async Task Killbot(SocketSlashCommand context)
        {
            await context.ModifyOriginalMessageAsync("eugh im dying ah");
            await Program.Client.LogoutAsync();
            await Program.Client.StopAsync();
            Program.SaveTriggers();
            await Task.Delay(3000);
            Environment.Exit(0);
        }
    }
}
