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
		if (string.IsNullOrEmpty(upto))
		{
			await context.RespondAsync("You must specify a message ID to purge up to.");
			return;
		}
		
		if (!ulong.TryParse(upto, out var messageId))
		{
			await context.RespondAsync("Invalid message ID. Please provide a valid message ID.");
			return;
		}
		
		var uptoMessage = await context.Channel.GetMessageAsync(messageId);
		if (uptoMessage == null)
		{
			await context.RespondAsync($"Could not find message with ID {upto}. It may be past the {MaxPurgeAmount} message purge limit, or it may not exist.");
			return;
		}

		var messagesToPurge = (await channel.GetMessagesAsync(limit: MaxPurgeAmount).FlattenAsync()).ToList();
		messagesToPurge.RemoveAll(msg => msg.CreatedAt < DateTimeOffset.UtcNow.AddDays(-14)); // removes any messages from the list that are older than 14 days
		messagesToPurge.RemoveAll(msg => msg.CreatedAt <= uptoMessage.CreatedAt); // removes any messages from the list that are older than the specified message
		if (user != null) messagesToPurge.RemoveAll(msg => msg.Author.Id != user.Id); // remove any messages from the list that are not from the specified user

		var purged = messagesToPurge.Count;
		if (purged == 0)
		{
			await context.RespondAsync("No messages to purge.");
			return;
		}

		await channel.DeleteMessagesAsync(messagesToPurge);
		await context.RespondAsync($"Purged {purged} {Utils.Plural(purged, "message", "messages")}.");

		await Task.Delay(2000);
		await context.DeleteOriginalResponseAsync();
	}
}