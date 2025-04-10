using DotNetEnv;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;
using NetCord.Services;
using NetCord.Services.ApplicationCommands;
using System.Text.Json;

namespace NelsonsWeirdTwin
{
    internal class Program
    {
        public static IEnumerable<KeyValuePair<string, string>> env = Env.Load();
        
        static string botToken = Environment.GetEnvironmentVariable("TOKEN");

        public static GatewayClient client = new(new BotToken(botToken),new GatewayClientConfiguration()
        {
            Intents = GatewayIntents.All
        });

        static Dictionary<string,string> triggerWordsAndResponses;

        public static ApplicationCommandService<SlashCommandContext> appCommandServices = new();

        static async Task Main()
        {
            triggerWordsAndResponses = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText("triggers.json"));
            client.Log += message =>
            {
                Console.WriteLine(message);
                return default;
            };

            appCommandServices.AddSlashCommand("pingmodtesters", "Pings the mod tester role", PingModTesters);
            appCommandServices.AddSlashCommand("verifyuser", "Swaps a users unverified and verified role.", VerifyCommand);
            appCommandServices.AddSlashCommand("addtrigger", "Adds a trigger word and phrase", AddTriggerAndResponse);
            client.InteractionCreate += async interaction =>
            {
                if (interaction is not SlashCommandInteraction appCommandInteraction) return;
                var result = await appCommandServices.ExecuteAsync(new SlashCommandContext(appCommandInteraction, client));
                
                if (result is not IFailResult fail) return;

                try
                {
                    await interaction.SendResponseAsync(InteractionCallback.Message(fail.Message));
                }
                catch
                {

                }
            };
            client.MessageCreate += MessageReceived;
            await appCommandServices.CreateCommandsAsync(client.Rest, client.Id);
            await client.StartAsync();
            while (true)
            {
                if (Console.ReadLine() == "ex")break; 
                if(Console.ReadLine() == "u") triggerWordsAndResponses = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText("triggers.json"));
            }
        }

        static async ValueTask MessageReceived(Message msg)
        {

            if (msg.Author.Id == client.Id) return;
            if (msg.Author.IsBot) return;
            if (msg.Author.IsSystemUser == true) return;
            foreach (string k in triggerWordsAndResponses.Keys)
            {
                if (msg.Content.Contains(k))
                {
                    await client.Rest.SendMessageAsync(msg.ChannelId, triggerWordsAndResponses[k]);
                }
            }
            return;

        }
        static Task<string> AddTriggerAndResponse(string trigger, string response) 
        {
            string replaced = response.Replace("\\\\","\\");
            response = replaced.Replace("\\n","\n");
            triggerWordsAndResponses.Add(trigger, response);
            File.WriteAllText("triggers.json", JsonSerializer.Serialize(triggerWordsAndResponses));
            return Task<string>.FromResult($"Trigger {trigger}:{response} added.");
        }
        static async Task<string> PingModTesters()
        {
            await Task.Delay(10);
            return "<@&1356043224510103692>";
        }
        static async Task<string> VerifyCommand(GuildUser user)
        {
            ulong userID = user.Id;
            await user.AddRoleAsync(1357079958954049717);
            await user.RemoveRoleAsync(1354831227152109590);
            return $"<@{userID}> has been made a Verified Mod Developer!";
        }
    }
}
