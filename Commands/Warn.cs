using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NelsonsWeirdTwin.Commands
{
    internal class Warn : Command
    {
        // TODO Warn JSON
        // TODO Warn Logic
        internal override SlashCommandProperties CommandProperties =>
            new SlashCommandBuilder()
            .WithName("warn")
            .WithDescription("Warns a user.")
            .Build();
    }
}
