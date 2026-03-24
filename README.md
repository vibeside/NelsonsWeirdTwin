Simple discord bot with auto responses to common phrases using modals, events, and string checking.

Automatic log analysis defaults to the S1 Modding #support channel (`1354832385128271922`).

Set `SUPPORT_CHANNEL_IDS` to a comma-separated list of Discord channel IDs to override that default and control where automatic log analysis replies run for `.log`, `.txt`, or pasted error logs.

## ErrorAnalyzer.Core dependency

- The bot uses the `ScheduleOne.ErrorAnalyzer.Core` package from NuGet.
- The dependency switch is isolated in `NelsonsWeirdTwin.csproj`:
  - local project mode: `UseLocalErrorAnalyzerCore=true`
  - package mode: `UseLocalErrorAnalyzerCore=false` with `ErrorAnalyzerCoreVersion=*`
- Use the package mode for normal builds, and switch to the local project mode when developing `ErrorAnalyzer.Core` side by side with the bot.
