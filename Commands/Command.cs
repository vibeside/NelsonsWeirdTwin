using System;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace NelsonsWeirdTwin.Commands;

internal abstract class Command
{
    internal virtual SlashCommandProperties CommandProperties { get; set; }
    internal virtual string[] ModalIDs { get; set; } = [];

    internal async Task RegisterCommand(DiscordSocketClient client, SocketGuild guild = null)
    {

        if (guild != null)
            await guild.CreateApplicationCommandAsync(CommandProperties);
        else
        {
            Console.WriteLine("WARNING: A guild was not provided or not found - commands will not be registered!");
            return;
        }

        Console.WriteLine($"Registered \"{CommandProperties.Name}\".");

    }

    internal virtual async Task OnExecuted(DiscordSocketClient client, SocketSlashCommand context)
    {
        await context.RespondAsync("Not implemented."); // This is called when the command is executed, but OnExecuted wasn't overridden.
    }

    internal virtual async Task OnModalSubmitted(DiscordSocketClient client, SocketModal context)
    {
        await context.RespondAsync("Not implemented."); // This is called when a modal was submit, but OnModalSubmitted wasn't overridden.
    }

    internal virtual async Task OnAutocompleteResultsRequested(DiscordSocketClient client, SocketAutocompleteInteraction context)
    {
        await context.RespondAsync("Not implemented."); // This is called when an autocomplete was requested, but OnAutocompleteResultsRequested wasn't overridden.
    }
}