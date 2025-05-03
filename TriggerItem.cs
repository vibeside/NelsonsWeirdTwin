using System;
using System.Collections.Generic;

namespace NelsonsWeirdTwin;

[Serializable]
public record TriggerItem
{
	public string Id { get; set; } = string.Empty;
	
	public HashSet<string> Aliases { get; set; } = []; // Hashset to force uniqueness
	public string Response { get; set; } = string.Empty;

	public int TimesTriggered { get; set; } = 0;
}