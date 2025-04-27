using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NelsonsWeirdTwin
{
    internal class Events
    {
        public static async Task SlashCommandSubmitted(SocketSlashCommand command)
        {
            if (command.User is { IsBot: true } or { IsWebhook: true }) return;
            var commandName = command.Data.Name;

            foreach (var cmd in Program.CommandsList.Where(cmd => cmd.CommandProperties.Name.Value == commandName))
            {
                try
                {
                    await cmd.OnExecuted(Program.Client, command);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"An error occurred while executing command \"{commandName}\"!");
                    Console.WriteLine(e);

                    await command.ModifyOriginalResponseAsync(properties =>
                    {
                        properties.Content = $"An error occurred while executing command \"{commandName}\":\n```\n{e.Message}\n```";
                    });
                }
                return;
            }

            await command.RespondAsync($"Command \"{commandName}\" not found. What the flip flop is happening here?!");
        }

        public static async Task AutoCompleteHandler(SocketAutocompleteInteraction context)
        {
            if (context.User is { IsBot: true } or { IsWebhook: true }) return;
            var commandName = context.Data.CommandName;

            foreach (var cmd in Program.CommandsList.Where(cmd => cmd.CommandProperties.Name.Value == commandName))
            {
                await cmd.OnAutocompleteResultsRequested(Program.Client, context);
                return;
            }
        }
        public static async Task ModalSubmitted(SocketModal modal)
        {
            if (modal.User is { IsBot: true } or { IsWebhook: true }) return;

            foreach (var cmd in Program.CommandsList.Where(cmd => cmd.ModalIDs.Contains(modal.Data.CustomId)))
            {
                await cmd.OnModalSubmitted(Program.Client, modal);
                return;
            }

            await modal.RespondAsync($"Modal {modal.Data.CustomId} not found!");
        }
    }
}
