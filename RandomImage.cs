using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using DarkBot;
using Discord;
using Discord.Net;
using Discord.WebSocket;



namespace DarkBot.RandomImage
{
    [BotModuleDependency(new Type[] { typeof(Whitelist.Whitelist) })]
    public class RandomImage : BotModule
    {
        Dictionary<string, ulong> commands = new Dictionary<string, ulong>();
        private DiscordSocketClient _client = null;
        private Whitelist.Whitelist _whitelist = null;
        private string prefix = "-";
        Random rand = new Random();

        public Task Initialize(IServiceProvider services)
        {
            _client = services.GetService(typeof(DiscordSocketClient)) as DiscordSocketClient;
            _whitelist = services.GetService(typeof(Whitelist.Whitelist)) as Whitelist.Whitelist;
            _client.Ready += OnReady;
            _client.MessageReceived += MessageReceived;
            return Task.CompletedTask;
        }

        private Task OnReady()
        {
            LoadDatabase();
            Log(LogSeverity.Info, "RandomImage ready!");
            return Task.CompletedTask;
        }

        public async Task MessageReceived(SocketMessage socketMessage)
        {
            SocketUserMessage message = socketMessage as SocketUserMessage;
            if (message == null || message.Author.IsBot)
            {
                return;
            }
            SocketTextChannel channel = message.Channel as SocketTextChannel;
            if (channel == null)
            {
                return;
            }
            if (socketMessage.Content.Length == 0 || !socketMessage.Content.StartsWith(prefix))
            {
                return;
            }
            if (!_whitelist.ObjectOK("randomimage", channel.Id))
            {
                return;
            }
            string command = socketMessage.Content.Substring(prefix.Length);
            if (command == "list")
            {
                await PostHelp(channel);
            }
            if (commands.ContainsKey(command))
            {
                await PostImage(channel, commands[command]);
            }
        }

        private async Task PostHelp(SocketTextChannel channel)
        {
            StringBuilder sb = new StringBuilder();
            foreach (KeyValuePair<string, ulong> kvp in commands)
            {
                sb.AppendLine(prefix + kvp.Key);
                if (sb.Length > 1500)
                {
                    await Say(channel, sb.ToString());
                    sb.Clear();
                }
            }
            await Say(channel, sb.ToString());
        }

        private async Task PostImage(SocketTextChannel stc, ulong channelID)
        {
            string folderPath = Path.Combine(Environment.CurrentDirectory, "Backup");
            if (!Directory.Exists(folderPath))
            {
                return;
            }
            string[] whitelistPaths = Directory.GetDirectories(folderPath);
            string channelPath = null;
            foreach (string testDir in whitelistPaths)
            {
                string testPath = Path.Combine(testDir, channelID.ToString());
                if (Directory.Exists(testPath))
                {
                    channelPath = testPath;
                    break;
                }
            }
            if (channelPath == null)
            {
                return;
            }
            string[] filesPath = Directory.GetFiles(channelPath);
            if (filesPath.Length == 0)
            {
                return;
            }
            for (int tries = 0; tries < 10; tries++)
            {
                int fileID = rand.Next(filesPath.Length - 1);
                string filePath = filesPath[fileID];
                if (!File.Exists(filePath))
                {
                    continue;
                }
                long fileLength = new FileInfo(filePath).Length;
                if (fileLength > 7 * 1024 * 1024)
                {
                    continue;
                }
                await stc.SendFileAsync(filePath, "");
                break;
            }
        }

        private async Task Say(SocketTextChannel stc, string message)
        {
            await stc.SendMessageAsync(message);
        }

        private void Log(LogSeverity severity, string text)
        {
            LogMessage logMessage = new LogMessage(severity, "RandomImage", text);
            Program.LogAsync(logMessage);
        }

        private void LoadDatabase()
        {
            commands.Clear();
            string databaseString = DataStore.Load("RandomImage");
            using (StringReader sr = new StringReader(databaseString))
            {
                string currentLine = null;
                while ((currentLine = sr.ReadLine()) != null)
                {
                    int index = currentLine.IndexOf("=");
                    string lhs = currentLine.Substring(0, index);
                    string rhs = currentLine.Substring(index + 1);
                    if (ulong.TryParse(lhs, out ulong lhsParse))
                    {
                        commands.Add(rhs, lhsParse);
                    }
                }
            }
        }
    }
}
