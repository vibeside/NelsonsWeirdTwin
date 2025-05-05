using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using NelsonsWeirdTwin.Commands;

namespace NelsonsWeirdTwin;

internal static class Events
{
	internal static async Task OnUserBanned(SocketUser user, SocketGuild guild)
	{
		await Task.Delay(1);
	}

		// TODO:
		// make this load and save to a json
	internal static Task OnReady()
	{
		_ = Task.Run(async () => await Program.LoadCommands()); 
		// Load commands in the background to stop
		// Discord.Net complaining about the Ready handler blocking the gateway task
	    
		//
		RolePicker.roles.Add(Program.Client.Guilds.First().GetRole(1359944109350981733));
		RolePicker.roles.Add(Program.Client.Guilds.First().GetRole(1359943967810256896));
        RolePicker.roles.Add(Program.Client.Guilds.First().GetRole(1366191861194293279));
        Console.WriteLine("Client is ready!");

		return Task.CompletedTask;
	}

	internal static async Task SelectMenuHandler(SocketMessageComponent c)
	{
		if (c.User is not SocketGuildUser u) await c.RespondAsync("Couldn't find user");
		else
		{
			var chosenRoles = RolePicker.roles.Where(r => c.Data.Values.Contains(r.Id.ToString())).ToList();
			var removedRoles = RolePicker.roles.Where(r => !c.Data.Values.Contains(r.Id.ToString())).ToList();
			
			var chosenRolesJoined = string.Join(", ", chosenRoles.ConvertAll(x => $"<@&{x.Id}>"));
			var removedRolesJoined = string.Join(", ", removedRoles.ConvertAll(x => $"<@&{x.Id}>"));

			var sb = new StringBuilder();

			sb.Append((chosenRoles.Count == 0
				? "Didn't give any roles"
				: $"Gave {u.Username} the roles {chosenRolesJoined}") + " "); // "[text] "

			sb.Append(removedRoles.Count == 0
				? ""
				: $"and removed the roles {removedRolesJoined}");
			
			await u.RemoveRolesAsync(removedRoles);
			await u.AddRolesAsync(chosenRoles);
			
			await c.RespondAsync(sb.ToString(), ephemeral: true, allowedMentions:AllowedMentions.None);
		}
	}

	internal static async Task MessageUpdated(Cacheable<IMessage, ulong> orig, SocketMessage updated, ISocketMessageChannel channel)
	{
		// if it STILL has no embeds or attachments, delete.
		if (Program.WatchList.Contains(updated.Id))
		{
			if(updated.Embeds.Count == 0 && updated.Attachments.Count == 0)
			{
				Program.WatchList.Remove(updated.Id);
				await updated.DeleteAsync();
			}
			else
			{
				Program.WatchList.Remove(updated.Id);
			}
		}
	}

	internal static async Task MessageReceived(SocketMessage msg)
	{
		if (msg.Author is not SocketGuildUser user) return;
		async Task CheckMessageSoon(SocketMessage checkedMessage)
        {
			await Task.Delay(5000);
			if (!Program.WatchList.Contains(checkedMessage.Id)) return;
            if (checkedMessage.Embeds.Count == 0 && checkedMessage.Attachments.Count == 0)
            {
                Program.WatchList.Remove(checkedMessage.Id);
                await checkedMessage.DeleteAsync();
            }
            else
            {
                Program.WatchList.Remove(checkedMessage.Id);
            }
        }
		if (msg is not SocketUserMessage || msg.Author is { IsBot: true } or { IsWebhook: true })
		{
			return;
		}
		// TODO
		// specialized trigger
		if(msg.Channel.Id is 1354832385128271922 or 1349410207582785670 && user.Roles.Any(x => x.Id == 1354831227152109590) && msg.Content.Contains("start modding"))
		{
			await msg.Channel.SendMessageAsync("If you are a new-ish mod developer, please see <#1359965818787598397>");
		}
		if(msg.Channel.Id is 1363500762780664070 or 1360078197609201785)
		{
			if (msg.Attachments.Count == 0 && msg.Embeds.Count == 0)
			{
				Program.WatchList.Add(msg.Id);
				Task.Run(async () => await CheckMessageSoon(msg));
			}
			else
			{
				var channel = (ITextChannel)msg.Channel; // using 'is not' causes problems, I've noticed
				if (channel == null) return;
				
				await channel.CreateThreadAsync($"{msg.Author.Username}'s mod showoff thread.", message: msg);
			}
		}
		
		if (user.Roles.Any(role => Program.IgnoredRoleIds.ToList().Contains(role.Id)))
		{
			return;
		}
		foreach (var k in Program.TriggerItems)
		{
			var triggers = k.Aliases.Where(x => msg.Content.Contains(x)).ToList();
			if(triggers.Count == 0) continue;
			
			var sb = new StringBuilder();
			sb.AppendLine(k.Response);
			sb.AppendLine($"-# ID: `{k.Id}` • Triggered by {string.Join(", ", triggers.Select(x => $"`{x}`"))}.");
			k.TimesTriggered += 1;
			await msg.Channel.SendMessageAsync(sb.ToString(), allowedMentions: AllowedMentions.None);
			break;
		}
	}

	internal static async Task SlashCommandSubmit(SocketSlashCommand command)
	{
		if (command.User is { IsBot: true } or { IsWebhook: true }) return;
		var commandName = command.Data.Name;

		foreach (var cmd in Program.CommandsList.Where(cmd => cmd.CommandProperties.Name.Value == commandName))
		{
		    Task.Run(async () =>
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
			});
			return;
	}
		await command.RespondAsync($"Command \"{commandName}\" not found. What the flip flop is happening here?!");
	}

	internal static async Task ModalSubmit(SocketModal modal)
	{
		if (modal.User is { IsBot: true } or { IsWebhook: true }) return;
		
		foreach (var cmd in Program.CommandsList.Where(cmd => cmd.ModalIDs.Contains(modal.Data.CustomId)))
		{
			await cmd.OnModalSubmitted(Program.Client, modal);
			return;
		}
		
		await modal.RespondAsync($"Modal {modal.Data.CustomId} not found!");
	}

	internal static async Task AutoCompleteHandler(SocketAutocompleteInteraction context)
	{
		if(context.User is { IsBot: true } or { IsWebhook: true }) return;
		var commandName = context.Data.CommandName;
		
		foreach (var cmd in Program.CommandsList.Where(cmd => cmd.CommandProperties.Name.Value == commandName))
		{
			await cmd.OnAutocompleteResultsRequested(Program.Client, context);
			return;
		}
	}
}