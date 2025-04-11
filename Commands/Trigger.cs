using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;

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
							.WithName("name") // Add a subcommand option (/trigger edit [name])
							.WithDescription("The name of the trigger to edit.")
							.WithType(ApplicationCommandOptionType.String)
							.WithRequired(true)
					)
			)
			.AddOption(
				new SlashCommandOptionBuilder()
					.WithName("remove") // Add a subcommand (/trigger remove)
					.WithDescription("Remove a trigger.")
					.WithType(ApplicationCommandOptionType.SubCommand)
					.AddOption(
						new SlashCommandOptionBuilder()
							.WithName("name") // Add a subcommand option (/trigger remove [name])
							.WithDescription("The name of the trigger to remove.")
							.WithType(ApplicationCommandOptionType.String)
							.WithRequired(true)
					)
			)
			.WithDescription("Triggers are effectively tags.")
			.Build();
	
	internal override string[] ModalIDs { get; set; } = [ "trigger_add", "trigger_edit" ]; // When you're working with modals, you need to specify the IDs of the modals that this command handles.

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
					.WithCustomId("trigger_add") // ...set the ID to "trigger_add" (don't forget to add it to the ModalIDs array above)...
					.AddTextInput("Trigger Words", "triggerer", placeholder: "Unity Hub download")
					.AddTextInput("Content", "content", TextInputStyle.Paragraph,
						"Copy and paste this link into your browser to download in Unity Hub: unityhub://2022.3.32f1/");

				await command.RespondWithModalAsync(addTriggerModal.Build()); // ...and respond with it.
				break;
			}
			case SocketModal modal: // If it's a modal with the ID "trigger_add"...
				var triggerName = modal.Data.Components.FirstOrDefault(c => c.CustomId == "triggerer")?.Value;
				var content = modal.Data.Components.FirstOrDefault(c => c.CustomId == "content")?.Value;
				if (string.IsNullOrEmpty(triggerName)) // ...check if the trigger name is empty...
				{
					await modal.RespondAsync("Trigger name cannot be empty.", ephemeral: true);
					return;
				}
				if (string.IsNullOrEmpty(content)) // ...check if the content is empty...
				{
					await modal.RespondAsync("Trigger content cannot be empty.", ephemeral: true);
					return;
				}
				if (Program.TriggersResponsesDict.ContainsKey(triggerName)) // ...check if the trigger already exists...
				{
					await modal.RespondAsync($"Trigger with the name `{triggerName}` already exists.", ephemeral: true);
					return;
				}
				
				await Program.AddTriggerAndResponse(triggerName, content); // ...add the trigger and response to the dictionary...
				
				await modal.RespondAsync($"Added trigger with name `{triggerName}`, and content:\n```\n{content}\n```"); // ...and respond with the trigger and content.
				break;
		}
	}

	private async Task HandleEdit(object context)
	{
		switch (context)
		{
			case SocketSlashCommand command: // If it's a command...
			{
				var subcommand = command.Data.Options.First(); // ...et the first option, which is the subcommand...
				var triggerName = subcommand.Options.FirstOrDefault(option => option.Name == "name")?.Value.ToString(); // ...get the name variable from the subcommand...
				if (string.IsNullOrEmpty(triggerName)) // ...check if it's empty...
				{
					await command.RespondAsync("Trigger name cannot be empty.", ephemeral: true);
					return;
				}
				
				if (!Program.TriggersResponsesDict.TryGetValue(triggerName, out var currentContent)) // ...check if the trigger exists...
				{
					await command.RespondAsync($"Trigger with the name `{triggerName}` not found.", ephemeral: true);
					return;
				}
				
				var editTriggerModal = new ModalBuilder()
					.WithTitle("Edit a trigger") // ...build a modal...
					.WithCustomId("trigger_edit") // ...set the ID to "trigger_edit" (also don't forget to add this one to the ModalIDs array above)...
					.AddTextInput("Trigger Words", "triggerer",
						placeholder: "Unity Hub download", value: triggerName,
						required: false
					)
					.AddTextInput("Content", "content", TextInputStyle.Paragraph,
						"hi i am edit", value: currentContent,
						required: false
					);

				await command.RespondWithModalAsync(editTriggerModal.Build()); // ...and respond with it.
				break;
			}
			case SocketModal modal: // If it's a modal with the ID "trigger_edit"...
				var id = modal.Data.Components.FirstOrDefault(c => c.CustomId == "triggerer")?.Value;
				var content = modal.Data.Components.FirstOrDefault(c => c.CustomId == "content")?.Value;
				if (string.IsNullOrEmpty(id)) // ...check if the trigger name is empty...
				{
					await modal.RespondAsync("Trigger name cannot be empty.", ephemeral: true);
					return;
				}
				if (string.IsNullOrEmpty(content)) // ...check if the content is empty...
				{
					await modal.RespondAsync("Trigger content cannot be empty.", ephemeral: true);
					return;
				}
				
				if (!Program.TriggersResponsesDict.ContainsKey(id)) // ...check if the trigger exists...
				{
					await modal.RespondAsync($"Trigger with the name `{id}` does not exist.", ephemeral: true);
					return;
				}
				
				Program.TriggersResponsesDict[id] = content; // ...update the trigger and response in the dictionary...
				await Program.SaveTriggers(); // ...save the changes...
				
				await modal.RespondAsync($"Trigger with the name `{id}` was updated to:\n```\n{content}\n```"); // ...and respond with the new content.
				break;
		}
	}
	
	private async Task HandleRemove(SocketSlashCommand context)
	{
		var subcommand = context.Data.Options.First(); // Get the first option, which is the subcommand...
		var triggerName = subcommand.Options.FirstOrDefault(option => option.Name == "name")?.Value.ToString(); // ...get the name variable from the subcommand...
		if (string.IsNullOrEmpty(triggerName)) // ...check if it's empty...
		{
			await context.RespondAsync("Trigger name cannot be empty.", ephemeral: true);
			return;
		}
		
		if (!Program.TriggersResponsesDict.Remove(triggerName, out _)) // ...check if the trigger exists and remove it...
		{
			await context.RespondAsync($"Trigger with the name `{triggerName}` not found.", ephemeral: true);
			return;
		}
		
		await context.RespondAsync($"Removed trigger with name `{triggerName}`."); // ...and respond with a status update.
	}
}