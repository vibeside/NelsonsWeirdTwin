using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace NelsonsWeirdTwin;

internal sealed class SupportLogSettingsStore
{
	private const string SettingsFilePath = "support-log-settings.json";

	private readonly ulong _defaultProductionSupportChannelId;

	internal SupportLogSettingsStore(ulong defaultProductionSupportChannelId)
	{
		_defaultProductionSupportChannelId = defaultProductionSupportChannelId;
	}

	internal HashSet<ulong> LoadChannelIds()
	{
		var settings = LoadSettings();
		return settings.ChannelIds
			.Where(channelId => channelId != 0)
			.ToHashSet();
	}

	internal void SaveChannelIds(IEnumerable<ulong> channelIds)
	{
		var distinctChannelIds = channelIds
			.Where(channelId => channelId != 0)
			.Distinct()
			.OrderBy(channelId => channelId)
			.ToList();

		var settings = new SupportLogSettings
		{
			ChannelIds = distinctChannelIds
		};
		File.WriteAllText(SettingsFilePath, JsonConvert.SerializeObject(settings, Formatting.Indented));
	}

	private SupportLogSettings LoadSettings()
	{
		if (File.Exists(SettingsFilePath))
		{
			try
			{
				var rawSettings = File.ReadAllText(SettingsFilePath);
				var settings = JsonConvert.DeserializeObject<SupportLogSettings>(rawSettings);
				if (settings?.ChannelIds != null)
				{
					Console.WriteLine($"Loaded support log settings from {SettingsFilePath}.");
					return settings;
				}

				Console.WriteLine($"{SettingsFilePath} did not contain any usable settings. Re-seeding from environment/defaults.");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Failed to load {SettingsFilePath}: {ex.Message}. Re-seeding from environment/defaults.");
			}
		}

		var seededChannelIds = ParseChannelIdsFromEnvironment();
		if (seededChannelIds.Count == 0)
		{
			seededChannelIds.Add(_defaultProductionSupportChannelId);
			Console.WriteLine($"Support log settings were seeded with the default production support channel ({_defaultProductionSupportChannelId}).");
		}
		else
		{
			Console.WriteLine($"Support log settings were seeded from SUPPORT_CHANNEL_IDS into {SettingsFilePath}.");
		}

		SaveChannelIds(seededChannelIds);
		return new SupportLogSettings
		{
			ChannelIds = seededChannelIds.OrderBy(channelId => channelId).ToList()
		};
	}

	private static List<ulong> ParseChannelIdsFromEnvironment()
	{
		var channelIds = new List<ulong>();
		var rawValue = Environment.GetEnvironmentVariable("SUPPORT_CHANNEL_IDS");
		if (string.IsNullOrWhiteSpace(rawValue))
		{
			return channelIds;
		}

		foreach (var token in rawValue.Split([',', ';', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			if (!ulong.TryParse(token, out var channelId))
			{
				Console.WriteLine($"Ignoring invalid support channel ID '{token}'.");
				continue;
			}

			channelIds.Add(channelId);
		}

		return channelIds;
	}

	private sealed class SupportLogSettings
	{
		[JsonProperty("channelIds")]
		public List<ulong> ChannelIds { get; init; } = [];
	}
}
