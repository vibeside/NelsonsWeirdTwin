using System;
using Discord;

namespace NelsonsWeirdTwin;

internal static class Utils
{
	internal static string Plural(int count, string singular, string plural = "s")
	{
		if (plural == "s")
			return count == 1 ? singular : singular + plural;
		else
			return count == 1 ? singular : plural;
	}
	internal static Color RandColor(ulong seed, bool alpha = false)
	{
		var rand = new Random((int)(seed & 0xFFFFFFFF));
		byte r = (byte)rand.Next(256);
		byte g = (byte)rand.Next(256);
		byte b = (byte)rand.Next(256);
		byte a = (byte)rand.Next(256);
		return Color.Parse($"{r}{g}{b}{(alpha ? a : "")}");

    }
}