using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DarkBot;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using Newtonsoft.Json;



namespace DarkBot.RandomImage
{
    [BotModuleDependency(new Type[] { typeof(Whitelist.Whitelist) })]
    public class RandomImage : BotModule
    {
        private DiscordSocketClient _client = null;
        private Whitelist.Whitelist _whitelist = null;
        Random rand = new Random();

        public Task Initialize(IServiceProvider services)
        {
            _client = services.GetService(typeof(DiscordSocketClient)) as DiscordSocketClient;
            _whitelist = services.GetService(typeof(Whitelist.Whitelist)) as Whitelist.Whitelist;
            _client.Ready += OnReady;
            _client.SlashCommandExecuted += HandleCommand;
            return Task.CompletedTask;
        }

        private async Task OnReady()
        {
            foreach (SocketGuild sg in _client.Guilds)
            {
                Log(LogSeverity.Info, sg.Name);
            }
            await SetupCommands();
            Log(LogSeverity.Info, "RandomImage ready!");
        }

        private async Task SetupCommands()
        {
            foreach (SocketApplicationCommand sac in await _client.GetGlobalApplicationCommandsAsync())
            {
                //await sac.DeleteAsync();
                if (sac.Name == "randomimage")
                {
                    Log(LogSeverity.Error, $"RandomImage command is already set up");
                    return;
                }
            }
            SlashCommandBuilder scb = new SlashCommandBuilder();
            scb.WithName("randomimage");
            scb.WithDescription("Post a random image");
            List<ChannelType> channelTypes = new List<ChannelType>();
            channelTypes.Add(ChannelType.Text);
            channelTypes.Add(ChannelType.DM);
            scb.AddOption("channel", ApplicationCommandOptionType.Channel, "Channel source", isRequired: true, channelTypes: channelTypes);
            try
            {
                Log(LogSeverity.Error, $"RandomImage command set up");
                await _client.CreateGlobalApplicationCommandAsync(scb.Build());
            }
            catch (Exception e)
            {
                Log(LogSeverity.Error, $"Error setting up slash command: {e.Message}");
            }
        }

        private async Task HandleCommand(SocketSlashCommand command)
        {
            if (command.CommandName != "randomimage")
            {
                return;
            }
            SocketTextChannel channel = command.Channel as SocketTextChannel;
            if (channel != null && !_whitelist.ObjectOK("randomimage", channel.Id))
            {
                await command.RespondAsync("You can only use this command in the allowed channels", ephemeral: true);
                return;
            }
            if (command.Data.Options.Count == 0)
            {
                await command.RespondAsync("You must select a channel");
                return;
            }
            SocketSlashCommandDataOption option = command.Data.Options.First<SocketSlashCommandDataOption>();
            SocketChannel sc = option.Value as SocketChannel;
            await PostImage(command, sc.Id);
        }

        private async Task PostImage(SocketSlashCommand command, ulong channelID)
        {
            string folderPath = Path.Combine(Environment.CurrentDirectory, "Backup");
            if (!Directory.Exists(folderPath))
            {
                await command.RespondAsync("Backup folder not found", ephemeral: true);
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
                await command.RespondAsync("Backup of channel not found", ephemeral: true);
                return;
            }
            string[] filesPath = Directory.GetFiles(channelPath);
            if (filesPath.Length == 0)
            {
                await command.RespondAsync("Backup of channel has no files", ephemeral: true);
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
                await command.RespondWithFileAsync(filePath, "");
                return;
            }
            await command.RespondAsync("Failed to find an image to post", ephemeral: true);
        }

        private void Log(LogSeverity severity, string text)
        {
            LogMessage logMessage = new LogMessage(severity, "RandomImage", text);
            Program.LogAsync(logMessage);
        }
    }
}
