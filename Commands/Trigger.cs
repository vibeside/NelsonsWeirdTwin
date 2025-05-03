#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Schema;
using Discord;
using Discord.WebSocket;
using NelsonsWeirdTwin.Extensions;

namespace NelsonsWeirdTwin.Commands;

internal class TriggerCommands: Command
{
	internal override SlashCommandProperties CommandProperties => // This one's a little more advanced, but only because we have subcommands.
		new SlashCommandBuilder()
			.WithName("trigger") // Start off with the base (/trigger)
			.AddOption(
				new SlashCommandOptionBuilder()
					.WithName("add") // Add a subcommand (/trigger add)
					.WithDescription("Add a trigger.")
					.WithType(ApplicationCommandOptionType.SubCommand)
			)
			.AddOption(
				new SlashCommandOptionBuilder()
					.WithName("edit") // Add a subcommand (/trigger edit)
					.WithDescription("Edit a trigger.")
					.WithType(ApplicationCommandOptionType.SubCommand)
					.AddOption(
						new SlashCommandOptionBuilder()
							.WithName("id") // Add a subcommand option (/trigger edit [id])
							.WithDescription("The ID of the trigger to edit.")
							.WithType(ApplicationCommandOptionType.String)
							.WithRequired(true)
							.WithAutocomplete(true)
					)
			)
			.AddOption(
				new SlashCommandOptionBuilder()
					.WithName("remove") // Add a subcommand (/trigger remove)
					.WithDescription("Remove a trigger.")
					.WithType(ApplicationCommandOptionType.SubCommand)
					.AddOption(
						new SlashCommandOptionBuilder()
							.WithName("id") // Add a subcommand option (/trigger remove [id])
							.WithDescription("The ID of the trigger to remove.")
							.WithType(ApplicationCommandOptionType.String)
							.WithRequired(true)
							.WithAutocomplete(true)
					)
			)
			.AddOption(
				new SlashCommandOptionBuilder()
					.WithName("run") // Add a subcommand (/trigger run)
					.WithDescription("Send a trigger's output to the channel.")
					.WithType(ApplicationCommandOptionType.SubCommand)
					.AddOption(
						new SlashCommandOptionBuilder()
							.WithName("id") // Add a subcommand option (/trigger run [id])
							.WithDescription("The ID of the trigger to run.")
							.WithType(ApplicationCommandOptionType.String)
							.WithRequired(true)
							.WithAutocomplete(true)
					)
					.AddOption(
						new SlashCommandOptionBuilder()
							.WithName("reply-to") // Add a subcommand option (/trigger run [id] [reply-to*])
							.WithDescription("The message to reply to.")
							.WithType(ApplicationCommandOptionType.String)
							.WithRequired(false)
					)
			)
			.AddOption(
				new SlashCommandOptionBuilder()
					.WithName("leaderboard")
					.WithDescription("Leader board sub commands(listall,top5)")
					.WithType(ApplicationCommandOptionType.SubCommandGroup)
					.AddOption(
						new SlashCommandOptionBuilder()
							.WithName("topfive")
							.WithDescription("Lists the top five triggered phrases")
							.WithType(ApplicationCommandOptionType.SubCommand)
					)
					.AddOption(
						new SlashCommandOptionBuilder()
							.WithName("listall")
							.WithDescription("Lists the entire leaderboard of triggered items")
							.WithType(ApplicationCommandOptionType.SubCommand)
					)
			)
            .WithDescription("Triggers are effectively tags.")
			.Build();
	
	internal override string[] ModalIDs { get; set; } = [ "trigger_add", "trigger_edit" ]; // When you're working with modals, you need to specify the IDs of the modals that this command handles.

	private const string NotFound = "A trigger with an ID of `{0}` could not be found.";
	private const string AlreadyExists = "A trigger with an ID of `{0}` already exists.";
	
	// private const string NoAliases = "A trigger needs to have aliases, otherwise how will it show?!";
	private const string EmptyResponse = "The trigger's response cannot be empty.";
	private const string EmptyId = "The trigger's ID cannot be empty.";
	
	private const string ConflictsFound = "One or more aliases already exist:\n- {0}";
	
	internal override async Task OnExecuted(DiscordSocketClient client, SocketSlashCommand context)
	{
		var option = context.Data.Options.First().Name; // Get the first option, which is the subcommand.
		switch (option)
		{
			case "add": // /trigger add
				await HandleAdd(context);
				break;
			case "edit": // /trigger edit
				await HandleEdit(context);
				break;
			case "remove": // /trigger remove 
				await HandleRemove(context);
				break;
			case "run":
				await HandleRun(context);
				break;
			case "leaderboard":
				await HandleLeaderboard(context);
				break;
		}
	}
	internal async Task HandleLeaderboard(SocketSlashCommand context)
	{
		int amountToList = 0;
		StringBuilder sb = new();
		sb.Append("Here's the leaderboard for the trigger list:\n");
		amountToList = context.Data.Options.First().Options.First().Name == "topfive" ? 5 : Program.TriggerItems.Count;
		List<TriggerItem> copy = [.. Program.TriggerItems.OrderByDescending(x => x.TimesTriggered)];
		for (int i = 0; i < amountToList; i++)
		{
			if (copy[i] == null) continue;
			sb.Append($"{i + 1}. {copy[i].Id} | {copy[i].TimesTriggered}\n");
		}
		await context.RespondAsync(sb.ToString());
	}
    internal override async Task OnModalSubmitted(DiscordSocketClient client, SocketModal context) // This is called when a modal is submitted, and the modal's CustomId was found in our command's ModalIDs.
	{
		var customId = context.Data.CustomId;
		switch (customId)
		{
			case "trigger_add": // This is from /trigger add
				await HandleAdd(context);
				break;
			case "trigger_edit": // This is from /trigger edit
				await HandleEdit(context);
				break;
		}
	}
	
	private async Task HandleAdd(object context) // We handle both the slash command and modals here, to reduce method count.
	{
		switch (context)
		{
			case SocketSlashCommand command: // If it's a command...
			{
				var addTriggerModal = new ModalBuilder()
					.WithTitle("Add a trigger") // ...build a modal...
					.WithCustomId(
						"trigger_add") // ...set the ID to "trigger_add" (don't forget to add it to the ModalIDs array above)...
					.AddTextInput("ID", "id", placeholder: "unity-hub-dl", required: true, maxLength: 32)
					.AddTextInput("Response", "resp", TextInputStyle.Paragraph,
						"Copy and paste this link into your browser to download in Unity Hub: unityhub://2022.3.32f1/", required: true)
					.AddTextInput("Aliases", "alias", TextInputStyle.Paragraph,
						"Each alias goes on a new line.", required: false);

				await command.RespondWithModalAsync(addTriggerModal.Build()); // ...and respond with it.
				break;
			}
			case SocketModal modal: // If it's a modal with the ID "trigger_add"...
				var id = modal.Data.Components.FirstOrDefault(c => c.CustomId == "id")?.Value;
				var response = modal.Data.Components.FirstOrDefault(c => c.CustomId == "resp")?.Value;
				var aliases = modal.Data.Components.FirstOrDefault(c => c.CustomId == "alias")?.Value;
				if (string.IsNullOrEmpty(id)) // ...check if the ID is empty...
				{
					await modal.RespondAsync(EmptyId, ephemeral: true);
					return;
				}
				if (string.IsNullOrEmpty(response)) // ...check if the response text is empty...
				{
					await modal.RespondAsync(EmptyResponse, ephemeral: true);
					return;
				}
				
				id = id.ToLower(); // ...make it lowercase to avoid case sensitivity issues...
				if (Program.TriggerItems.Any(t => t.Id == id)) // ...check if the trigger already exists...
				{
					await modal.RespondAsync(string.Format(AlreadyExists, id), ephemeral: true);
					return;
				}

				HashSet<string> aliasesList = [];
				if (!string.IsNullOrEmpty(aliases))
				{
					aliasesList = aliases.Trim().Split('\n').ToHashSet(); // ...split the aliases into a list...
					//if (aliasesList.Count > 0) // ...if there are aliases...
					//{
						//var conflicts = CheckForConflicts(aliasesList); // ...check if these aliases already exist...
						//if (conflicts.Count > 0) // ...alert the user if they do...
						//{
							//await modal.RespondAsync(string.Format(ConflictsFound, string.Join("\n- ", conflicts)),
								//ephemeral: true);
							//return;
						//}
					//}
				}

				await Program.AddNewTrigger(new TriggerItem
				{
					Id = id,
					Aliases = aliasesList,
					Response = response
				}); // ...add the trigger to the list...
				await modal.RespondAsync($"Added trigger with ID `{id}`, and response:\n```\n{response}\n```", ephemeral: true); // ...and respond with the trigger and content.
				break;
		}
	}

	private async Task HandleEdit(object context)
	{
		switch (context)
		{
			case SocketSlashCommand command: // If it's a command...
			{
				var subcommand = command.Data.Options.First(); // ...get the first option, which is the subcommand...
				var id = subcommand.Options.FirstOrDefault(option => option.Name == "id")?.Value.ToString(); // ...get the ID variable from the subcommand...
				if (string.IsNullOrEmpty(id)) // ...check if it's empty...
				{
					await command.RespondAsync(EmptyId, ephemeral: true);
					return;
				}
				
				id = id.ToLower(); // ...make it lowercase to avoid case sensitivity issues...
				var existing = Program.TriggerItems.FirstOrDefault(t => t.Id == id);
				if (existing == null) // ...check if the trigger exists...
				{
					await command.RespondAsync(string.Format(NotFound, id), ephemeral: true);
					return;
				}
				
				var editTriggerModal = new ModalBuilder()
					.WithTitle("Edit a trigger") // ...build a modal...
					.WithCustomId("trigger_edit") // ...set the ID to "trigger_edit" (also don't forget to add this one to the ModalIDs array above)...
					.AddTextInput("ID", "id", placeholder: "unity-hub-dl", value: existing.Id, required: true, maxLength: 32)
					.AddTextInput("Response", "resp", TextInputStyle.Paragraph,
						"Copy and paste this link into your browser to download in Unity Hub: unityhub://2022.3.32f1/", value: existing.Response, required: true)
					.AddTextInput("Aliases", "alias", TextInputStyle.Paragraph,
						"Each alias goes on a new line.", value: string.Join("\n", existing.Aliases), required: false);

				await command.RespondWithModalAsync(editTriggerModal.Build()); // ...and respond with it.
				break;
			}
			case SocketModal modal: // If it's a modal with the ID "trigger_edit"...
				var tid = modal.Data.Components.FirstOrDefault(c => c.CustomId == "id")?.Value;
				var response = modal.Data.Components.FirstOrDefault(c => c.CustomId == "resp")?.Value;
				var aliases = modal.Data.Components.FirstOrDefault(c => c.CustomId == "alias")?.Value;
				
				await DoTriggerUpdate(tid, response, aliases, modal); // ...call the DoTriggerUpdate method to handle the trigger update.
				break;
		}
	}
	
	private async Task HandleRemove(SocketSlashCommand context)
	{
		var subcommand = context.Data.Options.First(); // Get the first option, which is the subcommand...
		var tid = subcommand.Options.FirstOrDefault(option => option.Name == "id")?.Value.ToString(); // ...get the ID variable from the subcommand...
		if (string.IsNullOrEmpty(tid)) // ...check if it's empty...
		{
			await context.RespondAsync(EmptyId, ephemeral: true);
			return;
		}

		tid = tid.ToLower(); // ...make it lowercase to avoid case sensitivity issues...
		var existing = Program.TriggerItems.FirstOrDefault(t => t.Id == tid); // ...check if the trigger exists...
		if (existing == null)
		{
			await context.RespondAsync(string.Format(NotFound, tid), ephemeral: true);
			return;
		}
		
		Program.TriggerItems.Remove(existing); // ...remove the trigger...
		await Program.SaveTriggers(); // ...save the changes...
		
		await context.RespondAsync($"Removed trigger with ID `{tid}`.", ephemeral: true); // ...and respond with a status update.
	}
	
	private async Task HandleRun(SocketSlashCommand context)
	{
		await context.DeferAsync(ephemeral: true);
		
		var subcommand = context.Data.Options.First(); // Get the first option, which is the subcommand...
		var tid = subcommand.Options.FirstOrDefault(option => option.Name == "id")?.Value.ToString(); // ...get the ID variable from the subcommand...
		var replyTo = subcommand.Options.FirstOrDefault(option => option.Name == "reply-to")?.Value.ToString(); // ...get the reply-to variable from the subcommand...
		
		if (string.IsNullOrEmpty(tid)) // ...check if the ID is empty...
		{
			await context.ModifyOriginalMessageAsync(EmptyId, 2000);
			return;
		}

		IUserMessage? replyingTo = null;
		if (!string.IsNullOrEmpty(replyTo)) // ...check if the reply-to variable is empty...
		{
			if (!ulong.TryParse(replyTo, out var messageId)) // ...check if the reply-to variable is a valid message ID...
			{
				await context.ModifyOriginalMessageAsync("Invalid message ID to reply to.", 2000);
				return;
			}

			var msg = await context.Channel.GetMessageAsync(messageId);
			if (msg == null) // ...check if the message exists...
			{
				await context.ModifyOriginalMessageAsync($"Couldn't find message with an ID of `{replyTo}`.", 2000);
				return;
			}
			
			replyingTo = msg as IUserMessage; // ...and set the replyingTo variable to the message...
		}
		
		var existing = Program.TriggerItems.FirstOrDefault(
			t => t.Id.Equals(tid, StringComparison.InvariantCultureIgnoreCase)); // ...check if the trigger exists...
		if (existing == null)
		{
			await context.ModifyOriginalMessageAsync(string.Format(NotFound, tid), 2000);
			return;
		}

		await context.DeleteOriginalResponseAsync();
		
		var sb = new StringBuilder();
		sb.AppendLine(existing.Response);
		sb.AppendLine($"-# ID: `{existing.Id}` • Run by: {context.User.Mention}");
		
		if(replyingTo != null) await replyingTo.ReplyAsync(sb.ToString(), allowedMentions: AllowedMentions.None); else await context.Channel.SendMessageAsync(sb.ToString(), allowedMentions: AllowedMentions.None); // ...and respond with the trigger and content.
	}

	internal override async Task OnAutocompleteResultsRequested(DiscordSocketClient client, SocketAutocompleteInteraction context)
	{
		if (context.Data.Current.Name != "id")
		{
			await context.RespondAsync([]);
			return;
		}
		
		var currentValue = context.Data.Current.Value.ToString();
		if(string.IsNullOrEmpty(currentValue))
		{
			await context.RespondAsync(Program.TriggerItems
				.Select(k => new AutocompleteResult(k.Id, k.Id))
				.Take(25)
				.ToList());
			return;
		}
		
		var options = Program.TriggerItems
			.Where(k => k.Id.Contains(currentValue, StringComparison.InvariantCultureIgnoreCase))
			.Select(k => new AutocompleteResult(k.Id, k.Id))
			.Take(25)
			.ToList();
		await context.RespondAsync(options);
	}

	private async Task DoTriggerUpdate(string? id, string? response, string? aliases, IDiscordInteraction context)
	{
		if (string.IsNullOrEmpty(id)) // ...Check if the ID is empty...
		{
			await context.RespondAsync(EmptyId, ephemeral: true);
			return;
		}
		if (string.IsNullOrEmpty(response)) // ...check if the response text is empty...
		{
			await context.RespondAsync(EmptyResponse, ephemeral: true);
			return;
		}
		
		id = id.ToLower(); // ...make it lowercase to avoid case sensitivity issues...
		var existing = Program.TriggerItems.FirstOrDefault(t => t.Id == id);
		if (existing == null) // ...check if the trigger exists...
		{
			await context.RespondAsync(string.Format(NotFound, id), ephemeral: true);
			return;
		}
		
		HashSet<string> aliasesList = [];
		if (!string.IsNullOrEmpty(aliases))
		{
			aliasesList = aliases.Trim().Split('\n').ToHashSet(); // ...split the aliases into a list...
			//if (aliasesList.Count > 0) // ...if there are aliases...
			//{
			//	var conflicts = CheckForConflicts(aliasesList); // ...check if these aliases already exist...
			//	if (conflicts.Count > 0) // ...alert the user if they do...
			//	{
			//		await context.RespondAsync(string.Format(ConflictsFound, string.Join("\n- ", conflicts)),
			//			ephemeral: true);
			//		return;
			//	}
			//}
		}
		int v = Program.TriggerItems.FirstOrDefault(x => x.Id == id)?.TimesTriggered ?? 0;

		Program.TriggerItems.RemoveAll(t => t.Id == id); // ...remove the old trigger...
		await Program.AddNewTrigger(new TriggerItem
		{
			Id = id,
			Aliases = aliasesList,
			Response = response,
			TimesTriggered = v
		}); // ...add the new trigger to the list...
		
		await context.RespondAsync($"Updated trigger with ID `{id}`, and response:\n```\n{response}\n```", ephemeral: true); // ...and respond with the new trigger and content.
	}
	private List<string> CheckForConflicts(List<string> toBeAddedAliases)
	{
		
		return toBeAddedAliases.Where(a => Program.TriggerItems.Any(t => t.Aliases.Contains(a, StringComparer.InvariantCultureIgnoreCase))).ToList(); // ...check if any of the aliases already exist...
	}
	private List<string> CheckForConflicts(List<string> toBeAddedAliases, string myID)
	{
		return toBeAddedAliases.Where(a => Program.TriggerItems.Any(t => t.Aliases.Contains(a, StringComparer.InvariantCultureIgnoreCase) && t.Id != myID)).ToList(); // ...check if any of the aliases already exist...
	}
}