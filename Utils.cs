namespace NelsonsWeirdTwin;

internal static class Utils
{
	internal static string Plural(int count, string singular, string plural)
	{
		return count == 1 ? singular : plural;
	}
}