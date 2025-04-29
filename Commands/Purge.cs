using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using NelsonsWeirdTwin.Extensions;

namespace NelsonsWeirdTwin.Commands;

internal class PurgeCommand : Command
{
	private const int MaxPurgeAmount = 200; // I wouldn't recommend going above 200
											// simply due to the repeated get message API calls that happen under the hood
											// (Discord.NET splits over multiple requests if the amount is too high)
											// this is a good amount to keep the bot from being rate limited
											// and to also stop long delays

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
			.AddOption(
				new SlashCommandOptionBuilder()
					.WithName("inclusive")
					.WithDescription("Purges all messages up to & including 'upto' message.")
					.WithType(ApplicationCommandOptionType.Boolean)
					.WithRequired(false)
			)
			.Build();

	internal override async Task OnExecuted(DiscordSocketClient client, SocketSlashCommand context)
	{
		await context.DeferAsync();
		var original = await context.GetOriginalResponseAsync();
		
		var upto = context.Data.Options.FirstOrDefault(opt => opt.Name == "upto")?.Value as string;
		var user = context.Data.Options.FirstOrDefault(opt => opt.Name == "from")?.Value as IUser;
		var inclusive = context.Data.Options.FirstOrDefault(opt => opt.Name == "inclusive")?.Value as bool? ?? false;
		if (context.Channel is not ITextChannel channel)
		{
			await context.ModifyOriginalMessageAsync("This command can only be used in text channels.", 2500);
			return;
		}
		
		if (string.IsNullOrEmpty(upto))
		{
			await context.ModifyOriginalMessageAsync("You must specify a message ID to purge up to.", 2500);
			return;
		}
		if (!ulong.TryParse(upto, out var messageId))
		{
			await context.ModifyOriginalMessageAsync("Invalid message ID to purge up to.", 2500);
			return;
		}
		
		var uptoMessage = await context.Channel.GetMessageAsync(messageId);
		if (uptoMessage == null)
		{
			await context.ModifyOriginalMessageAsync($"Couldn't find message with an ID of `{upto}`.", 2500);
			return;
		}
		
		var messagesToPurge = (await channel.GetMessagesAsync(limit: MaxPurgeAmount).FlattenAsync()).ToList();
		
		#region Filters
		messagesToPurge.RemoveAll(msg => msg.Id == original.Id); // removes the bot's original message from the list
		var tooOld = messagesToPurge.RemoveAll(msg => msg.CreatedAt < DateTimeOffset.UtcNow.AddDays(-14)); // removes any messages from the list that are older than 14 days
		messagesToPurge.RemoveAll(msg => msg.CreatedAt < uptoMessage.CreatedAt); // removes any messages from the list that are older than the specified message
		if(!inclusive) messagesToPurge.RemoveAll(msg => msg.Id == uptoMessage.Id); // removes the specified message from the list
		if (user != null) messagesToPurge.RemoveAll(msg => msg.Author.Id != user.Id); // remove any messages from the list that are not from the specified user
		#endregion
		
		var amountToPurge = messagesToPurge.Count;
		if (amountToPurge == 0)
		{
			var awesomeSb = new StringBuilder();
			awesomeSb.AppendLine($"No messages were purged from {channel.Mention}.");
			if(tooOld > 0) awesomeSb.Append($"-# {tooOld} {Utils.Plural(tooOld, "message", "messages")}, of which are too old to be purged, were ignored.");
			
			await context.ModifyOriginalMessageAsync(awesomeSb.ToString(), 2500);
			return;
		}
		// test bruhhh
		var purgeHeading = new StringBuilder();
		purgeHeading.Append("==================================================================\n");
        purgeHeading.Append($"Removed {amountToPurge} {Utils.Plural(amountToPurge, "message", "messages")} at {DateTime.UtcNow}\n");
		purgeHeading.Append($"Command ran by: {context.User.GlobalName}({context.User.Mention})\n");
		purgeHeading.Append("==================================================================\n");
		
		await channel.DeleteMessagesAsync(messagesToPurge); 
        messagesToPurge.Reverse();
        messagesToPurge.ForEach(msg => purgeHeading.Append($"{msg.Author.GlobalName}({msg.Author.Mention}): {msg.CleanContent}\n"));
        File.AppendAllText("PurgeLog.txt", purgeHeading.ToString());
        var sb = new StringBuilder();
		sb.AppendLine($"Purged {amountToPurge} {Utils.Plural(amountToPurge, "message", "messages")} from {channel.Mention}.");
		if(tooOld > 0) sb.Append($"-# {tooOld} {Utils.Plural(tooOld, "message", "messages")}, of which are too old to be purged, were ignored.");
		
		await context.ModifyOriginalMessageAsync(sb.ToString(), 2500);
	}
}