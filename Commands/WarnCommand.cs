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
                    .AddOption(
                        new SlashCommandOptionBuilder()
                            .WithName("user")
                            .WithDescription("User to warn")
                            .WithType(ApplicationCommandOptionType.User)
                            .WithRequired(true)
                    )
                    .AddOption(
                        new SlashCommandOptionBuilder()
                            .WithName("reason")
                            .WithDescription("Reason for warn")
                            .WithType(ApplicationCommandOptionType.String)
                            .WithRequired(true)
                        )
                )
            .AddOption(
                new SlashCommandOptionBuilder()
                    .WithName("list")
                    .WithDescription("List your own warns")
                    .WithType(ApplicationCommandOptionType.SubCommand)
                    .AddOption(
                        new SlashCommandOptionBuilder()
                            .WithName("user")
                            .WithDescription("User to list warns for.")
                            .WithType(ApplicationCommandOptionType.User)
                            .WithRequired(false)
                    )
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
            var actualContext = context.Data.Options.FirstOrDefault(x => x.Name == "add");
            IUser user = actualContext.Options.FirstOrDefault(x => x.Name == "user").Value as IUser;
            string reason = actualContext.Options.FirstOrDefault(x => x.Name == "reason").Value as string;
            await Program.AddWarn(user.Id, new Warn()
            {
                Reason = reason,
                Timestamp = DateTime.UtcNow,
                IssuerID = context.User.Id
            });
            await context.RespondAsync($"Warned {user.Username} for {reason}");

        }
        internal static async Task HandleList(SocketSlashCommand context)
        {
            var actualContext = context.Data.Options.FirstOrDefault(x => x.Name == "list");
            IUser user = actualContext.Options.FirstOrDefault(x => x.Name == "user")?.Value as IUser;
            user ??= context.User;
            // get the warns for the user thats trying to list.
            WarnItem userWarn = (await Program.TryLoadWarns()).FirstOrDefault(x => x.User == user.Id);
            if (userWarn == null)
            {
                await context.RespondAsync("You've been a good boy! no warns!");
                return;
            }
            EmbedBuilder eb = new EmbedBuilder()
            .WithAuthor(user)
            .WithColor(Utils.RandColor(user.Id))
            .WithTitle($"Warns for <@{user.Id}")
            .WithFooter("naughty naughty little guy")
            .WithDescription($"You have {userWarn.CurrentWarns.Count} {Utils.Plural(userWarn.CurrentWarns.Count, "warn")}");
            foreach (Warn warn in userWarn.CurrentWarns)
            {
                eb.AddField(
                    new EmbedFieldBuilder()
                    .WithName($"Issued by:<@{warn.IssuerID}>")
                    .WithValue($"Reason:{warn.Reason}")
                    );
            }
            await context.RespondAsync(embed: eb.Build(), ephemeral: true);
        }
    }
}
