using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Threading;
using YamlDotNet.Core;

namespace MothBot
{
    public class Program
    {
        private DiscordSocketClient _client;
        private readonly CommandService _commands;
        private readonly IServiceProvider _services;
        static void Main(string[] args) => new Program().MainAsync().GetAwaiter().GetResult();

        private Program()
        {
            _client = new DiscordSocketClient(new DiscordSocketConfig
            {
                LogLevel = LogSeverity.Info,
            });
                
            _commands = new CommandService(new CommandServiceConfig
            {
                LogLevel = LogSeverity.Info,
                CaseSensitiveCommands = false,
            });
            // Subscribe the logging handler to both the client and the CommandService.
            _client.Log += Log;
            _commands.Log += Log;
        }

        // Example of a logging handler. This can be re-used by addons
        // that ask for a Func<LogMessage, Task>.
        private static Task Log(LogMessage message)
        {
            switch (message.Severity)
            {
                case LogSeverity.Critical:
                case LogSeverity.Error:
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;
                case LogSeverity.Warning:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    break;
                case LogSeverity.Info:
                    Console.ForegroundColor = ConsoleColor.White;
                    break;
                case LogSeverity.Verbose:
                case LogSeverity.Debug:
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    break;
            }
            Console.WriteLine($"{DateTime.Now,-19} [{message.Severity,8}] {message.Source}: {message.Message} {message.Exception}");
            Console.ResetColor();
            return Task.CompletedTask;
        }
        public async Task MainAsync()
        {
            //Console.WriteLine(RuntimeInformation.FrameworkDescription);
            Info.setUpDicts();
            var _config = new DiscordSocketConfig { MessageCacheSize = 100, GatewayIntents = GatewayIntents.All, AlwaysDownloadUsers = true };
            _client = new DiscordSocketClient(_config);
            _client.MessageReceived += HandleCommandAsync;
            //_commands.AddTypeReader(typeof(bool), new BooleanTypeReader());
            //_commands.AddTypeReader(typeof(int), new IntegerTypeReader());
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
            _client.Log += Log;
            var token = Environment.GetEnvironmentVariable("MOTHBOT_TOKEN",EnvironmentVariableTarget.User);

            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();

            _client.MessageUpdated += MessageUpdated;
            //_client.MessageDeleted += MessageDeleted;
            _client.ButtonExecuted += HandleButtonAsync;
            _client.SelectMenuExecuted += HandleSelectAsync;
            _client.ReactionAdded += ReactionAdded;
            _client.ReactionRemoved += ReactionRemoved;
            _client.GuildMemberUpdated += GuildMemberUpdated;
            _client.Ready += async () =>
            {
                Console.WriteLine("Bot is connected!");
                await _client.SetGameAsync("Creepy Castle");
#if !DEBUG
                var guild = _client.GetGuild(608912123317321738);
                var channel = (ISocketMessageChannel)guild.GetChannel(935420527051419688);
                await channel.SendMessageAsync("MothBot is now online.");
#endif
                Thread childThread = new Thread(() => CheckTimers());
                childThread.Start();
                return;
            };
            // Block this task until the program is closed.
            await Task.Delay(-1);
        }

        private async Task CheckTimers()
        {
            var timer = new PeriodicTimer(TimeSpan.FromSeconds(60));
            while (await timer.WaitForNextTickAsync())
            {
                //Console.WriteLine("Minute check");
                ulong thisTime = Convert.ToUInt64(((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds());
                foreach (var guildEntry in Info.guildsDict)
                {
                    var guild = _client.GetGuild(guildEntry.Key);
                    for (var i = 0; i < guildEntry.Value.Timers.Count; i++)
                    {
                        var timerCheck = guildEntry.Value.Timers[i];
                        if (timerCheck.Active)
                        {
                            continue;
                        }
                        int offset = (int)(timerCheck.TimeToFire - thisTime);
                        if (timerCheck.Paused)
                        {
                            if (offset + timerCheck.RemainingTime < -604800)
                                guildEntry.Value.Timers.RemoveAt(i);
                            continue;
                        }
                        //Console.WriteLine($"Time to fire: {timerCheck.TimeToFire}; This time: {thisTime}; Offset: {offset}");
                        if (offset < 60)
                        {
                            bool late = offset < -60;
                            if (offset < 0)
                                offset = 0;
                            guildEntry.Value.Timers[i].Active = true;
                            Thread childThread = new Thread(() => Func.MakeReminderAsync(timerCheck, guild, late, offset));
                            childThread.Start();
                        }
                    }
                }
            }
        }

        private async Task MessageUpdated(Cacheable<IMessage, ulong> before, SocketMessage after, ISocketMessageChannel channel)
        {
            // If the message was not in the cache, downloading it will result in getting a copy of `after`.
            var message = await before.GetOrDownloadAsync();
            Console.WriteLine($"{message} -> {after}");
        }

        private async Task HandleCommandAsync(SocketMessage messageParam)
        {
            // Don't process the command if it was a system message
            var message = messageParam as SocketUserMessage;
            if (message == null) return;
            if (message.Channel.Name[0]=='@'&&message.Author.Id!=943517355068256306)
            {
                var mGuild = _client.GetGuild(496015504516055042);
                var mChannel = (ISocketMessageChannel)mGuild.GetChannel(991008000573587568);
                var eb = new EmbedBuilder();
                eb.WithAuthor(message.Author);
                eb.WithDescription(message.Content);
                if (message.Attachments.Count > 0)
                {
                    Console.WriteLine($"Message with embed. {message.Attachments.First().Url}");
                    eb.WithImageUrl(message.Attachments.First().Url);
                }
                await mChannel.SendMessageAsync("", false, eb.Build());
            }

            // Create a number to track where the prefix ends and the command begins
            int argPos = 2;

            // Determine if the message is a command based on the prefix and make sure no bots trigger commands
            /*if (message.Author.Id != 943517355068256306&&message.Channel.Id== 608912123317321744)
            {
                long lastTime;
                try
                {
                    lastTime = Convert.ToInt64(File.ReadAllText("last date\\" + Convert.ToString(message.Author.Id) + ".txt"));
                }
                catch (FileNotFoundException)
                {
                    lastTime = message.Timestamp.ToUnixTimeSeconds();
                }
                if (Convert.ToInt64(message.Timestamp.ToUnixTimeSeconds()) - lastTime > 10800)
                {
                    Random rand = new Random(DateTime.Now.ToString().GetHashCode());
                    int rv = rand.Next(0, 100);
                    Console.WriteLine(message.Author.Id);
                    switch (message.Author.Id)
                    {
                        case 332263102165024770:
                            if (rv < 80)
                                await message.Channel.SendMessageAsync("https://media.discordapp.net/attachments/608912123317321744/939221990089310370/71b63b12-eaf1-4750-a2a1-88ab15d8e12e.gif");
                            else
                            {
                                await message.Channel.SendMessageAsync("https://cdn.discordapp.com/attachments/608912123317321744/939209355381850222/makesweet-pljcy8_1.gif");
                                await message.Channel.SendMessageAsync("curveball!");
                            }
                            break;
                        case 491998313399189504:
                            if (rv < 80)
                                await message.Channel.SendMessageAsync("https://media.discordapp.net/attachments/608912123317321744/939224269999706162/makesweet-8bteqr.gif");
                            else
                            {
                                await message.Channel.SendMessageAsync("https://cdn.discordapp.com/attachments/608912123317321744/939224270368800778/makesweet-i5wxgc.gif");
                                await message.Channel.SendMessageAsync("curveball!");
                            }
                            break;
                        case 709034180046225440:
                            await message.Channel.SendMessageAsync("https://cdn.discordapp.com/attachments/608912123317321744/939221623058366535/makesweet-ua3mpb.gif");
                            break;
                        case 929729853941489694:
                            await message.Channel.SendMessageAsync("https://cdn.discordapp.com/attachments/608912123317321744/944663034801029140/makesweet-g74806.gif");
                            break;
                        case 604572456899969034:
                            await message.Channel.SendMessageAsync("https://cdn.discordapp.com/attachments/608912123317321744/944663034457128970/4b8bbda9-ff0d-4b1c-a3a3-2150710abaa8.gif");
                            break; 

                    }
                }
                await File.WriteAllTextAsync("last date\\"+ Convert.ToString(message.Author.Id) + ".txt", Convert.ToString(message.Timestamp.ToUnixTimeSeconds()));  
            }*/
                
            if (!(message.HasStringPrefix("m!", ref argPos, comparisonType: StringComparison.OrdinalIgnoreCase) ||
                message.HasMentionPrefix(_client.CurrentUser, ref argPos)) ||
                message.Author.IsBot/*||message.Author.Id==946143212031078430*/)
                return;
            //Console.WriteLine(_client.GetGuild(496015504516055042).Name);
            if (Info.confirmations.ContainsKey(message.Author.Id) && (Info.confirmations[message.Author.Id].Purpose!="help"||message.Content.Trim()=="m!help"))
                ConfirmationResponses.cancelConfirmation(message.Author.Id, _client);

            // Create a WebSocket-based command context based on the message
            var context = new SocketCommandContext(_client, message);
            Func.GuildFailsafe(context.Guild.Id);
            Func.UserFailsafe(context.User.Id);
            //Console.WriteLine($"{context.Message}");
            // Execute the command with the command context we just
            // created, along with the service provider for precondition checks.
            var result = await _commands.ExecuteAsync(
                context: context,
                argPos: argPos,
                services: null);
            if (!result.IsSuccess)
                Console.WriteLine(result.ErrorReason);
            if (result.Error.Equals(CommandError.Exception))
                await message.Channel.SendMessageAsync(result.ErrorReason);
            else if (result.Error.Equals(CommandError.Unsuccessful))
                await message.Channel.SendMessageAsync(result.ErrorReason);
            else if (result.Error.Equals(CommandError.ObjectNotFound))
                await message.Channel.SendMessageAsync(result.ErrorReason);
        }
        public async Task HandleButtonAsync(SocketMessageComponent component)
        {
            ulong messageID = component.Message.Id;
            ulong userID = component.User.Id;
            var context = new SocketCommandContext(_client, component.Message);
            // We can now check for our custom id
            switch (component.Data.CustomId)
            {
                // Since we set our buttons custom id as 'custom-id', we can check for it like this:
                case "confirmation-cancel":
                    if (Info.confirmations.ContainsKey(userID) && Info.confirmations[userID].MessageID == messageID)
                        ConfirmationResponses.cancelConfirmation(userID, _client);
                    await component.DeferAsync();
                    break;
                case "confirmation-confirm":
                    if (Info.confirmations.ContainsKey(userID) && Info.confirmations[userID].MessageID == messageID)
                        ConfirmationResponses.confirmConfirmation(context, userID);
                    await component.DeferAsync();
                    break;
            }
        }
        public async Task HandleSelectAsync(SocketMessageComponent component)
        {
            ulong messageID = component.Message.Id;
            ulong userID = component.User.Id;
            string text = String.Join(" ",component.Data.Values);
            // We can now check for our custom id
            switch (component.Data.CustomId)
            {
                // Since we set our buttons custom id as 'custom-id', we can check for it like this:
                case "helpMenu":
                    if (Info.confirmations.ContainsKey(userID) && Info.confirmations[userID].MessageID == messageID)
                    {
                        Console.WriteLine(text);
                        await HelpHandler.ModuleHelpAsync(text, (SocketGuildUser)component.User, _commands, component.Message);
                        await component.DeferAsync();
                    }
                    break;
                default:
                    await component.Channel.SendMessageAsync($"This should never appear! Encountered case {component.Data.CustomId}\n\nPing Randy if you see this, may Moth be with you.");
                    break;
            }
        }
        public async Task ReactionAdded(Cacheable<IUserMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
        {
            //Console.WriteLine(reaction.Emote.Name);
            if (channel.Id == 937374835577864202)
                return;
            var emote = reaction.Emote;
            if (channel.Id == 804436650045603900&&emote.Name=="grublove")
                return;
            var guildID = ((IGuildChannel)channel.Value).Guild.Id;
            IUserMessage reactMessage;
            if (!message.HasValue)
            {
                var messageID = reaction.MessageId;
                reactMessage = (IUserMessage)await channel.Value.GetMessageAsync(messageID);
            }
            else
                reactMessage = message.Value;
            if (emote.Equals(new Emoji("\u274C"))
                && Info.guildsDict[guildID].MessageReactions.Any(x => x.Value.Id == reactMessage.Id)
                && Info.guildsDict[guildID].MessageReactions.First(x => x.Value.Id == reactMessage.Id).Value.Author==reaction.UserId)
            {
                await reactMessage.DeleteAsync();
                Info.guildsDict[guildID].MessageReactions.First(x => x.Value.Id == reactMessage.Id).Value.Disabled = true;
                return;
            }
            if (Info.guildsDict[guildID].MessageReactions.ContainsKey(reactMessage.Id)&& Info.guildsDict[guildID].MessageReactions[reactMessage.Id].Disabled)
                return;
            var serverEmote = Info.guildsDict[guildID].ConvertedEmoji();
            ISocketMessageChannel reactchannel;
            if (Info.guildsDict[guildID].MessageReactions.ContainsKey(reactMessage.Id))
                reactchannel = (ISocketMessageChannel)await ((IGuildChannel)channel.Value).Guild.GetChannelAsync(Info.guildsDict[guildID].MessageReactions[reactMessage.Id].Channel);
            else
                reactchannel = (ISocketMessageChannel)await((IGuildChannel)channel.Value).Guild.GetChannelAsync(Info.guildsDict[guildID].ReactionChannel);
            if (serverEmote == null || reactchannel == null)
                return;
            if (serverEmote.Contains(emote)&& reactMessage.Author.Id != _client.CurrentUser.Id&& reactMessage.Reactions[emote].ReactionCount > 3)
            {
                //Console.WriteLine("Found correct emoji!");
                if (Info.guildsDict[guildID].MessageReactions.ContainsKey(reactMessage.Id))
                {
                    var boardMessage = (IUserMessage)await reactchannel.GetMessageAsync(Info.guildsDict[guildID].MessageReactions[reactMessage.Id].Id);
                    var reactionsInMessage = new List<IEmote>();
                    var emojiList = reactMessage.Reactions.Where(x => serverEmote.Contains(x.Key) && x.Value.ReactionCount > 2);
                    var reCount = 0;
                    foreach (var s in emojiList)
                    {
                        reactionsInMessage.Add(s.Key);
                        reCount = Math.Max(s.Value.ReactionCount, reCount);
                    }
                    reactionsInMessage.OrderByDescending(x => reactMessage.Reactions[x].ReactionCount);
                    await boardMessage.ModifyAsync(x => x.Content = $"**{String.Join(" ", reactionsInMessage)} {reCount} | <#{channel.Id}>**");
                    if (!boardMessage.Reactions.ContainsKey(emote) || !boardMessage.Reactions[emote].IsMe)
                        await boardMessage.AddReactionAsync(emote);
                }
                else if (reactMessage.Reactions.ContainsKey(emote))
                {
                    //Console.WriteLine("bog");
                    var eb = embedMessage(reactMessage);
                    var reactionsInMessage = new List<IEmote>();
                    var emojiList = reactMessage.Reactions.Where(x => serverEmote.Contains(x.Key) && x.Value.ReactionCount > 2);
                    var reCount = 0;
                    foreach (var s in emojiList)
                    {
                        reactionsInMessage.Add(s.Key);
                        reCount = Math.Max(s.Value.ReactionCount, reCount);
                    }
                    reactionsInMessage.OrderByDescending(x => reactMessage.Reactions[x].ReactionCount);
                    var result = reactchannel.SendMessageAsync($"**{String.Join(" ", reactionsInMessage)} {reCount} | <#{channel.Id}>**", false, eb.Build());
                    result.Wait();
                    if (result.IsCompleted)
                    {
                        var messageEntry = new Message()
                        {
                            Channel = reactchannel.Id,
                            Id = result.Result.Id,
                            Disabled = false,
                            Author = reactMessage.Author.Id
                        };
                        Info.guildsDict[guildID].MessageReactions.Add(reactMessage.Id, messageEntry);
                        await result.Result.AddReactionAsync(emote);
                    }
                }
            }
        }
        public async Task ReactionRemoved(Cacheable<IUserMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
        {
            var emote = reaction.Emote;
            var guildID = ((IGuildChannel)channel.Value).Guild.Id;
            var serverEmote = Info.guildsDict[guildID].ConvertedEmoji();
            IUserMessage reactMessage;
            if (!message.HasValue)
            {
                var messageID = reaction.MessageId;
                reactMessage = (IUserMessage)await channel.Value.GetMessageAsync(messageID);
            }
            else
                reactMessage = message.Value;
            if (Info.guildsDict[guildID].MessageReactions.ContainsKey(reactMessage.Id) && Info.guildsDict[guildID].MessageReactions[reactMessage.Id].Disabled)
                return;
            ISocketMessageChannel reactchannel;
            if (Info.guildsDict[guildID].MessageReactions.ContainsKey(reactMessage.Id))
                reactchannel = (ISocketMessageChannel)await ((IGuildChannel)channel.Value).Guild.GetChannelAsync(Info.guildsDict[guildID].MessageReactions[reactMessage.Id].Channel);
            else
                reactchannel = (ISocketMessageChannel)await ((IGuildChannel)channel.Value).Guild.GetChannelAsync(Info.guildsDict[guildID].ReactionChannel);
            if (serverEmote == null || reactchannel == null)
                return;
            if (serverEmote.Contains(emote) && reactMessage.Author.Id != _client.CurrentUser.Id)
            {
                //Console.WriteLine("Found correct emoji!");
                if (Info.guildsDict[guildID].MessageReactions.ContainsKey(reactMessage.Id))
                {
                    var boardMessage = (IUserMessage)await reactchannel.GetMessageAsync(Info.guildsDict[guildID].MessageReactions[reactMessage.Id].Id);
                    if (!reactMessage.Reactions.Any(x => serverEmote.Contains(x.Key)&&x.Value.ReactionCount>2))
                    {
                        await boardMessage.DeleteAsync();
                        Info.guildsDict[guildID].MessageReactions.Remove(reactMessage.Id);
                    }
                    else
                    {
                        var reactionsInMessage = new List<IEmote>();
                        var emojiList = reactMessage.Reactions.Where(x => serverEmote.Contains(x.Key) && x.Value.ReactionCount > 2);
                        var reCount = 0;
                        foreach (var s in emojiList)
                        {
                            reactionsInMessage.Add(s.Key);
                            reCount = Math.Max(s.Value.ReactionCount, reCount);
                        }
                        reactionsInMessage.OrderByDescending(x => reactMessage.Reactions[x].ReactionCount);
                        await boardMessage.ModifyAsync(x => x.Content = $"**{String.Join(" ", reactionsInMessage)} {reCount} | <#{channel.Id}>**");
                        if (!reactMessage.Reactions.ContainsKey(emote))
                            await boardMessage.RemoveReactionAsync(emote, _client.CurrentUser);
                    }
                }
            }
        }
        public EmbedBuilder embedMessage(IUserMessage message)
        {
            var eb = new EmbedBuilder();
            eb.WithAuthor(message.Author);
            var content = message.Content;
            if (content.Length > 1000)
            {
                content = content.Substring(0, 1000);
                content += "...";
            }
            content += $"\n\n**[Click to jump to the message!]({message.GetJumpUrl()})**";
            eb.WithDescription(content);
            if (message.Attachments.Count > 0)
            {
                Console.WriteLine($"Message with embed. {message.Attachments.First().Url}");
                eb.WithImageUrl(message.Attachments.First().Url);
            }
            eb.WithCurrentTimestamp();
            eb.WithColor(238, 226, 160);
            eb.WithFooter($"Message ID: {message.Id}");
            return eb;
        }
        /*public async Task MessageDeleted(Cacheable<IMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel)
        {
            var guildID = ((IGuildChannel)channel.Value).Guild.Id;
            if (ClassSetups.guildsDict[guildID].MessageReactions.Any(x => x.Value.Id == message.Id))
                ClassSetups.guildsDict[guildID].MessageReactions.First(x => x.Value.Id == message.Id).Value.Disabled = true;
        }*/
        public async Task GuildMemberUpdated(Cacheable<SocketGuildUser, ulong> userCache, SocketGuildUser user)
        {
            if (user.Id == 134307305805185024 && user.Roles.Contains(user.Guild.GetRole(916764069233557544)))
                await user.RemoveRoleAsync(916764069233557544);
        }
    }
    public class LoggingService
    {
        public LoggingService(DiscordSocketClient client, CommandService command)
        {
            client.Log += LogAsync;
            command.Log += LogAsync;
        }
        private Task LogAsync(LogMessage message)
        {
            if (message.Exception is CommandException cmdException)
            {
                Console.WriteLine($"[Command/{message.Severity}] {cmdException.Command.Aliases.First()}"
                    + $" failed to execute in {cmdException.Context.Channel}.");
                Console.WriteLine(cmdException);
            }
            else
                Console.WriteLine($"[General/{message.Severity}] {message}");

            return Task.CompletedTask;
        }
    }
}
