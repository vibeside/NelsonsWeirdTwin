using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

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
			.WithDescription("Triggers are effectively tags.")
			.Build();
	
	internal override string[] ModalIDs { get; set; } = [ "trigger_add", "trigger_edit" ]; // When you're working with modals, you need to specify the IDs of the modals that this command handles.

	private const string NotFound = "A trigger with an ID of `{0}` could not be found.";
	private const string AlreadyExists = "A trigger with an ID of `{0}` already exists.";
	
	private const string NoAliases = "A trigger needs to have aliases, otherwise how will it show?!";
	private const string NoResponse = "The trigger's response cannot be empty.";
	private const string NoId = "The trigger's ID cannot be empty.";
	
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
		}
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
						"Each alias goes on a new line.", required: true);

				await command.RespondWithModalAsync(addTriggerModal.Build()); // ...and respond with it.
				break;
			}
			case SocketModal modal: // If it's a modal with the ID "trigger_add"...
				var id = modal.Data.Components.FirstOrDefault(c => c.CustomId == "id")?.Value;
				var response = modal.Data.Components.FirstOrDefault(c => c.CustomId == "resp")?.Value;
				var aliases = modal.Data.Components.FirstOrDefault(c => c.CustomId == "alias")?.Value;
				if (string.IsNullOrEmpty(id)) // ...check if the ID is empty...
				{
					await modal.RespondAsync(NoId, ephemeral: true);
					return;
				}
				if (string.IsNullOrEmpty(response)) // ...check if the response text is empty...
				{
					await modal.RespondAsync(NoResponse, ephemeral: true);
					return;
				}
				if (string.IsNullOrEmpty(aliases)) // ...check if the aliases text is empty...
				{
					await modal.RespondAsync(NoAliases, ephemeral: true);
					return;
				}
				
				id = id.ToLower(); // ...make it lowercase to avoid case sensitivity issues...
				if (Program.TriggerItems.Any(t => t.Id == id)) // ...check if the trigger already exists...
				{
					await modal.RespondAsync(string.Format(AlreadyExists, id), ephemeral: true);
					return;
				}
				
				var aliasesList = aliases.Split('\n').Select(a => a.ToLower()).ToList();
				if (aliasesList.Count == 0) // ...check if the aliases are empty...
				{
					await modal.RespondAsync(NoAliases, ephemeral: true);
					return;
				}

				var conflicts = CheckForConflicts(aliasesList); // ...check if the aliases already exist...
				if (conflicts.Count != 0) // ...alert the user if they do...
				{
					await modal.RespondAsync(string.Format(ConflictsFound, string.Join("\n- ", conflicts)), ephemeral: true);
					return;
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
					await command.RespondAsync(NoId, ephemeral: true);
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
						"Each alias goes on a new line.", value: string.Join("\n", existing.Aliases), required: true);

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
			await context.RespondAsync(NoId, ephemeral: true);
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
			.Where(k => k.Id.Contains(currentValue, StringComparison.CurrentCultureIgnoreCase))
			.Select(k => new AutocompleteResult(k.Id, k.Id))
			.Take(25)
			.ToList();
		await context.RespondAsync(options);
	}

	private async Task DoTriggerUpdate(string id, string response, string aliases, IDiscordInteraction context)
	{
		if (string.IsNullOrEmpty(id)) // ...check if the ID is empty...
		{
			await context.RespondAsync(NoId, ephemeral: true);
			return;
		}
		if (string.IsNullOrEmpty(response)) // ...check if the response is empty...
		{
			await context.RespondAsync(NoResponse, ephemeral: true);
			return;
		}
		if (string.IsNullOrEmpty(aliases)) // ...check if the aliases is empty...
		{
			await context.RespondAsync(NoAliases, ephemeral: true);
			return;
		}
				
		id = id.ToLower(); // ...make it lowercase to avoid case sensitivity issues...
		var existing = Program.TriggerItems.FirstOrDefault(t => t.Id == id);
		if (existing == null) // ...check if the trigger exists...
		{
			await context.RespondAsync(string.Format(NotFound, id), ephemeral: true);
			return;
		}
		
		var aliasesList = aliases.Split('\n').ToList();
		if (aliasesList.Count == 0) // ...check if the aliases are empty...
		{
			await context.RespondAsync(NoAliases, ephemeral: true);
			return;
		}

		var lowered = aliasesList.Select(a => a.ToLower()).ToList(); // ...make them lowercase to avoid case sensitivity issues...
		var conflicts = CheckForConflicts(lowered, id); // ...check if the aliases already exist...
		if (conflicts.Count > 0) // ...alert the user if they do...
		{
			await context.RespondAsync(string.Format(ConflictsFound, string.Join("\n- ", conflicts)), ephemeral: true);
			return;
		}

		Program.TriggerItems.RemoveAll(t => t.Id == id); // ...remove the old trigger...
		await Program.AddNewTrigger(new TriggerItem
		{
			Id = id,
			Aliases = lowered,
			Response = response
		}); // ...add the new trigger to the list...
		
		await context.RespondAsync($"Updated trigger with ID `{id}`, and response:\n```\n{response}\n```", ephemeral: true); // ...and respond with the new trigger and content.
	}
	private List<string> CheckForConflicts(List<string> toBeAddedAliases)
	{
		return toBeAddedAliases.Where(a => Program.TriggerItems.Any(t => t.Aliases.Contains(a, StringComparer.CurrentCultureIgnoreCase))).ToList(); // ...check if any of the aliases already exist...
	}
	private List<string> CheckForConflicts(List<string> toBeAddedAliases, string myID)
	{
		return toBeAddedAliases.Where(a => Program.TriggerItems.Any(t => t.Aliases.Contains(a, StringComparer.CurrentCultureIgnoreCase) && t.Id != myID)).ToList(); // ...check if any of the aliases already exist...
	}
}