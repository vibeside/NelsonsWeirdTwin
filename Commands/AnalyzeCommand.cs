using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace NelsonsWeirdTwin.Commands;

internal sealed class AnalyzeCommand : Command
{
	internal override SlashCommandProperties CommandProperties =>
		new SlashCommandBuilder()
			.WithName("analyze")
			.WithDescription("Analyze a support log or manage analysis settings.")
			.AddOption(
				new SlashCommandOptionBuilder()
					.WithName("message")
					.WithDescription("Analyze a message with a .log or .txt attachment.")
					.WithType(ApplicationCommandOptionType.SubCommand)
					.AddOption(
						new SlashCommandOptionBuilder()
							.WithName("target")
							.WithDescription("A message ID in this channel or a full Discord message link.")
							.WithType(ApplicationCommandOptionType.String)
							.WithRequired(true)
					)
			)
			.AddOption(
				new SlashCommandOptionBuilder()
					.WithName("settings")
					.WithDescription("Manage which channels allow /analyze.")
					.WithType(ApplicationCommandOptionType.SubCommandGroup)
					.AddOption(
						new SlashCommandOptionBuilder()
							.WithName("list")
							.WithDescription("List the configured analyze channels.")
							.WithType(ApplicationCommandOptionType.SubCommand)
					)
					.AddOption(
						new SlashCommandOptionBuilder()
							.WithName("add-channel")
							.WithDescription("Add a channel where /analyze can be used.")
							.WithType(ApplicationCommandOptionType.SubCommand)
							.AddOption(
								new SlashCommandOptionBuilder()
									.WithName("channel")
									.WithDescription("The support or testing channel to allow.")
									.WithType(ApplicationCommandOptionType.Channel)
									.WithRequired(true)
							)
					)
					.AddOption(
						new SlashCommandOptionBuilder()
							.WithName("remove-channel")
							.WithDescription("Remove a channel from /analyze.")
							.WithType(ApplicationCommandOptionType.SubCommand)
							.AddOption(
								new SlashCommandOptionBuilder()
									.WithName("channel")
									.WithDescription("The support or testing channel to remove.")
									.WithType(ApplicationCommandOptionType.Channel)
									.WithRequired(true)
							)
					)
			)
			.Build();

	internal override async Task OnExecuted(DiscordSocketClient client, SocketSlashCommand context)
	{
		await context.DeferAsync(ephemeral: true);

		var rootOption = context.Data.Options.FirstOrDefault();
		if (rootOption == null)
		{
			await SetResponseAsync(context, "Missing analyze subcommand.");
			return;
		}

		switch (rootOption.Name)
		{
			case "message":
				await HandleMessageAsync(client, context, rootOption);
				break;
			case "settings":
				await HandleSettingsAsync(client, context, rootOption);
				break;
			default:
				await SetResponseAsync(context, $"Unknown analyze subcommand `{rootOption.Name}`.");
				break;
		}
	}

	private static async Task HandleMessageAsync(DiscordSocketClient client, SocketSlashCommand context, SocketSlashCommandDataOption subcommand)
	{
		if (!Program.SupportChannelIds.Contains(context.Channel.Id))
		{
			await SetResponseAsync(context, "This command can only be used in configured analyze channels.");
			return;
		}

		var target = subcommand.Options.FirstOrDefault(option => option.Name == "target")?.Value as string;
		if (string.IsNullOrWhiteSpace(target))
		{
			await SetResponseAsync(context, "You must provide a message ID or Discord message link.");
			return;
		}

		var resolvedTarget = await TryResolveTargetMessageAsync(client, context, target);
		if (resolvedTarget.Message == null)
		{
			await SetResponseAsync(context, resolvedTarget.ErrorMessage);
			return;
		}

		await Program.SupportLogAnalyzer.AnalyzeMessageAsync(context, resolvedTarget.Message);
	}

	private static async Task HandleSettingsAsync(DiscordSocketClient client, SocketSlashCommand context, SocketSlashCommandDataOption groupOption)
	{
		if (!HasManageMessagesPermission(context))
		{
			await SetResponseAsync(context, "You need `ManageMessages` to change analyze settings.");
			return;
		}

		var subcommand = groupOption.Options.FirstOrDefault();
		if (subcommand == null)
		{
			await SetResponseAsync(context, "Missing analyze settings subcommand.");
			return;
		}

		switch (subcommand.Name)
		{
			case "list":
				await SetResponseAsync(context, BuildChannelListMessage());
				break;
			case "add-channel":
				await UpdateChannelAsync(client, context, subcommand, addChannel: true);
				break;
			case "remove-channel":
				await UpdateChannelAsync(client, context, subcommand, addChannel: false);
				break;
			default:
				await SetResponseAsync(context, $"Unknown analyze settings subcommand `{subcommand.Name}`.");
				break;
		}
	}

	private static async Task UpdateChannelAsync(DiscordSocketClient client, SocketSlashCommand context, SocketSlashCommandDataOption subcommand, bool addChannel)
	{
		var channelOption = subcommand.Options.FirstOrDefault(option => option.Name == "channel")?.Value as IChannel;
		if (channelOption == null)
		{
			await SetResponseAsync(context, "You must pick a channel.");
			return;
		}

		var liveChannel = await TryGetMessageChannelAsync(client, channelOption.Id);
		if (liveChannel == null)
		{
			await SetResponseAsync(context, "That channel does not support messages.");
			return;
		}

		if (addChannel)
		{
			var added = Program.AddSupportChannelId(channelOption.Id);
			await SetResponseAsync(
				context,
				added
					? $"Added <#{channelOption.Id}> (`{channelOption.Id}`) to the analyze channel allowlist."
					: $"<#{channelOption.Id}> (`{channelOption.Id}`) is already in the analyze channel allowlist.");
			return;
		}

		var removed = Program.RemoveSupportChannelId(channelOption.Id);
		await SetResponseAsync(
			context,
			removed
				? $"Removed <#{channelOption.Id}> (`{channelOption.Id}`) from the analyze channel allowlist."
				: $"<#{channelOption.Id}> (`{channelOption.Id}`) was not in the analyze channel allowlist.");
	}

	private static string BuildChannelListMessage()
	{
		if (Program.SupportChannelIds.Count == 0)
		{
			return "Analyze is currently disabled. No channels are configured.";
		}

		var channelLines = Program.SupportChannelIds
			.OrderBy(channelId => channelId)
			.Select(channelId => $"- <#{channelId}> (`{channelId}`)");

		return "Configured analyze channels:\n" + string.Join("\n", channelLines);
	}

	private static bool HasManageMessagesPermission(SocketSlashCommand context)
	{
		if (context.User is not SocketGuildUser guildUser)
		{
			return false;
		}

		if (context.Channel is IGuildChannel guildChannel)
		{
			return guildUser.GetPermissions(guildChannel).ManageMessages;
		}

		return guildUser.GuildPermissions.ManageMessages;
	}

	private static async Task<(IMessage Message, string ErrorMessage)> TryResolveTargetMessageAsync(DiscordSocketClient client, SocketSlashCommand context, string rawTarget)
	{
		if (!TryParseTarget(rawTarget, context.Channel.Id, out var channelId, out var messageId))
		{
			return (null, "That target must be a raw message ID in this channel or a full Discord message link.");
		}

		if (!Program.SupportChannelIds.Contains(channelId))
		{
			return (null, "The target message must be in a configured analyze channel.");
		}

		var channel = await TryGetMessageChannelAsync(client, channelId);
		if (channel == null)
		{
			return (null, "I couldn't access that target channel.");
		}

		var message = await channel.GetMessageAsync(messageId);
		if (message == null)
		{
			return (null, $"Couldn't find a message with ID `{messageId}` in <#{channelId}>.");
		}

		return (message, null);
	}

	private static async Task<IMessageChannel> TryGetMessageChannelAsync(DiscordSocketClient client, ulong channelId)
	{
		if (client.GetChannel(channelId) is IMessageChannel cachedMessageChannel)
		{
			return cachedMessageChannel;
		}

		var fetchedChannel = await client.GetChannelAsync(channelId);
		return fetchedChannel as IMessageChannel;
	}

	private static bool TryParseTarget(string rawTarget, ulong currentChannelId, out ulong channelId, out ulong messageId)
	{
		if (ulong.TryParse(rawTarget, out messageId))
		{
			channelId = currentChannelId;
			return true;
		}

		if (!Uri.TryCreate(rawTarget, UriKind.Absolute, out var uri))
		{
			channelId = 0;
			messageId = 0;
			return false;
		}

		if (!string.Equals(uri.Host, "discord.com", StringComparison.OrdinalIgnoreCase)
			&& !string.Equals(uri.Host, "ptb.discord.com", StringComparison.OrdinalIgnoreCase)
			&& !string.Equals(uri.Host, "canary.discord.com", StringComparison.OrdinalIgnoreCase))
		{
			channelId = 0;
			messageId = 0;
			return false;
		}

		var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
		if (segments.Length != 4 || !string.Equals(segments[0], "channels", StringComparison.OrdinalIgnoreCase))
		{
			channelId = 0;
			messageId = 0;
			return false;
		}

		return ulong.TryParse(segments[2], out channelId) && ulong.TryParse(segments[3], out messageId);
	}

	private static Task SetResponseAsync(SocketSlashCommand context, string message)
	{
		return context.ModifyOriginalResponseAsync(properties =>
		{
			properties.Content = message;
			properties.Embed = null;
			properties.Components = null;
		});
	}
}
