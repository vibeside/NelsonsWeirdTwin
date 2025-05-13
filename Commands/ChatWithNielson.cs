using Discord;
using Discord.WebSocket;
using NelsonsWeirdTwin.Extensions;
using OllamaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NelsonsWeirdTwin.Commands
{
    internal class ChatWithNielson : Command
    {

        internal override SlashCommandProperties CommandProperties =>
            new SlashCommandBuilder()
                .WithName("chat")
                .WithDescription("Lets you chat with uncle nielson")
                .AddOption(
                    "info",
                    ApplicationCommandOptionType.SubCommand,
                    "Information about chatting with the bot."
                )
                .AddOption(
                    new SlashCommandOptionBuilder()
                        .WithName("message")
                        .WithType(ApplicationCommandOptionType.SubCommand)
                        .WithDescription("Talk with nielson!")
                        .AddOption("content", 
                        ApplicationCommandOptionType.String, 
                        "The prompt for ai", 
                        isRequired:true)
                )
                .Build();
        internal async override Task OnExecuted(DiscordSocketClient client, SocketSlashCommand context)
        {
            context.DeferAsync();
            var option = context.Data.Options.First().Name;
            switch (option)
            {
                case "message":
                    await HandleMessage(context);
                    break;
                case "info":
                    await context.ModifyOriginalMessageAsync("Chatting with the robot does not send any data anywhere. \n" +
                        "Chats are not saved locally, or remotely. I hallucinate, do not believe my lies.\n" +
                        "All prompts are logged into the discord. All Responses are logged locally.\n" +
                        "Breaking discord TOS is an instant ban.");
                    break;
                default:
                    await context.ModifyOriginalMessageAsync("Not found.");
                    break;
            }
        }
        internal static async Task HandleMessage(SocketSlashCommand context)
        {
            var realContext = context.Data.Options.FirstOrDefault(x => x.Name == "message");
            string prompt = realContext.Options.FirstOrDefault(x => x.Name == "content")?.Value as string;
            prompt = prompt == "" ? "NULL" : prompt;
            prompt ??= "NULL";
            if (prompt == "NULL")
            {
                await context.ModifyOriginalMessageAsync("I dont know how this happened, but get fucked.");
                return;
            }
            
            Program.genericRequest.Prompt = prompt;
            var nielsonsResponse = await Program.ollama.GenerateAsync(Program.genericRequest).StreamToEndAsync();
            
            StringBuilder logBuilder = new();
            logBuilder.AppendLine($"======================================================");
            logBuilder.AppendLine($"User {context.User.GlobalName} chatted at {DateTime.UtcNow}");
            logBuilder.AppendLine($"Prompt:{prompt}");
            var channel = Program.Client.GetChannel(1371686608457437325);
            // repo add your test server here if you plan on using it.
            channel ??= Program.Client.GetChannel(1368263313531732011); 
            (channel as ISocketMessageChannel).
                SendMessageAsync(logBuilder.ToString());
            logBuilder.AppendLine($"Response:\n{nielsonsResponse}");

            File.AppendAllText("ailogs.txt",logBuilder.ToString());

            await context.ModifyOriginalMessageAsync(nielsonsResponse.Response + "\n-# I lie. Alot. Do not believe anything I say about the game.\n" +
                "-# Asking for anything that would break the discord TOS will get you banned. No Hesitation.");
        }

    }
}
