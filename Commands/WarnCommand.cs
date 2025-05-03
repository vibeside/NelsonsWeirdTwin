using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NelsonsWeirdTwin.Commands
{
    internal class WarnCommand : Command
    {
        // TODO Warn JSON
        // TODO Warn Logic
        internal override SlashCommandProperties CommandProperties =>
            new SlashCommandBuilder()
            .WithName("warn")
            .WithDescription("Warns a user.")
            .AddOption(
                new SlashCommandOptionBuilder()
                    .WithName("add")
                    .WithDescription("Adds a warn to a specified user")
                    .WithType(ApplicationCommandOptionType.SubCommand)
                    .WithRequired(true).AddOption(
                        new SlashCommandOptionBuilder()
                            .WithName("user")
                            .WithDescription("User to warn")
                            .WithType(ApplicationCommandOptionType.User)
                            .WithRequired(true)
                        )
                )
            .AddOption(
                new SlashCommandOptionBuilder()
                    .WithName("list")
                    .WithDescription("List your own warns")
                    .WithType(ApplicationCommandOptionType.SubCommand)
                    .WithRequired(true)
                )
            .Build();
        internal override async Task OnExecuted(DiscordSocketClient client, SocketSlashCommand context)
        {
            var option = context.Data.Options.First().Name; // Get the first option, which is the subcommand.
            switch (option)
            {
                case "add":
                    HandleAdd(context);
                    break;
                case "list":
                    HandleList(context);
                    break;
                default:
                    await context.RespondAsync("Could not find subcommand.");
                    break;
            }
        }
        internal static async Task HandleAdd(SocketSlashCommand context)
        {
            var user = context.Data.Options.First().Options.First().Value as IUser;
        }
        internal static async Task HandleList(SocketSlashCommand context)
        {
            // get the warns for the user thats trying to list.
            WarnItem userWarn = (await Program.TryLoadWarns()).FirstOrDefault(x => x.User == context.User.Id);
            
            EmbedBuilder eb = new EmbedBuilder()
            .WithAuthor(context.User)
            .WithDescription("Warns for user");
            foreach(Warn warn in userWarn.CurrentWarns)
            {
                eb.AddField(
                    new EmbedFieldBuilder()
                    .WithName($"Issued by:<@{warn.IssuerID}>")
                    );
            }
        }
    }
}
