using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;

namespace NelsonsWeirdTwin.Commands;

internal class TriggerCommands: Command
{
	internal override SlashCommandProperties CommandProperties =>
		new SlashCommandBuilder()
			.WithName("trigger")
			.AddOption(
				new SlashCommandOptionBuilder()
					.WithName("add")
					.WithDescription("Add a trigger.")
					.WithType(ApplicationCommandOptionType.SubCommand)
			)
			.AddOption(
				new SlashCommandOptionBuilder()
					.WithName("edit")
					.WithDescription("Edit a trigger.")
					.WithType(ApplicationCommandOptionType.SubCommand)
					.AddOption(
						new SlashCommandOptionBuilder()
							.WithName("name")
							.WithDescription("The name of the trigger to edit.")
							.WithType(ApplicationCommandOptionType.String)
							.WithRequired(true)
					)
			)
			.AddOption(
				new SlashCommandOptionBuilder()
					.WithName("remove")
					.WithDescription("Remove a trigger.")
					.WithType(ApplicationCommandOptionType.SubCommand)
					.AddOption(
						new SlashCommandOptionBuilder()
							.WithName("name")
							.WithDescription("The name of the trigger to remove.")
							.WithType(ApplicationCommandOptionType.String)
							.WithRequired(true)
					)
			)
			.WithDescription("Triggers are effectively tags.")
			.Build();
	
	internal override string[] ModalIDs { get; set; } = [ "trigger_add", "trigger_edit" ];

	internal override async Task OnExecuted(DiscordSocketClient client, SocketSlashCommand context)
	{
		var option = context.Data.Options.First().Name;
		switch (option)
		{
			case "add":
				await HandleAdd(context);
				break;
			case "edit":
				await HandleEdit(context);
				break;
			case "remove":
				await HandleRemove(context);
				break;
		}
	}

	internal override async Task OnModalSubmitted(DiscordSocketClient client, SocketModal context)
	{
		var customId = context.Data.CustomId;
		switch (customId)
		{
			case "trigger_add":
				await HandleAdd(context);
				break;
			case "trigger_edit":
				await HandleEdit(context);
				break;
		}
	}
	
	private async Task HandleAdd(object context)
	{
		switch (context)
		{
			case SocketSlashCommand command:
			{
				var addTriggerModal = new ModalBuilder()
					.WithTitle("Add a trigger")
					.WithCustomId("trigger_add")
					.AddTextInput("Trigger Words", "triggerer", placeholder: "Unity Hub download")
					.AddTextInput("Content", "content", TextInputStyle.Paragraph,
						"Copy and paste this link into your browser to download in Unity Hub: unityhub://2022.3.32f1/");

				await command.RespondWithModalAsync(addTriggerModal.Build());
				break;
			}
			case SocketModal modal:
				var triggerName = modal.Data.Components.FirstOrDefault(c => c.CustomId == "triggerer")?.Value;
				var content = modal.Data.Components.FirstOrDefault(c => c.CustomId == "content")?.Value;
				if (string.IsNullOrEmpty(triggerName))
				{
					await modal.RespondAsync("Trigger name cannot be empty.");
					return;
				}
				if (string.IsNullOrEmpty(content))
				{
					await modal.RespondAsync("Trigger content cannot be empty.");
					return;
				}
				if (Program.TriggersResponsesDict.ContainsKey(triggerName))
				{
					await modal.RespondAsync($"Trigger with the name `{triggerName}` already exists.");
					return;
				}
				
				await Program.AddTriggerAndResponse(triggerName, content);
				
				await modal.RespondAsync($"Added trigger with name `{triggerName}`, and content: ```\n{content}\n```");
				break;
		}
	}

	private async Task HandleEdit(object context)
	{
		switch (context)
		{
			case SocketSlashCommand command:
			{
				var subcommand = command.Data.Options.First();
				var triggerName = subcommand.Options.FirstOrDefault(option => option.Name == "name")?.Value.ToString();
				if (string.IsNullOrEmpty(triggerName))
				{
					await command.RespondAsync("Trigger name cannot be empty.");
					return;
				}
				
				if (!Program.TriggersResponsesDict.TryGetValue(triggerName, out var currentContent))
				{
					await command.RespondAsync($"Trigger with the name `{triggerName}` not found.");
					return;
				}
				
				var editTriggerModal = new ModalBuilder()
					.WithTitle("Edit a trigger")
					.WithCustomId("trigger_edit")
					.AddTextInput("Trigger Words", "triggerer",
						placeholder: "Unity Hub download", value: triggerName,
						required: false
					)
					.AddTextInput("Content", "content", TextInputStyle.Paragraph,
						"hi i am edit", value: currentContent,
						required: false
					);

				await command.RespondWithModalAsync(editTriggerModal.Build());
				break;
			}
			case SocketModal modal:
				var id = modal.Data.Components.FirstOrDefault(c => c.CustomId == "triggerer")?.Value;
				var content = modal.Data.Components.FirstOrDefault(c => c.CustomId == "content")?.Value;
				
				if (string.IsNullOrEmpty(id))
				{
					await modal.RespondAsync("Trigger name cannot be empty.");
					return;
				}
				if (string.IsNullOrEmpty(content))
				{
					await modal.RespondAsync("Trigger content cannot be empty.");
					return;
				}
				
				if (!Program.TriggersResponsesDict.ContainsKey(id))
				{
					await modal.RespondAsync($"Trigger with the name `{id}` does not exist.");
					return;
				}
				
				Program.TriggersResponsesDict[id] = content;
				await Program.SaveTriggers();
				
				await modal.RespondAsync($"Trigger with the name `{id}` was updated.");
				break;
		}
	}
	
	private async Task HandleRemove(SocketSlashCommand context)
	{
		var subcommand = context.Data.Options.First();
		var triggerName = subcommand.Options.FirstOrDefault(option => option.Name == "name")?.Value.ToString();
		if (string.IsNullOrEmpty(triggerName))
		{
			await context.RespondAsync("Trigger name cannot be empty.");
			return;
		}
		
		if (!Program.TriggersResponsesDict.TryGetValue(triggerName, out _))
		{
			await context.RespondAsync($"Trigger with the name `{triggerName}` not found.");
			return;
		}
		await context.RespondAsync($"Removed trigger with name `{triggerName}`.");
	}
}