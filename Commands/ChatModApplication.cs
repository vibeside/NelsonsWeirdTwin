using Discord;
using Discord.WebSocket;
using System.Threading.Tasks;

namespace NelsonsWeirdTwin.Commands
{
    internal class ChatModApplication : Command
    {
        internal override SlashCommandProperties CommandProperties =>
        new SlashCommandBuilder()
            .WithName("chatmod")
            .WithDescription("Makes current channel into a role picker channel")
        .Build();

        internal override async Task OnExecuted(DiscordSocketClient client, SocketSlashCommand context)
        {
            var cb = new ComponentBuilder();
            cb.WithButton(
                new ButtonBuilder()
                .WithCustomId("vouch")
                .WithLabel("I vouch!")
                .WithEmote(Emote.Parse("ducky"))
            );
            cb.WithButton(
                new ButtonBuilder()
                .WithCustomId("anti-vouch")
                .WithLabel("I don't vouch!")
                .WithEmote(Emote.Parse("AntiDucky"))
            );
            await context.RespondAsync(components: cb.Build());
        }


        public static async Task HandleVouch(SocketMessageComponent c)
        {

        }
        public static async Task HandleAntiVouch(SocketMessageComponent c)
        {

        }
    }
}
