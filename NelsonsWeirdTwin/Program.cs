using Discord;
using Discord.Commands;
using Discord.Net;
using Discord.WebSocket;
using DotNetEnv;
using Newtonsoft.Json;
using System;
using System.Text.Json;
using JsonSerializer = Newtonsoft.Json.JsonSerializer;

namespace NelsonsWeirdTwin
{
    internal class Program
    {
        static string botToken;

        static DiscordSocketClient _client;
        static CommandService _commandService;

        static Dictionary<string,string> triggerWordsAndResponses;


        static async Task Main()
        {
            DotNetEnv.Env.Load("token.env");
            botToken = Environment.GetEnvironmentVariable("TOKEN");
            triggerWordsAndResponses = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText("triggers.json"));
            
            _client = new DiscordSocketClient();

            _client.Log += Log;

            _client.Ready += Client_Ready;

            await _client.LoginAsync(TokenType.Bot,botToken);
            await _client.StartAsync();
            while (true)
            {
                if (Console.ReadLine() == "ex")break; 
                if(Console.ReadLine() == "u") triggerWordsAndResponses = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText("triggers.json"));
            }
        }
        public static async Task Client_Ready()
        {
            List<SlashCommandBuilder> builders = new List<SlashCommandBuilder>();
            builders.Add(new());
            builders[0].WithName("pingmodtesters");
            builders[0].WithDescription("Pings the Mod tester role!");
            builders.Add(new());
            builders[1].WithName("addtrigger");
            builders[1].WithDescription("Adds a trigger and response to the bot");
            builders.Add(new()); // to be implemented, not sure how to avoid bricking it aside
            builders[2].WithName("edittrigger"); // from creating a copy entry and placing it
            builders[2].WithDescription("Edits an existing trigger"); 
            builders.Add(new()); 
            builders[3].WithName("verifyuser");
            builders[3].WithDescription("Swaps a user to the verified mod developer role!");

            try
            {
                foreach (SlashCommandBuilder c in builders)
                {
                    await _client.CreateGlobalApplicationCommandAsync(c.Build());
                }
            }
            catch (HttpException exception)
            {
                var json = JsonConvert.SerializeObject(exception.Errors, Formatting.Indented);

                Console.WriteLine(json);
            }
        }
        public static Task Log(LogMessage msg)
        {
            Console.WriteLine(msg);
            return Task.CompletedTask;
        }

        static async ValueTask MessageReceived(SocketMessage msg)
        {
            var message = msg as SocketUserMessage;
            if (message == null) return;
            if (msg.Author.IsBot) return;
            foreach (string k in triggerWordsAndResponses.Keys)
            {
                if (msg.Content.Contains(k))
                {
                    await msg.Channel.SendMessageAsync(triggerWordsAndResponses[k]);
                }
            }
            return;

        }
        static Task<string> AddTriggerAndResponse(string trigger, string response) 
        {
            string replaced = response.Replace("\\\\","\\");
            response = replaced.Replace("\\n","\n");
            triggerWordsAndResponses.Add(trigger, response);
            File.WriteAllText("triggers.json", JsonConvert.SerializeObject(triggerWordsAndResponses));
            return Task<string>.FromResult($"Trigger {trigger}:{response} added.");
        }
        static async Task<string> PingModTesters()
        {
            await Task.Delay(10);
            return "<@&1356043224510103692>";
        }
        //static async Task<string> VerifyCommand(GuildUser user)
        //{
        //    ulong userID = user.Id;
        //    await user.AddRoleAsync(1357079958954049717);
        //    await user.RemoveRoleAsync(1354831227152109590);
        //    return $"<@{userID}> has been made a Verified Mod Developer!";
        //}
    }
}
