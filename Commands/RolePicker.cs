using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NelsonsWeirdTwin.Commands
{
    internal class RolePicker : Command
    {
        internal override SlashCommandProperties CommandProperties =>
        new SlashCommandBuilder()
            .WithName("rolepicker")
            .WithDescription("Makes current channel into a role picker channel")
        .AddOption(new SlashCommandOptionBuilder()
            .WithName("set")
            .WithDescription("sets the current channel as the rolepicker embed")
            .WithType(ApplicationCommandOptionType.SubCommand)
        )
        .AddOption(new SlashCommandOptionBuilder()
            .WithName("add")
            .WithDescription("role to add")
            .WithType(ApplicationCommandOptionType.SubCommand)
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("role")
                .WithDescription("Role to add")
                .WithType(ApplicationCommandOptionType.Role)
                .WithRequired(true)
                )
        )
            .AddOption(new SlashCommandOptionBuilder()
            .WithName("remove")
            .WithDescription("Removes a role from the list")
            .WithType(ApplicationCommandOptionType.SubCommand)
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("role")
                .WithDescription("Role to remove")
                .WithType(ApplicationCommandOptionType.Role)
                .WithRequired(true)
                )
        )
        .Build();

        

        public static List<IRole> roles = [];
        public static ulong rolepickerchannel = 0;
        internal override async Task OnExecuted(DiscordSocketClient client, SocketSlashCommand context)
        {
            string subcommand = context.Data.Options.First().Name;
            switch (subcommand)
            {
                case "add":
                    await HandleAdd(context);
                    break;
                case "remove":
                    await HandleRemove(context);
                    break;
                case "set":
                    await HandleSet(context);
                    break;
                default:
                    await context.RespondAsync("Subcommand not implemented.");
                    break;
            }
        }

        public static async Task HandleAdd(SocketSlashCommand context)
        {
            IRole r = context.Data.Options.First().Options.First()?.Value as IRole;
            if (r != null)
            {
                roles.Add(r);
                await context.RespondAsync($"Added <@&{r.Id}> to list!", allowedMentions: AllowedMentions.None);
            }
            else
            {
                await context.RespondAsync("Could not find role!");
            }


        }
        public static async Task HandleRemove(SocketSlashCommand context)
        {
            IRole r = context.Data.Options.First().Options.First()?.Value as IRole;
            if (r != null)
            {
                roles.Remove(r);
                await context.RespondAsync($"Removed <@&{r.Id}>", allowedMentions: AllowedMentions.None);
            }
            else
            {
                await context.RespondAsync("Could not find role!");
            }
        }
        public static async Task HandleSet(SocketSlashCommand context)
        {
            rolepickerchannel = context.Channel.Id;
            EmbedBuilder emb = new EmbedBuilder();
            emb.WithTitle("Role picker");
            emb.WithDescription("Do you plan on making Mono mods, or Il2cpp mods, or both?");
            emb.WithColor(Color.Blue);
            ComponentBuilder cb = new ComponentBuilder();
            SelectMenuBuilder smb = new SelectMenuBuilder()
                .WithCustomId("role-select")
                .WithMinValues(0)
                .WithMaxValues(roles.Count)
                .WithPlaceholder("Select some options");
            foreach (IRole r in roles)
            {
                smb.AddOption($"{r.Name}", $"{r.Id}",$"Gives you the {r.Name} role");
            }
            cb.WithSelectMenu(smb);
            await context.RespondAsync(embed: emb.Build(),components:cb.Build());
        }
    }
}
