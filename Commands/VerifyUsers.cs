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
        internal override Task OnExecuted(DiscordSocketClient client, SocketSlashCommand context)
        {
            Console.WriteLine("Executing.");
            if (context.Data.Options.First().Value is SocketGuildUser user)
            {
                Console.WriteLine(user.Username); // verify
            }
            return Task.CompletedTask;
        }
    }
}
