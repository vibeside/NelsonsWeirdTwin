using Discord;
using Discord.Commands;
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
        // 28 is shutdown, 69 is update.
        internal static byte[] exitCodes = [69,28];
        internal override SlashCommandProperties CommandProperties =>
        new SlashCommandBuilder()
            .WithName("helper")
            .WithDescription("helpful subcommands")
            .AddOption(
                new SlashCommandOptionBuilder()
                    .WithName("killbot")
                    .WithDescription("Kills the bot MUAHAHAHAHAHA")
                    .WithType(ApplicationCommandOptionType.SubCommand)
                    .AddOption(
                        new SlashCommandOptionBuilder()
                            .WithName("exitcode")
                            .WithDescription("Used to determine what the bot does upon being closed.")
                            .WithType(ApplicationCommandOptionType.Integer)
                            .WithRequired(true)
                            .WithAutocomplete(true)
                    )
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
            if (1295119273030586471 == context.User.Id)
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
            long exitCode = (long)context.Data.Options.First().Options.First().Value;
            await context.ModifyOriginalMessageAsync("eugh im dying ah");
            await Program.Client.LogoutAsync();
            await Program.Client.StopAsync();
            Program.SaveTriggers();
            await Task.Delay(1000);
            Environment.Exit((byte)exitCode);
        }
        internal override async Task OnAutocompleteResultsRequested(DiscordSocketClient client, SocketAutocompleteInteraction context)
        {
            if(context.Data.Current.Name != "exitcode")
            {
                await context.RespondAsync([]);
                return;
            }
            
            await context.RespondAsync(exitCodes.Select(x => new AutocompleteResult(x.ToString(),x)));
            
        }
    }
}
