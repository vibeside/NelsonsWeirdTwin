using System;
using System.Collections.Generic;

namespace NelsonsWeirdTwin;

[Serializable]
public record TriggerItem
{
	public string Id { get; set; } = string.Empty;
	
	public List<string> Aliases { get; set; } = [];
	public string Response { get; set; } = string.Empty;

	public int TimesTriggered { get; set; } = 0;
}