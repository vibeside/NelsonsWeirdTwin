using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace NelsonsWeirdTwin.Commands;

internal class PurgeCommand : Command
{
    private const int MaxPurgeAmount = 200;

    internal override SlashCommandProperties CommandProperties =>
    new SlashCommandBuilder()
        .WithName("purge")
        .WithDescription("Purge messages from a channel.")
        .AddOption(
    new SlashCommandOptionBuilder()
        .WithName("upto")
        .WithDescription("Delete up to a certain message ID.")
        .WithType(ApplicationCommandOptionType.String)
        .WithRequired(true)
    )
    .AddOption(
    new SlashCommandOptionBuilder()
        .WithName("from")
        .WithDescription("Delete only a specific user's messages.")
        .WithType(ApplicationCommandOptionType.User)
        .WithRequired(false)
    )
    .Build();

    internal override async Task OnExecuted(DiscordSocketClient client, SocketSlashCommand context)
    {
        var upto = context.Data.Options.FirstOrDefault(opt => opt.Name == "upto")?.Value as string;
        var user = context.Data.Options.FirstOrDefault(opt => opt.Name == "from")?.Value as IUser;
        if (context.Channel is not ITextChannel channel)
        {
            await context.RespondAsync("This command can only be used in text channels.");
            return;
        }

        if (upto is null)
        {
            await context.RespondAsync("You must specify a message ID to purge up to.");
            return;
        }

        var purged = 0;
        var messages = (await channel.GetMessagesAsync(limit: MaxPurgeAmount).FlattenAsync()).ToList();

        var uptoMessage = messages.FirstOrDefault(m => m.Id.ToString() == upto);
        if (uptoMessage == null)
        {
            await context.RespondAsync($"Could not find message with ID {upto}. It may be past the {MaxPurgeAmount} message purge limit, or it may not exist.");
            return;
        }

        if (user != null) messages.RemoveAll(m => m.Author.Id != user.Id);
        messages.RemoveAll(m => m.CreatedAt > DateTimeOffset.UtcNow.AddDays(-14)); // Discord only allows purging messages from the last 14 days.
        messages.RemoveAll(m => m.CreatedAt > uptoMessage.CreatedAt); // Remove all messages after the specified message.

        purged = messages.Count;
        if (purged == 0)
        {
            await context.RespondAsync("No messages to purge.");
            return;
        }

        await channel.DeleteMessagesAsync(messages);
        await context.RespondAsync($"Purged {purged} {Utils.Plural(purged, "message", "messages")}.");

        await Task.Delay(2000);
        await context.DeleteOriginalResponseAsync();
    }
}