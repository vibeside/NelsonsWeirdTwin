using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace NelsonsWeirdTwin.Commands;

internal class PurgeCommand: Command
{
	private const int MaxPurgeAmount = 200;
	
	internal override SlashCommandProperties CommandProperties =>
		new SlashCommandBuilder()
			.WithName("purge")
			.WithDescription("Purge messages from a channel.")
			.AddOption(
				new SlashCommandOptionBuilder()
					.WithName("amount")
					.WithDescription("The amount of messages to purge.")
					.WithType(ApplicationCommandOptionType.Integer)
					.WithRequired(false)
					.WithMinValue(1)
					.WithMaxValue(MaxPurgeAmount)
			)
			.AddOption(
				new SlashCommandOptionBuilder()
					.WithName("upto")
					.WithDescription("Delete up to a certain message ID.")
					.WithType(ApplicationCommandOptionType.String)
					.WithRequired(false)
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
		var amount = (long?)context.Data.Options.FirstOrDefault(opt => opt.Name == "amount")?.Value;
		var upto = context.Data.Options.FirstOrDefault(opt => opt.Name == "upto")?.Value as string;
		var user = context.Data.Options.FirstOrDefault(opt => opt.Name == "from")?.Value as IUser;
		if (context.Channel is not ITextChannel channel)
		{
			await context.RespondAsync("This command can only be used in text channels.");
			return;
		}

		if (amount is null && upto is null)
		{
			await context.RespondAsync("You must specify either an amount, or up to a message ID, to purge.");
			return;
		}
		
		var purged = 0;
		if (amount is not null)
		{
			var messages = await channel.GetMessagesAsync(limit: Math.Min(Math.Max((int)amount, 1), MaxPurgeAmount)).FlattenAsync();
			if (user != null) messages = messages.Where(m => m.Author.Id == user.Id);
			messages = messages.Where(m => m.CreatedAt > DateTimeOffset.UtcNow.AddDays(-14)).ToList(); // Discord only allows purging messages from the last 14 days.
			
			purged = messages.Count();
			await channel.DeleteMessagesAsync(messages);
		}

		if (upto is not null)
		{
			var messages = await channel.GetMessagesAsync(limit: MaxPurgeAmount).FlattenAsync();
			if (user != null) messages = messages.Where(m => m.Author.Id == user.Id);
			messages = messages.Where(m => m.CreatedAt > DateTimeOffset.UtcNow.AddDays(-14)).ToList(); // Discord only allows purging messages from the last 14 days.
			
			var uptoMessage = messages.FirstOrDefault(m => m.Id.ToString() == upto);
			if (uptoMessage == null)
			{
				await context.RespondAsync($"Could not find message with ID {upto}. It may be past the {MaxPurgeAmount} message purge limit, or it may not exist.");
				return;
			}
			messages = messages.Where(m => m.CreatedAt > uptoMessage.CreatedAt).ToList();
			
			purged = messages.Count();
			await channel.DeleteMessagesAsync(messages);
		}
		
		await context.RespondAsync($"Purged {purged} {Utils.Plural(purged, "message", "messages")}.");
		
		await Task.Delay(2000);
		await context.DeleteOriginalResponseAsync();
	}
}