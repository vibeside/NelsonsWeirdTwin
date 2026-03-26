using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using ErrorAnalyzer.Core;
using ErrorAnalyzer.Core.Models;

namespace NelsonsWeirdTwin;

internal sealed class SupportLogAnalyzer
{
	internal const string PaginationCustomIdPrefix = "support-log-page:";

	private static readonly HttpClient HttpClient = new();
	private static readonly string[] SupportedAttachmentExtensions = [".log", ".txt"];
	private static readonly ConcurrentDictionary<ulong, PaginationStates> Sessions = new();

	private const int MaxAttachmentBytes = 10 * 1024 * 1024;
	private const int IssueGroupsPerPage = 3;
	private const int MaxEmbedFieldLength = 1024;
	private static readonly TimeSpan AnalysisLifetime = TimeSpan.FromMinutes(14.5);

	private readonly LogAnalyzer _analyzer = new();

	internal async Task AnalyzeMessageAsync(SocketSlashCommand context, IMessage targetMessage)
	{
		LogAnalysisResponse response;
		try
		{
			response = await BuildResponseAsync(targetMessage);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Support log analysis failed: {ex}");
			response = LogAnalysisResponse.FromStatus(BuildStatusEmbed(
				$"message {targetMessage.Id}",
				"I hit an error while analyzing that log. Please try again or let a helper review it manually.",
				Color.Orange));
		}

		await SendResponseAsync(context, response);
	}

	internal async Task<bool> TryHandlePaginationAsync(SocketMessageComponent component)
	{
		if (!component.Data.CustomId.StartsWith(PaginationCustomIdPrefix, StringComparison.Ordinal))
		{
			return false;
		}

		try
		{
			await component.DeferAsync();
		}
		catch (Exception ex)
		{
			var interactionAgeMs = (DateTimeOffset.UtcNow - component.CreatedAt).TotalMilliseconds;
			Console.WriteLine($"[SupportLogAnalyzer] Failed to defer pagination interaction for message {component.Message?.Id}: {ex.Message} (interaction age: {interactionAgeMs:F0}ms)");
			return true;
		}

		if (!Sessions.TryGetValue(component.Message.Id, out var state) || state.ExpiresAt <= DateTimeOffset.UtcNow)
		{
			Sessions.TryRemove(component.Message.Id, out _);
			await TryShowExpiredResponseAsync(component);
			return true;
		}

		if (component.User.Id != state.OwnerUserId)
		{
			await component.FollowupAsync("Only the user who ran this analysis can change pages.", ephemeral: true);
			return true;
		}

		var action = component.Data.CustomId[PaginationCustomIdPrefix.Length..];
		lock (state.SyncRoot)
		{
			if (action == "prev" && state.CurrentPage > 0)
			{
				state.CurrentPage -= 1;
			}
			else if (action == "next" && state.CurrentPage < state.Embeds.Count - 1)
			{
				state.CurrentPage += 1;
			}
		}

		try
		{
			await component.ModifyOriginalResponseAsync(properties =>
			{
				properties.Content = null;
				properties.Embed = state.Embeds[state.CurrentPage];
				properties.Components = BuildPaginationComponents(state.CurrentPage, state.Embeds.Count);
			});
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[SupportLogAnalyzer] Failed interactive pagination update for message {component.Message?.Id}: {ex.Message}");
		}

		return true;
	}

	private async Task<LogAnalysisResponse> BuildResponseAsync(IMessage message)
	{
		var input = await TryExtractLogInputAsync(message);
		if (input == null)
		{
			return LogAnalysisResponse.FromStatus(BuildStatusEmbed(
				$"message {message.Id}",
				"I couldn't find a supported `.log` or `.txt` attachment on that message.",
				Color.Orange));
		}

		if (!string.IsNullOrWhiteSpace(input.ErrorMessage))
		{
			return LogAnalysisResponse.FromStatus(BuildStatusEmbed(input.SourceName, input.ErrorMessage, Color.Orange));
		}

		if (!ContainsException(input.Text))
		{
			return LogAnalysisResponse.FromStatus(BuildStatusEmbed(
				input.SourceName,
				"I couldn't find an exception in that log, so there isn't anything to analyze automatically.",
				Color.Orange));
		}

		var pageSet = BuildPages(input);
		if (pageSet == null)
		{
			return LogAnalysisResponse.FromStatus(BuildStatusEmbed(
				input.SourceName,
				"I couldn't identify any actionable issue groups in that log.",
				Color.Blue));
		}

		return new LogAnalysisResponse(pageSet.Embeds);
	}

	private async Task<LogInput> TryExtractLogInputAsync(IMessage message)
	{
		foreach (var attachment in message.Attachments)
		{
			if (!HasSupportedExtension(attachment.Filename))
			{
				continue;
			}

			if (attachment.Size > MaxAttachmentBytes)
			{
				return new LogInput(
					attachment.Filename,
					string.Empty,
					$"`{attachment.Filename}` is too large to analyze automatically. Please keep it under {MaxAttachmentBytes / 1024 / 1024} MB.");
			}

			var text = await HttpClient.GetStringAsync(attachment.Url);
			return new LogInput(attachment.Filename, text);
		}

		return null;
	}

	private async Task SendResponseAsync(SocketSlashCommand context, LogAnalysisResponse response)
	{
		await context.ModifyOriginalResponseAsync(properties =>
		{
			properties.Content = null;
			properties.Embed = response.Embeds[0];
			properties.Components = BuildPaginationComponents(0, response.Embeds.Count);
		});

		var responseMessage = await context.GetOriginalResponseAsync();
		var expiresAt = DateTimeOffset.UtcNow.Add(AnalysisLifetime);
		if (response.Embeds.Count > 1)
		{
			Sessions[responseMessage.Id] = new PaginationStates(response.Embeds, context.User.Id, expiresAt);
		}

		ScheduleResponseCleanup(context, responseMessage.Id, expiresAt);
	}

	private void ScheduleResponseCleanup(SocketSlashCommand context, ulong responseMessageId, DateTimeOffset expiresAt)
	{
		Program.RunBackgroundTask(nameof(ScheduleResponseCleanup), async () =>
		{
			var delay = expiresAt - DateTimeOffset.UtcNow;
			if (delay > TimeSpan.Zero)
			{
				await Task.Delay(delay);
			}

			Sessions.TryRemove(responseMessageId, out _);

			try
			{
				await context.DeleteOriginalResponseAsync();
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[SupportLogAnalyzer] Failed to delete expired analysis response {responseMessageId}: {ex.Message}");
			}
		});
	}

	private static async Task TryShowExpiredResponseAsync(SocketMessageComponent component)
	{
		try
		{
			await component.ModifyOriginalResponseAsync(properties =>
			{
				properties.Content = null;
				properties.Embed = BuildStatusEmbed(
					"discord-log",
					"This log analysis expired. Run `/analyze message` again if you still need it.",
					Color.DarkGrey);
				properties.Components = null;
			});
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[SupportLogAnalyzer] Failed to show expired state for message {component.Message?.Id}: {ex.Message}");
		}
	}

	private PageSet BuildPages(LogInput input)
	{
		var result = _analyzer.AnalyzeTextAsDto(input.Text);
		var findings = SelectOneFindingPerMod(result.Diagnoses);
		if (findings.Count == 0)
		{
			return null;
		}

		var adviceGroups = BuildAdviceGroupPageItems(result);
		if (adviceGroups.Count == 0)
		{
			return null;
		}

		var embeds = new List<Embed>();
		var pageCount = (int)Math.Ceiling(adviceGroups.Count / (double)IssueGroupsPerPage);

		for (var pageIndex = 0; pageIndex < pageCount; pageIndex++)
		{
			var pageItems = adviceGroups
				.Skip(pageIndex * IssueGroupsPerPage)
				.Take(IssueGroupsPerPage)
				.ToArray();

			var embedBuilder = new EmbedBuilder()
				.WithTitle($"Log analysis for {input.SourceName}")
				.WithColor(GetColor(pageItems.SelectMany(pageItem => pageItem.Diagnoses)));

			foreach (var pageItem in pageItems)
			{
				embedBuilder.AddField(
					EscapeMarkdown(pageItem.Group.Title),
					BuildAdviceGroupFieldValue(pageItem.Group),
					false);
			}

			var embed = embedBuilder
				.AddField("Runtime", result.Runtime, true)
				.AddField("Issue Groups", adviceGroups.Count.ToString(), true)
				.AddField("Affected Mods", findings.Count.ToString(), true)
				.WithFooter($"Showing issues {pageIndex * IssueGroupsPerPage + 1}-{pageIndex * IssueGroupsPerPage + pageItems.Length} of {adviceGroups.Count} - Page {pageIndex + 1}/{pageCount}")
				.Build();

			embeds.Add(embed);
		}

		return new PageSet(embeds);
	}

	private static List<AdviceGroupPageItem> BuildAdviceGroupPageItems(LogAnalysisResultDto result)
	{
		var diagnosesByGroupKey = result.Diagnoses
			.GroupBy(diagnosis => diagnosis.Advice.GroupKey, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(
				group => group.Key,
				group => (IReadOnlyList<DiagnosisDto>)group.ToList(),
				StringComparer.OrdinalIgnoreCase);

		var adviceGroups = result.AdviceGroups is { Count: > 0 }
			? result.AdviceGroups
			: BuildAdviceGroupsFromDiagnoses(result.Diagnoses);

		return adviceGroups
			.Select(group => new AdviceGroupPageItem(
				group,
				diagnosesByGroupKey.TryGetValue(group.GroupKey, out var diagnoses)
					? diagnoses
					: Array.Empty<DiagnosisDto>()))
			.ToList();
	}

	private static IReadOnlyList<DiagnosisAdviceGroupDto> BuildAdviceGroupsFromDiagnoses(IReadOnlyList<DiagnosisDto> diagnoses)
	{
		return diagnoses
			.GroupBy(diagnosis => diagnosis.Advice.GroupKey, StringComparer.OrdinalIgnoreCase)
			.Select(group =>
			{
				var primaryDiagnosis = group
					.OrderBy(diagnosis => diagnosis.Advice.Priority)
					.ThenBy(diagnosis => diagnosis.LineNumber)
					.First();
				var affectedMods = group
					.Select(diagnosis => string.IsNullOrWhiteSpace(diagnosis.ModName) ? "Unknown mod" : diagnosis.ModName.Trim())
					.Distinct(StringComparer.OrdinalIgnoreCase)
					.OrderBy(modName => modName, StringComparer.OrdinalIgnoreCase)
					.ToArray();

				return new DiagnosisAdviceGroupDto(
					primaryDiagnosis.Advice.GroupKey,
					primaryDiagnosis.Advice.Priority,
					primaryDiagnosis.Advice.Urgency,
					primaryDiagnosis.Advice.Title,
					primaryDiagnosis.Advice.PrimaryAction,
					primaryDiagnosis.Advice.Explanation,
					affectedMods,
					group.Count(),
					group.Sum(diagnosis => diagnosis.OccurrenceCount));
			})
			.OrderBy(group => group.Priority)
			.ThenBy(group => group.Title, StringComparer.OrdinalIgnoreCase)
			.ToArray();
	}

	private static string BuildAdviceGroupFieldValue(DiagnosisAdviceGroupDto group)
	{
		var action = !string.IsNullOrWhiteSpace(group.PrimaryAction)
			? group.PrimaryAction
			: !string.IsNullOrWhiteSpace(group.Explanation)
				? group.Explanation
				: "Review the affected mods and their install state.";
		var modLabel = group.AffectedMods.Count == 1
			? "Affected mod"
			: $"Affected mods ({group.AffectedMods.Count})";
		var prefix = $"{action}\n\n{modLabel}: ";
		var availableLength = Math.Max(32, MaxEmbedFieldLength - prefix.Length);

		return prefix + BuildModList(group.AffectedMods, availableLength);
	}

	private static string BuildModList(IReadOnlyList<string> modNames, int maxLength)
	{
		if (modNames.Count == 0)
		{
			return "Unknown mod";
		}

		var builder = new StringBuilder();
		for (var index = 0; index < modNames.Count; index++)
		{
			var escapedModName = EscapeMarkdown(modNames[index]);
			var segment = builder.Length == 0 ? escapedModName : $", {escapedModName}";
			if (builder.Length + segment.Length <= maxLength)
			{
				builder.Append(segment);
				continue;
			}

			var remainingCount = modNames.Count - index;
			var suffix = builder.Length == 0
				? $"+{remainingCount} mod{(remainingCount == 1 ? string.Empty : "s")}"
				: $", +{remainingCount} more";

			if (builder.Length + suffix.Length <= maxLength)
			{
				builder.Append(suffix);
			}

			break;
		}

		return builder.Length == 0 ? "Unknown mod" : builder.ToString();
	}

	private static List<DiagnosisDto> SelectOneFindingPerMod(IReadOnlyList<DiagnosisDto> diagnoses)
	{
		return diagnoses
			.GroupBy(diagnosis => string.IsNullOrWhiteSpace(diagnosis.ModName) ? "Unknown mod" : diagnosis.ModName, StringComparer.OrdinalIgnoreCase)
			.Select(group => group
				.OrderBy(diagnosis => diagnosis.Advice.Priority)
				.ThenByDescending(diagnosis => GetSeverityRank(diagnosis.Severity))
				.ThenByDescending(diagnosis => GetConfidenceRank(diagnosis.Confidence))
				.ThenByDescending(diagnosis => diagnosis.OccurrenceCount)
				.First())
			.OrderBy(diagnosis => diagnosis.Advice.Priority)
			.ThenByDescending(diagnosis => GetSeverityRank(diagnosis.Severity))
			.ThenBy(diagnosis => diagnosis.ModName ?? "Unknown mod", StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	private static MessageComponent BuildPaginationComponents(int currentPage, int pageCount)
	{
		if (pageCount <= 1)
		{
			return null;
		}

		return new ComponentBuilder()
			.WithButton("Previous", PaginationCustomIdPrefix + "prev", ButtonStyle.Secondary, disabled: currentPage == 0)
			.WithButton("Next", PaginationCustomIdPrefix + "next", ButtonStyle.Secondary, disabled: currentPage >= pageCount - 1)
			.Build();
	}

	private static Embed BuildStatusEmbed(string sourceName, string message, Color color)
	{
		return new EmbedBuilder()
			.WithTitle($"Log analysis for {sourceName}")
			.WithColor(color)
			.WithDescription(message)
			.WithFooter($"Powered by ErrorAnalyzer.Core {ErrorAnalyzerBuildInfo.Version}")
			.Build();
	}

	private static bool HasSupportedExtension(string filename)
	{
		return SupportedAttachmentExtensions.Any(extension => filename.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
	}

	private static bool ContainsException(string text)
	{
		return text.Contains("Exception", StringComparison.OrdinalIgnoreCase);
	}

	private static int GetSeverityRank(string severity)
	{
		return severity switch
		{
			"Error" => 3,
			"Warning" => 2,
			_ => 1,
		};
	}

	private static int GetConfidenceRank(string confidence)
	{
		return confidence switch
		{
			"High" => 3,
			"Medium" => 2,
			_ => 1,
		};
	}

	private static Color GetColor(IEnumerable<DiagnosisDto> diagnoses)
	{
		if (diagnoses.Any(diagnosis => string.Equals(diagnosis.Severity, "Error", StringComparison.OrdinalIgnoreCase)))
		{
			return Color.Red;
		}

		if (diagnoses.Any(diagnosis => string.Equals(diagnosis.Severity, "Warning", StringComparison.OrdinalIgnoreCase)))
		{
			return Color.Orange;
		}

		return Color.Blue;
	}

	private static string EscapeMarkdown(string value)
	{
		return value
			.Replace("\\", "\\\\", StringComparison.Ordinal)
			.Replace("`", "\\`", StringComparison.Ordinal)
			.Replace("*", "\\*", StringComparison.Ordinal)
			.Replace("_", "\\_", StringComparison.Ordinal);
	}

	private sealed record LogInput(string SourceName, string Text, string ErrorMessage = null);

	private sealed record AdviceGroupPageItem(DiagnosisAdviceGroupDto Group, IReadOnlyList<DiagnosisDto> Diagnoses);

	private sealed class PaginationStates
	{
		internal PaginationStates(IReadOnlyList<Embed> embeds, ulong ownerUserId, DateTimeOffset expiresAt)
		{
			Embeds = embeds;
			OwnerUserId = ownerUserId;
			ExpiresAt = expiresAt;
		}

		internal object SyncRoot { get; } = new();
		internal IReadOnlyList<Embed> Embeds { get; }
		internal ulong OwnerUserId { get; }
		internal DateTimeOffset ExpiresAt { get; }
		internal int CurrentPage { get; set; }
	}

	private sealed record LogAnalysisResponse(IReadOnlyList<Embed> Embeds)
	{
		internal static LogAnalysisResponse FromStatus(Embed embed)
		{
			return new LogAnalysisResponse([embed]);
		}
	}

	private sealed record PageSet(IReadOnlyList<Embed> Embeds);
}
