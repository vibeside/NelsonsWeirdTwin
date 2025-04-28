using System.Runtime.Loader;
using System.Threading.Tasks;
using Discord.WebSocket;

namespace NelsonsWeirdTwin.Extensions;

public static class ContextExtensions
{
	public static async Task ModifyOriginalMessageAsync(this SocketSlashCommand context, string message, int deleteAfterMS = -1)
	{
		await context.ModifyOriginalResponseAsync(properties => properties.Content = message);

		if (deleteAfterMS > 0)
		{
			await Task.Delay(deleteAfterMS);
			await context.DeleteOriginalResponseAsync();
		}
	}
}