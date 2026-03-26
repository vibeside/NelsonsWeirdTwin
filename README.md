Simple discord bot with auto responses to common phrases using modals, events, and string checking.

## Support Log Analysis

- Log analysis is opt-in through `/analyze message target:<message-id-or-link>`.
- The response is ephemeral, keeps the paginated embed flow, and is cleaned up after roughly 15 minutes.
- `/analyze settings list`
- `/analyze settings add-channel channel:<channel>`
- `/analyze settings remove-channel channel:<channel>`
- Settings are stored in `support-log-settings.json`.
- On first run, if `support-log-settings.json` does not exist, it is seeded from `SUPPORT_CHANNEL_IDS`.
- If `SUPPORT_CHANNEL_IDS` is not set on first run, the bot seeds the default production support channel (`1354832385128271922`).

## ErrorAnalyzer.Core dependency

- The bot uses the `ScheduleOne.ErrorAnalyzer.Core` package from NuGet.
- The dependency switch is isolated in `NelsonsWeirdTwin.csproj`:
  - local project mode: `UseLocalErrorAnalyzerCore=true`
  - package mode: `UseLocalErrorAnalyzerCore=false` with `ErrorAnalyzerCoreVersion=*`
- Use the package mode for normal builds, and switch to the local project mode when developing `ErrorAnalyzer.Core` side by side with the bot.
