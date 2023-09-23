using Discord;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;
using Reddit.Controllers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MothBot
{
    public class Func
    {
        private enum states { NOT_IN_QUOTES, IN_QUOTES, ESCAPE_CHAR }
        public static string ConvertEmojis(string str)
        {
            foreach (var x in Info.emojisDict)
            {
                str = Regex.Replace(str,$@"(?i)(?<!\\)(?(?<=<):{x.Key}:(?!\d*>)|:{x.Key}:)", $"<$&{x.Value}>");
            }
            return str; 
        }
        public static SocketRole? getRole(string s, SocketGuild g)
        {
            if(s.StartsWith("<@&")&&s.EndsWith(">"))
                s = s.Substring(3, s.Length - 4);
            ulong roleID;
            try
            {
                roleID = Convert.ToUInt64(s);
            }
            catch (FormatException)
            {
                return null;
            }
            return g.GetRole(roleID);
        }
        public static string convertSeconds(ulong secs)
        {
            ulong days = secs / 86400;
            ulong hours = (secs / 3600) % 24;
            ulong mins = (secs / 60) % 60;
            secs = secs % 60;
            string res = "";
            ulong lcount = Math.Min(1, hours) + Math.Min(1, mins) + Math.Min(1, secs);
            ulong fcount = Math.Min(1, hours) + Math.Min(1, mins) + Math.Min(1, days);
            if (days > 0)
            {
                res += $"{days} day";
                if (days != 1)
                    res += "s";
                if (lcount > 1)
                    res += ", ";
                else if (lcount == 1)
                    res += " and ";
            }
            if (hours > 0)
            {
                res += $"{hours} hour";
                if (hours != 1)
                    res += "s";
                if (mins > 0 && secs > 0)
                    res += ", ";
                else if (days > 0 &&(mins > 0 || secs > 0))
                    res += ", and ";
                else if (mins > 0 || secs > 0)
                    res += " and ";
            }
            if (mins > 0)
            {
                res += $"{mins} minute";
                if (mins != 1)
                    res += "s";
                if (fcount > 1&&secs>0)
                    res += ", and ";
                else if (secs>0)
                    res += " and ";
            }
            if (secs > 0)
            {
                res += $"{secs} second";
                if (secs != 1)
                    res += "s";
            }
            return res;
        }
        public static List<int>? readHexCode(string s)
        {
            if (s.StartsWith("#"))
                    s = s.Substring(1, s.Length - 1);
            var l = new List<int>();
            s = s.ToUpper();
            if (!Regex.IsMatch(s, "^[0-9A-F]{1,6}$"))
                return null;
            else
                for (int i = 0; i < 6; i += 2)
                {
                    var s1 = s.Substring(i, 2);
                    l.Add(Convert.ToInt32(s1, 16));
                }
            return l;
        }
        public static async void disableButtons(IUserMessage message)
        {
            var builder = new ComponentBuilder();

            var rows = ComponentBuilder.FromMessage(message).ActionRows;

            for (int i = 0; i < rows.Count; i++)
            {
                foreach (var component in rows[i].Components)
                {
                    switch (component)
                    {
                        case ButtonComponent button:
                            builder.WithButton(button.ToBuilder()
                                .WithDisabled(true), i);
                            break;
                        case SelectMenuComponent menu:
                            builder.WithSelectMenu(menu.ToBuilder()
                                .WithDisabled(true), i);
                            break;
                    }
                }
            }
            await message.ModifyAsync(x => x.Components = builder.Build());
        }
        public static void UserFailsafe(ulong userID)
        {
            if (!Info.usersDict.ContainsKey(userID))
                Info.usersDict.Add(userID, new User());
        }
        public static void GuildFailsafe(ulong guildID)
        {
            if (!Info.guildsDict.ContainsKey(guildID))
                Info.guildsDict.Add(guildID, new Guild());
        }
        public static int? HumanTimeToSeconds(string rawTime)
        {
            rawTime = Regex.Replace(rawTime, @"[^\w\s:]+", "");
            int seconds = 0;
            if (rawTime.Contains(':'))
            {
                var time = rawTime.Split(':',options: StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
                if (time.Length > 4)
                return null;
                List<int> times = new List<int>() { 1, 60, 3600, 86400 };
                for (int i = 0; i<time.Length; i++)
                {
                    seconds += Convert.ToInt32(time[time.Length-1-i]) * times[i];
                }
                return seconds;
            }
            var timeSplit = rawTime.Split(' ',options: StringSplitOptions.RemoveEmptyEntries);
            foreach (string s in timeSplit)
            {
                Console.WriteLine(s);
                int number = Convert.ToInt32(s.Remove(s.Length - 1));
                switch (s.Last())
                {
                    case 's':
                        seconds += number;
                        break;
                    case 'm':
                        seconds += number * 60;
                        break;
                    case 'h':
                        seconds += number * 3600;
                        break;
                    case 'd':
                        seconds += number * 86400;
                        break;
                    default:
                        return null;
                }
            }
            return seconds;
        }
        public static DateTimeOffset DiscordIDToTimestamp(ulong ID)
        {
            long time = (long)(ID >> 22) + 1420070400000;
            return DateTimeOffset.FromUnixTimeMilliseconds(time);
        }
        public static string TimestampToHumanFormat(DateTimeOffset time)
        {
            string desc = $"{time.TimeOfDay.ToString("hh")}:{time.TimeOfDay.ToString("mm")}:{time.TimeOfDay.ToString("ss")} of the {time.Day}";
            switch (time.Day)
            {
                case 1:
                case 21:
                case 31:
                    desc += "st";
                    break;
                case 2:
                case 22:
                    desc += "nd";
                    break;
                case 3:
                case 23:
                    desc += "rd";
                    break;
                default:
                    desc += "th";
                    break;
            }
            desc += $" of {time.ToString("MMMM", CultureInfo.InvariantCulture)}, {time.Year}";
            return desc;
        }
        public static List<string> parseQuotes(string str)
        {
            states state = states.NOT_IN_QUOTES;
            var result = new List<string>();
            string cur_string = "";
            foreach (char c in str)
            {
                switch (state)
                {
                    case states.NOT_IN_QUOTES:
                        switch (c)
                        {
                            case '\"':
                            case '“':
                            case '”':
                                state = states.IN_QUOTES;
                                result.Add(cur_string);
                                cur_string = "";
                                break;
                            case '\\':
                                state = states.ESCAPE_CHAR;
                                break;
                            default:
                                cur_string += c;
                                break;
                        }
                        break;
                    case states.IN_QUOTES:
                        switch (c)
                        {
                            case '\"':
                            case '“':
                            case '”':
                                state = states.NOT_IN_QUOTES;
                                result.Add(cur_string);
                                cur_string = "";
                                break;
                            case '\\':
                                state = states.ESCAPE_CHAR;
                                break;
                            default:
                                cur_string += c;
                                break;
                        }
                        break;
                    case states.ESCAPE_CHAR:
                        state = states.IN_QUOTES;
                        cur_string += c;
                        break;
                }
            }
            result.Add(cur_string);
            result.ForEach(s => s = s.Trim());
            result.RemoveAll(s => s.Trim() == "");          // DO NOT REMOVE .TRIM() OR IT BREAKS
            return result;
        }
        public static Emoji? getKeycapEmoji(int i)
        {
            switch (i)
            {
                case 1:
                    return new Emoji("1️⃣");
                case 2:
                    return new Emoji("2️⃣");
                case 3:
                    return new Emoji("3️⃣");
                case 4:
                    return new Emoji("4️⃣");
                case 5:
                    return new Emoji("5️⃣");
                case 6:
                    return new Emoji("6️⃣");
                case 7:
                    return new Emoji("7️⃣");
                case 8:
                    return new Emoji("8️⃣");
                case 9:
                    return new Emoji("9️⃣");
                case 10:
                    return new Emoji("🔟");
            }
            return null;
        }
        public static ulong GCD(ulong a, ulong b)
        {
            if (b == 0)
                return a;
            else
                return GCD(b, a % b);
        }
        public static ulong LCM(ulong a, ulong b)
        {
            return a * b / GCD(a, b);
        }
        public static async void MakeReminderAsync(Timer timer, SocketGuild guild, bool late = false, int delay = 0)
        {
            if (delay < 0)
                delay = 0;
            var channel = (ISocketMessageChannel)guild.GetChannel(timer.Channel);
            var timeToFire = timer.TimeToFire;
            var msg = $"<@{timer.User}>: here is your reminder.";
            if (late)
            {
                var currentTime = Convert.ToUInt64(((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds());
                msg += $" Due to the bot being offline, the end of the timer has been delayed by {Func.convertSeconds(currentTime-timer.TimeToFire)}.";
            }
            Thread.Sleep(delay * 1000);
            if (!Info.guildsDict[guild.Id].Timers.Any(tt => tt.Name.Equals(timer.Name, StringComparison.OrdinalIgnoreCase) && !tt.Paused && tt.TimeToFire == timeToFire))
                return;
            var index = Info.guildsDict[guild.Id].Timers.FindIndex(tt => tt.Name.Equals(timer.Name, StringComparison.OrdinalIgnoreCase));
            Info.guildsDict[guild.Id].Timers.RemoveAt(index);
            var eb = new EmbedBuilder();
            eb.WithDescription(timer.Text);
            eb.WithColor(51, 127, 213);
            await channel.SendMessageAsync(msg,false,eb.Build());
            return;
        }
    }
    public class HelpHandler : ModuleBase<SocketCommandContext>
    {
        private readonly CommandService _service;

        public HelpHandler(CommandService service)
        {
            _service = service;
        }

        [Command("help")]
        [Alias("commands", "h")]
        [Summary("Information on how to use a command.\n\nUsage: `m!help [Command name]`")]
        public async Task HelpAsync([Remainder][Summary("The name of the command")] string command = "")
        {
            if (command != "")
                await CommandHelpAsync(command);
            else
            {
                var builder = new EmbedBuilder()
                {
                    Color = new Color(155, 89, 182),
                };
                string? description = null;
                foreach (var cmd in _service.Modules.First(x => x.Name == "General commands").Commands)
                {
                    description += $"**{cmd.Aliases.First()}**\n{cmd.Summary.Split("\n")[0]}\n\n";
                }

                if (!string.IsNullOrWhiteSpace(description))
                {
                    builder.WithTitle("General commands");
                    builder.WithDescription(description);
                }
                var menuBuilder = new SelectMenuBuilder().WithPlaceholder("Select a command category").WithCustomId("helpMenu").WithMinValues(1).WithMaxValues(1);
                foreach (var module in _service.Modules)
                {
                    if (module.Name == "HelpHandler" || module.Name == "Developer commands" || (module.Name == "Admin" && !((SocketGuildUser)Context.User).GuildPermissions.ManageGuild))
                        continue;
                    if (module.Name == "General commands")
                        menuBuilder = menuBuilder.AddOption(module.Name, module.Name, module.Summary, isDefault: true);
                    else
                        menuBuilder = menuBuilder.AddOption(module.Name, module.Name, module.Summary);
                }
                var comp = new ComponentBuilder();
                comp.WithSelectMenu(menuBuilder);
                var newMessage = await Context.Channel.SendMessageAsync("", false, builder.Build(), components: comp.Build());
                var newConfirmation = new Confirmation()
                {
                    MessageID = newMessage.Id,
                    ChannelID = Context.Channel.Id,
                    GuildID = Context.Guild.Id,
                    Purpose = "help"
                };
                Info.confirmations.Add(Context.User.Id, newConfirmation);
                var childref = new ThreadStart(HelpDisablingSetup);
                Thread childThread = new Thread(childref);
                childThread.Start();
            }
        }
        public static async Task ModuleHelpAsync(string moduleName, SocketGuildUser user, CommandService _service, IUserMessage message)
        {
            Console.WriteLine($"test {moduleName}");
            var builder = new EmbedBuilder()
            {
                Color = new Color(155, 89, 182),
            };
            string? description = null;
            foreach (var cmd in _service.Modules.First(x => x.Name == moduleName).Commands)
            {
                description += $"**{cmd.Aliases.First()}**\n{cmd.Summary.Split("\n")[0]}\n\n";
            }

            if (!string.IsNullOrWhiteSpace(description))
            {
                builder.WithTitle(moduleName);
                builder.WithDescription(description);
            }
            var menuBuilder = new SelectMenuBuilder().WithPlaceholder("Select a command category").WithCustomId("helpMenu").WithMinValues(1).WithMaxValues(1);
            foreach (var module in _service.Modules)
            {
                if (module.Name == "HelpHandler" || module.Name == "DeveloperTest" || (module.Name == "Admin" && !user.GuildPermissions.ManageGuild))
                    continue;
                if (module.Name == moduleName)
                    menuBuilder = menuBuilder.AddOption(module.Name, module.Name, module.Summary, isDefault: true);
                else
                    menuBuilder = menuBuilder.AddOption(module.Name, module.Name, module.Summary);
            }
            var comp = new ComponentBuilder();
            comp.WithSelectMenu(menuBuilder);
            await message.ModifyAsync(x => { x.Embed = builder.Build(); x.Components = comp.Build(); }) ;
        }

        private void HelpDisablingSetup()
        {
            var messageID = Info.confirmations[Context.User.Id].MessageID;
            var message = (IUserMessage)Context.Channel.GetMessageAsync(messageID).Result;
            Thread.Sleep(30000);
            if (Info.confirmations.ContainsKey(Context.User.Id))
            {
                Info.confirmations.Remove(Context.User.Id);
                Func.disableButtons(message);
            }
        }
        public async Task CommandHelpAsync([Remainder][Summary("The name of the command")] string command)
        {
            var builder = new EmbedBuilder()
            {
                Color = new Color(155, 89, 182)
            };
            CommandInfo? ncmd = null;
            var commandLow = command.ToLower();
            foreach (var module in _service.Modules)
            {
                string? strali = null;
                foreach (var cmd in module.Commands)
                {
                    Console.WriteLine(cmd.Name);
                    if (cmd.Aliases.Contains(commandLow))
                    {
                        var result = await cmd.CheckPreconditionsAsync(Context);
                        if (result.IsSuccess)
                        {
                            ncmd = cmd;
                            if (cmd.Aliases.Count > 1)
                                strali = "Aliases: *" + cmd.Aliases.Skip(1).Aggregate((i, j) => i + ", " + j).ToString() + "*\n\n";
                            break;
                        }
                    }
                }
                if (ncmd != null)
                {
                    builder.AddField(x =>
                    {
                        x.Name = ncmd.Name;
                        x.Value = strali + ncmd.Summary;
                        x.IsInline = false;
                    });
                    break;
                }
            }
            if (ncmd == null)
            {
                var eb = new EmbedBuilder();
                eb.WithDescription($"No command named {command} found!");
                eb.WithColor(224, 33, 33);
                await Context.Channel.SendMessageAsync("", false, eb.Build());
            }
            else
                await ReplyAsync("", false, builder.Build());

        }
    }
    [Name("General commands")]
    [Summary("Commands that don't fit elsewhere")]
    public class Commands : ModuleBase<SocketCommandContext>
    {
        [Command("moths")]
        [Summary("Sends a moth picture.\n\nUsage: `m!moths`")]
        [Alias("moth", "oths", "oth")]
        public async Task MothsAsync([Remainder()] string s = "")
        {
            ulong thisTime = Convert.ToUInt64(Context.Message.Timestamp.ToUnixTimeSeconds());
            if (thisTime - Info.lastReddit >= 21600)
            {
                var childref = new ThreadStart(MothUpdate);
                Thread childThread = new Thread(childref);
                childThread.Start();
                return;
            }
            Random rand = new Random(DateTime.Now.ToString().GetHashCode());
            int bound = Info.posts.Count;
            if (bound > 20)
                bound = 20;
            int index = rand.Next(0, bound);
            var post = (LinkPost)Info.posts[index];
            var eb = new EmbedBuilder();
            eb.WithImageUrl(post.URL);
            eb.WithDescription($"[{post.Title}](https://np.reddit.com{post.Permalink})");
            await Context.Channel.SendMessageAsync("", false, eb.Build());
        }
        private async void MothUpdate()
        {
            var eb = new EmbedBuilder();
            eb.WithDescription(Localisation.GetLoc("MothUpdate", Info.GetUser(Context.User.Id).Language));
            var message = await Context.Channel.SendMessageAsync("", false, eb.Build());
            Info.redditUpdate();
            Info.lastReddit = Convert.ToUInt64(Context.Message.Timestamp.ToUnixTimeSeconds());
            Random rand = new Random(DateTime.Now.ToString().GetHashCode());
            int bound = Info.posts.Count;
            if (bound > 20)
                bound = 20;
            int index = rand.Next(0, bound);
            var post = (LinkPost)Info.posts[index];
            eb.WithImageUrl(post.URL);
            eb.WithDescription($"[{post.Title}](https://np.reddit.com{post.Permalink})");
            await message.ModifyAsync(msg => msg.Embed = eb.Build());
        }
        [Command("say")]
        [Summary("Echoes a message.\n\nUsage: `m!say [Channel to echo the text to] <The text to echo>`")]
        public async Task SayAsync([Summary("Channel")] string chanName = "", [Remainder][Summary("The text to echo")] string echo = "")
        {
            var eb = new EmbedBuilder();
            if (chanName == "")
            {
                eb.WithDescription(Localisation.GetLoc("SayEmpty", Info.GetUser(Context.User.Id).Language));
                eb.WithColor(224, 33, 33);
                await Context.Channel.SendMessageAsync("", false, eb.Build());
                return;
            }
            if (chanName.StartsWith("<#") && chanName.EndsWith(">"))
                chanName = chanName.Substring(2, chanName.Length - 3);
            ISocketMessageChannel chan = Context.Channel;
            try
            {
                chan = (ISocketMessageChannel)Context.Guild.GetChannel(Convert.ToUInt64(chanName));
                if (chan == null)
                {
                    chan = (ISocketMessageChannel)Context.Client.GetGuild(608912123317321738).GetChannel(Convert.ToUInt64(chanName));
                    if (chan == null)
                    {
                        chan = (ISocketMessageChannel)Context.Client.GetGuild(798817032367505419).GetChannel(Convert.ToUInt64(chanName));
                        if (chan == null)
                        {
                            echo = chanName + " " + echo;
                            chan = Context.Channel;
                        }
                    }
                }
            }
            catch (FormatException)
            {
                echo = chanName + " " + echo;
                chan = Context.Channel;
            }
            if (echo != "")
            {
                var user = (IGuildUser)Context.User;
                if (user.GetPermissions((IGuildChannel)chan).SendMessages)
                {
                    echo = Func.ConvertEmojis(echo);
                    await chan.SendMessageAsync(echo, allowedMentions: AllowedMentions.None);
                    return;
                }
                eb.WithDescription(Localisation.GetLoc("SayNoAccess", Info.GetUser(Context.User.Id).Language));
                eb.WithColor(224, 33, 33);
                await Context.Channel.SendMessageAsync("", false, eb.Build());
                return;
            }
            eb.WithDescription(Localisation.GetLoc("SayNoText", Info.GetUser(Context.User.Id).Language));
            eb.WithColor(224, 33, 33);
            await Context.Channel.SendMessageAsync("", false, eb.Build());
        }
        [Command("react")]
        [Summary("Reacts to a message.\n\nUsage: `m!react <message link> <emoji>`")]
        public async Task ReactAsync([Summary("Channel")] string messageLink = "", [Remainder][Summary("The emoji to react with")] string emojiName = "")
        {
            var eb = new EmbedBuilder();
            if (messageLink == "" || emojiName == "")
            {
                eb.WithDescription(Localisation.GetLoc("ReactEmpty", Info.GetUser(Context.User.Id).Language));
                eb.WithColor(224, 33, 33);
                await Context.Channel.SendMessageAsync("", false, eb.Build());
                return;
            }
            if (!Regex.IsMatch(messageLink, @"\Ahttps://discord.com/channels/\d*/\d*/\d*\Z"))
            {
                eb.WithDescription(Localisation.GetLoc("InvalidMessageLink", Info.GetUser(Context.User.Id).Language));
                eb.WithColor(224, 33, 33);
                await Context.Channel.SendMessageAsync("", false, eb.Build());
                return;
            }
            emojiName = emojiName.Trim();
            emojiName = Func.ConvertEmojis(emojiName);
            Console.WriteLine(emojiName);
            Emote? emoji;
            try
            {
                emoji = Emote.Parse(emojiName);
            }
            catch (ArgumentException)
            {
                emoji = null;
            }
            if (emoji == null)
            {
                eb.WithDescription(Localisation.GetLoc("NoEmoji", Info.GetUser(Context.User.Id).Language));
                eb.WithColor(224, 33, 33);
                await Context.Channel.SendMessageAsync("", false, eb.Build());
                return;
            }
            var messageArray = messageLink.Split("/", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (Convert.ToUInt64(messageArray[3]) != Context.Guild.Id)
            {
                eb.WithDescription(Localisation.GetLoc("NoServer", Info.GetUser(Context.User.Id).Language));
                eb.WithColor(224, 33, 33);
                await Context.Channel.SendMessageAsync("", false, eb.Build());
                return;
            }
            ISocketMessageChannel chan = Context.Channel;
            chan = (ISocketMessageChannel)Context.Guild.GetChannel(Convert.ToUInt64(messageArray[4]));
            if (chan == null)
            {
                eb.WithDescription(Localisation.GetLoc("NoChannel", Info.GetUser(Context.User.Id).Language));
                eb.WithColor(224, 33, 33);
                await Context.Channel.SendMessageAsync("", false, eb.Build());
                return;
            }
            var message = await chan.GetMessageAsync(Convert.ToUInt64(messageArray[5]));
            if (message == null)
            {
                eb.WithDescription(Localisation.GetLoc("NoMessage", Info.GetUser(Context.User.Id).Language));
                eb.WithColor(224, 33, 33);
                await Context.Channel.SendMessageAsync("", false, eb.Build());
                return;
            }
            var result = message.AddReactionAsync(emoji);
            result.Wait();
            if (!result.IsCompleted)
            {
                eb.WithDescription(Localisation.GetLoc("FailReact", Info.GetUser(Context.User.Id).Language));
                eb.WithColor(224, 33, 33);
                await Context.Channel.SendMessageAsync("", false, eb.Build());
                return;
            }
            eb.WithDescription(Localisation.GetLoc("SuccessReact", Info.GetUser(Context.User.Id).Language));
            eb.WithColor(72, 139, 48);
            await Context.Channel.SendMessageAsync("", false, eb.Build());
            return;
        }
        [Command("howhot")]
        [Alias("hot", "howkortz", "kortz")]
        [Summary("Randomly rates how hot a user is.\n\nUsage: `m!howhot <User>`")]
        public async Task HotAsync([Remainder][Summary("The user")] SocketGuildUser? user = null)
        {
            //Console.WriteLine("Received hot command");
            SocketGuildUser userHot = user ?? (SocketGuildUser)Context.User;
            Random rand = new Random(DateTime.Now.ToString().GetHashCode());
            int rv = 0;
            if (userHot.Id == 491998313399189504)
                rv = rand.Next(-10, 11);
            else if (userHot.Id == 332263102165024770)
                rv = rand.Next(85, 106);
            else
                rv = rand.Next(0, 101);
            int rv2 = 100;
            if (userHot.Id != 491998313399189504)
                rv2 = rand.Next(0, 100);
            if (rv2 == 0)
                rv += 100;
            if (userHot.Id == 946143212031078430)
                rv *= -1;
            if (userHot.Id == 943517355068256306)
                rv = 100;
            var eb = new EmbedBuilder();
            eb.WithDescription(Localisation.GetLoc("HotMsg", Info.GetUser(Context.User.Id).Language, userHot, rv.ToString()));
            eb.WithColor(247, 71, 91);
            await Context.Channel.SendMessageAsync("", false, eb.Build());
        }
        [Command("howstupid")]
        [Alias("stupid", "howdumb", "dumb")]
        [Summary("Randomly rates how stupid a user is.\n\nUsage: `m!howstupid <User>`")]
        public async Task StupidAsync([Remainder][Summary("The user")] SocketGuildUser? user = null)
        {
            SocketGuildUser userHot = user ?? (SocketGuildUser)Context.User;
            Random rand = new Random(DateTime.Now.ToString().GetHashCode());
            int rv = 0;
            if (userHot.Id == 491998313399189504)
                rv = rand.Next(80, 106);
            else if (userHot.Id == 332263102165024770)
                rv = rand.Next(0, 40);
            else
                rv = rand.Next(0, 101);
            int rv2 = 100;
            if (userHot.Id != 332263102165024770)
                rv2 = rand.Next(0, 100);
            if (rv2 == 0)
                rv += 100;
            if (userHot.Id == 943517355068256306)
                rv = 0;
            var eb = new EmbedBuilder();
            eb.WithDescription(Localisation.GetLoc("StupidMsg", Info.GetUser(Context.User.Id).Language, userHot, rv.ToString()));
            eb.WithColor(247, 71, 91);
            await Context.Channel.SendMessageAsync("", false, eb.Build());
        }
        [Command("someone")]
        [Summary("Returns a random user.\n\nUsage: `m!someone`")]
        public async Task SomeoneAsync([Remainder()] string s = "")
        {
            var users = Context.Guild.Users;
            Random rand = new Random(DateTime.Now.ToString().GetHashCode());
            var i = rand.Next(0, users.Count);
            await Context.Channel.SendMessageAsync($"{users.ToArray()[i].Mention}", allowedMentions: AllowedMentions.None);
        }
        [Command("nickname")]
        [Summary("Changes the nickname of the bot.\n\nUsage: `m!nickname <bot name>`")]
        [Alias("nick")]
        public async Task NicknameAsync([Remainder][Summary("New nickname")] string nick = "")
        {
            var eb = new EmbedBuilder();
            nick = nick.Replace("­", "").Trim();
            if (nick == "")
            {
                eb.WithDescription(Localisation.GetLoc("NickEmpty", Info.GetUser(Context.User.Id).Language));
                eb.WithColor(224, 33, 33);
                await Context.Channel.SendMessageAsync("", false, eb.Build());
                return;
            }
            if (nick.Length > 32)
            {
                eb.WithDescription(Localisation.GetLoc("NickLong", Info.GetUser(Context.User.Id).Language));
                eb.WithColor(224, 33, 33);
                await Context.Channel.SendMessageAsync("", false, eb.Build());
                return;
            }
            var user = Context.Guild.GetUser(Context.Client.CurrentUser.Id);
            await user.ModifyAsync(x => { x.Nickname = nick; });
            eb.WithDescription(Localisation.GetLoc("NickSuccess", Info.GetUser(Context.User.Id).Language));
            eb.WithColor(72, 139, 48);
            await Context.Channel.SendMessageAsync("", embed: eb.Build());
        }
        [Command("colour")]
        [Summary("Changes the colour of a role you have been assigned to a hexcode or resets it to being colourless.\n\nUsage: `m!colour <role> FFFF00`, `m!colour <role> reset`, `m!colour #00FF00`")]
        [Alias("color")]
        public async Task ColourAsync([Summary("Role")] string roleString, [Summary("Colour")] string hexCode = "")
        {
            var eb = new EmbedBuilder();
            if (hexCode == "")
            {
                await ColourNoRoleAsync(roleString, Context);
                return;
            }
            if (hexCode == "reset")
                hexCode = "#000000";
            var role = Func.getRole(roleString, Context.Guild);
            if (role == null)
            {
                eb.WithDescription(Localisation.GetLoc("NoRole", Info.GetUser(Context.User.Id).Language));
                eb.WithColor(224, 33, 33);
                await Context.Channel.SendMessageAsync("", false, eb.Build());
                return;
            }
            var user = (SocketGuildUser)Context.User;
            if (!user.GuildPermissions.ManageGuild)
            {
                var roleList = Info.guildsDict[Context.Guild.Id].AssignedRoles[user.Id];
                if (!roleList.Contains(role.Id))
                {
                    eb.WithDescription(Localisation.GetLoc("RoleUnassigned", Info.GetUser(Context.User.Id).Language));
                    eb.WithColor(224, 33, 33);
                    await Context.Channel.SendMessageAsync("", false, eb.Build());
                    return;
                }
            }
            var colour = Func.readHexCode(hexCode);
            if (colour == null)
            {
                eb.WithDescription(Localisation.GetLoc("NoColour", Info.GetUser(Context.User.Id).Language));
                eb.WithColor(224, 33, 33);
                await Context.Channel.SendMessageAsync("", false, eb.Build());
                return;
            }
            var c = new Color(colour[0], colour[1], colour[2]);
            await role.ModifyAsync(x => { x.Color = c; });
            eb.WithDescription(Localisation.GetLoc("RoleColourChange", Info.GetUser(Context.User.Id).Language, Context1: role.Id.ToString()));
            if (c == new Color(0, 0, 0))
                eb.WithDescription($"The colour of <@&{role.Id}> has been reset!");
            else
                eb.WithColor(c);
            await Context.Channel.SendMessageAsync("", embed: eb.Build());
        }
        public async Task ColourNoRoleAsync(string hexCode, SocketCommandContext Context)
        {
            if (hexCode == "reset")
                hexCode = "#000000";
            var roleList = Info.guildsDict[Context.Guild.Id].AssignedRoles[Context.User.Id];
            var eb = new EmbedBuilder();
            if (roleList.Count == 0)
            {
                eb.WithDescription("You don't have any roles assigned!");
                eb.WithColor(224, 33, 33);
                await Context.Channel.SendMessageAsync("", false, eb.Build());
                return;
            }
            if (roleList.Count > 1)
            {
                eb.WithDescription("You have more than one role assigned, please specify a role.");
                eb.WithColor(224, 33, 33);
                await Context.Channel.SendMessageAsync("", false, eb.Build());
                return;
            }
            var role = Context.Guild.GetRole(roleList[0]);
            if (role == null)
            {
                eb.WithDescription("Please enter a valid role!");
                eb.WithColor(224, 33, 33);
                await Context.Channel.SendMessageAsync("", false, eb.Build());
                return;
            }
            var colour = Func.readHexCode(hexCode);
            if (colour == null)
            {
                eb.WithDescription("Please enter a valid hex code!");
                eb.WithColor(224, 33, 33);
                await Context.Channel.SendMessageAsync("", false, eb.Build());
                return;
            }
            var c = new Color(colour[0], colour[1], colour[2]);
            await role.ModifyAsync(x => { x.Color = c; });
            eb.WithDescription($"The colour of <@&{role.Id}> has been changed!");
            if (c == new Color(0, 0, 0))
                eb.WithDescription($"The colour of <@&{role.Id}> has been reset!");
            else
                eb.WithColor(c);
            await Context.Channel.SendMessageAsync("", embed: eb.Build());
        }
        [Command("pokeSpawn")]
        [Summary("Spawns a pokemon in the style of Poketwo that can't be caught.\n\nUsage: `m!pokespawn #channel <URL>`")]
        private async Task arceSpawnAsync(string chanName = "", string URL = "")
        {
            var eb = new EmbedBuilder();
            eb.WithColor(244, 178, 23);
            if (chanName == "" || URL == "")
            {
                eb.WithDescription("Please specify both the channel and the image URL with `m!pokespawn #channel <URL>`");
                await Context.Channel.SendMessageAsync("", embed: eb.Build());
                return;
            }
            ISocketMessageChannel chan = Context.Channel;
            if (chanName.StartsWith("<#") && chanName.EndsWith(">"))
                chanName = chanName.Substring(2, chanName.Length - 3);
            SocketGuild guild;
            if (Context.Guild.Id == 496015504516055042)
                guild = Context.Client.GetGuild(608912123317321738);
            else
                guild = Context.Guild;
            //Console.WriteLine(guild);
            try
            {
                chan = (ISocketMessageChannel)guild.GetChannel(Convert.ToUInt64(chanName));
            }
            catch (FormatException)
            {
                eb.WithDescription($"Failed to convert {chanName} to a channel ID.");
                await Context.Channel.SendMessageAsync("", embed: eb.Build());
                return;
            }
            if (chan == null)
            {
                eb.WithDescription($"Failed to find a channel with the ID of {chanName}.");
                await Context.Channel.SendMessageAsync("", embed: eb.Build());
                return;
            }
            eb.WithTitle("A wild pokémon has appeared!");
            eb.WithDescription("Guess the pokémon and type `p!catch <pokémon>` to catch it!");
            eb.WithColor(254, 154, 201);
            eb.WithImageUrl(URL);
            await chan.SendMessageAsync("", false, eb.Build());
        }
        [Command("date")]
        [Summary("Converts the Discord ID to a date.\n\nUsage: `m!date <ID>`")]
        private async Task dateAsync(ulong discordID = 0)
        {
            var eb = new EmbedBuilder();
            eb.WithColor(254, 154, 201);
            if (discordID == 0)
            {
                var currentTime = DateTimeOffset.UtcNow;
                eb.WithDescription($"The current time by the Coordinated Universal Time is {Func.TimestampToHumanFormat(currentTime)}");
                await Context.Channel.SendMessageAsync("", false, eb.Build());
                return;
            }
            var time = Func.DiscordIDToTimestamp(discordID);
            eb.WithDescription($"This discord ID originates from {Func.TimestampToHumanFormat(time)} by the Coordinated Universal Time");
            await Context.Channel.SendMessageAsync("", false, eb.Build());
        }
        [Command("poll")]
        [Summary("Creates a poll with the specified options, with up to 10 allowed, separated by quotes. \\\\\" allows escaping a quotation mark. \n\nUsage: `m!poll title \"option 1\" \"option 2\" <...>`")]
        private async Task pollAsync([Remainder] string pollOptionsRaw = "")
        {
            var pollOptions = Func.parseQuotes(Func.ConvertEmojis(pollOptionsRaw));
            /*Console.WriteLine(pollOptionsRaw);
            foreach (var x in pollOptions)
                Console.WriteLine(x);*/
            var eb = new EmbedBuilder();
            eb.WithColor(244, 178, 23);
            if (pollOptions.Count < 3 || pollOptions.Count > 11)
            {
                eb.WithDescription(Localisation.GetLoc("PollOptionsError", Info.GetUser(Context.User.Id).Language));
                await Context.Channel.SendMessageAsync("", embed: eb.Build());
                return;
            }
            ulong currentTime = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var user = (SocketGuildUser)Context.User;
            if (!user.GuildPermissions.ManageGuild)
            {
                if (currentTime - Info.guildsDict[Context.Guild.Id].LastPoll < 60)
                {
                    eb.WithDescription(Localisation.GetLoc("PollTimeout", Info.GetUser(Context.User.Id).Language));
                    await Context.Channel.SendMessageAsync("", embed: eb.Build());
                    return;
                }
                if (currentTime - Info.usersDict[user.Id].LastTimes.Poll < 18000)
                {
                    eb.WithDescription(Localisation.GetLoc("PollUserTimeout", Info.GetUser(Context.User.Id).Language));
                    await Context.Channel.SendMessageAsync("", embed: eb.Build());
                    return;
                }
            }
            string desc = "";
            for (int i = 1; i < pollOptions.Count; i++)
            {
                var emote = Func.getKeycapEmoji(i);
                desc += $"{emote} {pollOptions[i]}\n\n";
            }
            desc.TrimEnd();
            eb.WithColor(51, 127, 213);
            eb.WithTitle(pollOptions[0]);
            eb.WithDescription(desc);
            eb.WithFooter(Localisation.GetLoc("PollFooter", Info.GetUser(Context.User.Id).Language, user));
            Attachment? attach = null;
            if (Context.Message.Attachments.Count > 0)
            {
                attach = Context.Message.Attachments.First();
                eb.WithImageUrl(attach.Url);
            }
            var channel = (ISocketMessageChannel)Context.Guild.GetChannel(Info.guildsDict[Context.Guild.Id].PollChannel);
            var message = await channel.SendMessageAsync("", false, eb.Build());
            Thread childThread = new Thread(() => PollReaction(message, pollOptions.Count, Info.usersDict[Context.User.Id].Language));
            childThread.Start();
            Info.guildsDict[Context.Guild.Id].LastPoll = currentTime;
            Info.usersDict[Context.User.Id].LastTimes.Poll = currentTime;
            eb = new EmbedBuilder();
            eb.WithColor(51, 127, 213);
            eb.WithTitle(Localisation.GetLoc("PollSuccess", Info.GetUser(Context.User.Id).Language));
            await Context.Channel.SendMessageAsync("", false, eb.Build());
        }
        private async void PollReaction(RestUserMessage message, int reactCount, Language language)
        {
            var channel = (SocketTextChannel)message.Channel;
            for (int i = 1; i < reactCount; i++)
            {
                var emote = Func.getKeycapEmoji(i);
                await message.AddReactionAsync(emote);
            }
            var threadName = message.Embeds.First().Title;
            if (threadName.Length > 99)
                threadName = Regex.Match(threadName, @"^.{0,96}\w\b") + "...";
            if (Regex.IsMatch(threadName, @"^[\s­]*$"))
                threadName = "Thread";
            var thread = await channel.CreateThreadAsync(threadName, autoArchiveDuration: ThreadArchiveDuration.OneDay, message: message);
            await thread.SendMessageAsync(Localisation.GetLoc("PollMessage", language));
        }
 
        [Command("pollEmoji")]
        [Summary("Creates a poll with the specified options and emojis, with up to 10 allowed, separated by quotes. Each option must have an emoji preceding it. \\\\\" allows escaping a quotation mark. \n\nUsage: `m!pollEmoji \"title\" 1️⃣ \"option 1\" 2️⃣ \"option 2\" <...>`")]
        private async Task pollEmojiAsync([Remainder] string pollOptionsRaw = "")
        {
            var pollOptions = Func.parseQuotes(Func.ConvertEmojis(pollOptionsRaw));
            var eb = new EmbedBuilder();
            eb.WithColor(244, 178, 23);
            if (pollOptions.Count < 5 || pollOptions.Count > 21)
            {
                eb.WithDescription(Localisation.GetLoc("PollOptionsError", Info.GetUser(Context.User.Id).Language));
                await Context.Channel.SendMessageAsync("", embed: eb.Build());
                return;
            }
            if (pollOptions.Count % 2 != 1)
            {
                eb.WithDescription(Localisation.GetLoc("PollEmojiPerOption", Info.GetUser(Context.User.Id).Language));
                await Context.Channel.SendMessageAsync("", embed: eb.Build());
                return;
            }
            string title = pollOptions[0];
            List<Emoteji> emotejiList = new List<Emoteji>();
            List<String> truePollOptions = new List<String>();
            for (int i = 0; i < pollOptions.Count/2; i++)
            {
                Emoji? emoji = null;
                Emote? emote = null;
                if (Regex.IsMatch(pollOptions[2*i+1], @"<:\w+:\d+>"))
                    emote = Emote.Parse(Regex.Match(pollOptions[2 * i+1], @"<:\w+:\d+>").ToString());
                else if (Regex.IsMatch(pollOptions[2 * i+1], EmojiRegex)) 
                    emoji = Emoji.Parse(Regex.Match(pollOptions[2 * i+1], EmojiRegex).ToString());
                else
                {
                    eb.WithDescription(Localisation.GetLoc("FailFindEmoji", Info.GetUser(Context.User.Id).Language, Context1:pollOptions[2*i]));
                    await Context.Channel.SendMessageAsync("", embed: eb.Build());
                    return;
                }
                emotejiList.Add(new Emoteji { Emoji = emoji, Emote = emote });
                truePollOptions.Add(pollOptions[2 * i + 2]);
            }
            ulong currentTime = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var user = (SocketGuildUser)Context.User;
            if (!user.GuildPermissions.ManageGuild)
            {
                if (currentTime - Info.guildsDict[Context.Guild.Id].LastPoll < 60)
                {
                    eb.WithDescription(Localisation.GetLoc("PollTimeout", Info.GetUser(Context.User.Id).Language));
                    await Context.Channel.SendMessageAsync("", embed: eb.Build());
                    return;
                }
                if (currentTime - Info.usersDict[user.Id].LastTimes.Poll < 18000)
                {
                    eb.WithDescription(Localisation.GetLoc("PollUserTimeout", Info.GetUser(Context.User.Id).Language));
                    await Context.Channel.SendMessageAsync("", embed: eb.Build());
                    return;
                }
            }
            string desc = "";
            for (int i = 0; i < truePollOptions.Count; i++)
            {
                var emoteji = emotejiList[i];
                if (emoteji.Emote == null)
                    desc += $"{emoteji.Emoji} {truePollOptions[i]}\n\n";
                else 
                    desc += $"{emoteji.Emote} {truePollOptions[i]}\n\n";
            }
            desc.TrimEnd();
            eb.WithColor(51, 127, 213);
            eb.WithTitle(pollOptions[0]);
            eb.WithDescription(desc);
            eb.WithFooter(Localisation.GetLoc("PollFooter", Info.GetUser(Context.User.Id).Language, user));
            Attachment? attach = null;
            if (Context.Message.Attachments.Count > 0)
            {
                attach = Context.Message.Attachments.First();
                eb.WithImageUrl(attach.Url);
            }
            var channel = (ISocketMessageChannel)Context.Guild.GetChannel(Info.guildsDict[Context.Guild.Id].PollChannel);
            var message = await channel.SendMessageAsync("", false, eb.Build());
            Thread childThread = new Thread(() => PollEmojiReaction(message, emotejiList, Info.usersDict[Context.User.Id].Language));
            childThread.Start();
            Info.guildsDict[Context.Guild.Id].LastPoll = currentTime;
            Info.usersDict[Context.User.Id].LastTimes.Poll = currentTime;
            eb = new EmbedBuilder();
            eb.WithColor(51, 127, 213);
            eb.WithTitle(Localisation.GetLoc("PollSuccess", Info.GetUser(Context.User.Id).Language));
            await Context.Channel.SendMessageAsync("", false, eb.Build());
        }
        private async void PollEmojiReaction(RestUserMessage message, List<Emoteji> reactList, Language language)
        {
            var channel = (SocketTextChannel)message.Channel;
            foreach (var react in reactList)
            {
                if (react.Emoji == null)
                    await message.AddReactionAsync(react.Emote);
                else
                    await message.AddReactionAsync(react.Emoji);
            }
            var threadName = message.Embeds.First().Title;
            if (threadName.Length > 99)
                threadName = Regex.Match(threadName, @"^.{0,96}\w\b") + "...";
            if (Regex.IsMatch(threadName, @"^[\s­]*$"))
                threadName = "Thread";
            var thread = await channel.CreateThreadAsync(threadName, autoArchiveDuration: ThreadArchiveDuration.OneDay, message: message);
            await thread.SendMessageAsync(Localisation.GetLoc("PollMessage", language));
        }
        private const string EmojiRegex = @"\uFE0F?\u20E3|©\uFE0F?|[®\u203C\u2049\u2122\u2139\u2194-\u2199\u21A9\u21AA]\uFE0F?|[\u231A\u231B]|[\u2328\u23CF]\uFE0F?|[\u23E9-\u23EC]|[\u23ED-\u23EF]\uFE0F?|\u23F0|[\u23F1\u23F2]\uFE0F?|\u23F3|[\u23F8-\u23FA\u24C2\u25AA\u25AB\u25B6\u25C0\u25FB\u25FC]\uFE0F?|[\u25FD\u25FE]|[\u2600-\u2604\u260E\u2611]\uFE0F?|[\u2614\u2615]|\u2618\uFE0F?|\u261D(?:\uD83C[\uDFFB-\uDFFF]|\uFE0F)?|[\u2620\u2622\u2623\u2626\u262A\u262E\u262F\u2638-\u263A\u2640\u2642]\uFE0F?|[\u2648-\u2653]|[\u265F\u2660\u2663\u2665\u2666\u2668\u267B\u267E]\uFE0F?|\u267F|\u2692\uFE0F?|\u2693|[\u2694-\u2697\u2699\u269B\u269C\u26A0]\uFE0F?|\u26A1|\u26A7\uFE0F?|[\u26AA\u26AB]|[\u26B0\u26B1]\uFE0F?|[\u26BD\u26BE\u26C4\u26C5]|\u26C8\uFE0F?|\u26CE|[\u26CF\u26D1\u26D3]\uFE0F?|\u26D4|\u26E9\uFE0F?|\u26EA|[\u26F0\u26F1]\uFE0F?|[\u26F2\u26F3]|\u26F4\uFE0F?|\u26F5|[\u26F7\u26F8]\uFE0F?|\u26F9(?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?|\uFE0F(?:\u200D[\u2640\u2642]\uFE0F?)?)?|[\u26FA\u26FD]|\u2702\uFE0F?|\u2705|[\u2708\u2709]\uFE0F?|[\u270A\u270B](?:\uD83C[\uDFFB-\uDFFF])?|[\u270C\u270D](?:\uD83C[\uDFFB-\uDFFF]|\uFE0F)?|\u270F\uFE0F?|[\u2712\u2714\u2716\u271D\u2721]\uFE0F?|\u2728|[\u2733\u2734\u2744\u2747]\uFE0F?|[\u274C\u274E\u2753-\u2755\u2757]|\u2763\uFE0F?|\u2764(?:\u200D(?:\uD83D\uDD25|\uD83E\uDE79)|\uFE0F(?:\u200D(?:\uD83D\uDD25|\uD83E\uDE79))?)?|[\u2795-\u2797]|\u27A1\uFE0F?|[\u27B0\u27BF]|[\u2934\u2935\u2B05-\u2B07]\uFE0F?|[\u2B1B\u2B1C\u2B50\u2B55]|[\u3030\u303D\u3297\u3299]\uFE0F?|\uD83C(?:[\uDC04\uDCCF]|[\uDD70\uDD71\uDD7E\uDD7F]\uFE0F?|[\uDD8E\uDD91-\uDD9A]|\uDDE6\uD83C[\uDDE8-\uDDEC\uDDEE\uDDF1\uDDF2\uDDF4\uDDF6-\uDDFA\uDDFC\uDDFD\uDDFF]|\uDDE7\uD83C[\uDDE6\uDDE7\uDDE9-\uDDEF\uDDF1-\uDDF4\uDDF6-\uDDF9\uDDFB\uDDFC\uDDFE\uDDFF]|\uDDE8\uD83C[\uDDE6\uDDE8\uDDE9\uDDEB-\uDDEE\uDDF0-\uDDF5\uDDF7\uDDFA-\uDDFF]|\uDDE9\uD83C[\uDDEA\uDDEC\uDDEF\uDDF0\uDDF2\uDDF4\uDDFF]|\uDDEA\uD83C[\uDDE6\uDDE8\uDDEA\uDDEC\uDDED\uDDF7-\uDDFA]|\uDDEB\uD83C[\uDDEE-\uDDF0\uDDF2\uDDF4\uDDF7]|\uDDEC\uD83C[\uDDE6\uDDE7\uDDE9-\uDDEE\uDDF1-\uDDF3\uDDF5-\uDDFA\uDDFC\uDDFE]|\uDDED\uD83C[\uDDF0\uDDF2\uDDF3\uDDF7\uDDF9\uDDFA]|\uDDEE\uD83C[\uDDE8-\uDDEA\uDDF1-\uDDF4\uDDF6-\uDDF9]|\uDDEF\uD83C[\uDDEA\uDDF2\uDDF4\uDDF5]|\uDDF0\uD83C[\uDDEA\uDDEC-\uDDEE\uDDF2\uDDF3\uDDF5\uDDF7\uDDFC\uDDFE\uDDFF]|\uDDF1\uD83C[\uDDE6-\uDDE8\uDDEE\uDDF0\uDDF7-\uDDFB\uDDFE]|\uDDF2\uD83C[\uDDE6\uDDE8-\uDDED\uDDF0-\uDDFF]|\uDDF3\uD83C[\uDDE6\uDDE8\uDDEA-\uDDEC\uDDEE\uDDF1\uDDF4\uDDF5\uDDF7\uDDFA\uDDFF]|\uDDF4\uD83C\uDDF2|\uDDF5\uD83C[\uDDE6\uDDEA-\uDDED\uDDF0-\uDDF3\uDDF7-\uDDF9\uDDFC\uDDFE]|\uDDF6\uD83C\uDDE6|\uDDF7\uD83C[\uDDEA\uDDF4\uDDF8\uDDFA\uDDFC]|\uDDF8\uD83C[\uDDE6-\uDDEA\uDDEC-\uDDF4\uDDF7-\uDDF9\uDDFB\uDDFD-\uDDFF]|\uDDF9\uD83C[\uDDE6\uDDE8\uDDE9\uDDEB-\uDDED\uDDEF-\uDDF4\uDDF7\uDDF9\uDDFB\uDDFC\uDDFF]|\uDDFA\uD83C[\uDDE6\uDDEC\uDDF2\uDDF3\uDDF8\uDDFE\uDDFF]|\uDDFB\uD83C[\uDDE6\uDDE8\uDDEA\uDDEC\uDDEE\uDDF3\uDDFA]|\uDDFC\uD83C[\uDDEB\uDDF8]|\uDDFD\uD83C\uDDF0|\uDDFE\uD83C[\uDDEA\uDDF9]|\uDDFF\uD83C[\uDDE6\uDDF2\uDDFC]|\uDE01|\uDE02\uFE0F?|[\uDE1A\uDE2F\uDE32-\uDE36]|\uDE37\uFE0F?|[\uDE38-\uDE3A\uDE50\uDE51\uDF00-\uDF20]|[\uDF21\uDF24-\uDF2C]\uFE0F?|[\uDF2D-\uDF35]|\uDF36\uFE0F?|[\uDF37-\uDF7C]|\uDF7D\uFE0F?|[\uDF7E-\uDF84]|\uDF85(?:\uD83C[\uDFFB-\uDFFF])?|[\uDF86-\uDF93]|[\uDF96\uDF97\uDF99-\uDF9B\uDF9E\uDF9F]\uFE0F?|[\uDFA0-\uDFC1]|\uDFC2(?:\uD83C[\uDFFB-\uDFFF])?|[\uDFC3\uDFC4](?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|[\uDFC5\uDFC6]|\uDFC7(?:\uD83C[\uDFFB-\uDFFF])?|[\uDFC8\uDFC9]|\uDFCA(?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|[\uDFCB\uDFCC](?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?|\uFE0F(?:\u200D[\u2640\u2642]\uFE0F?)?)?|[\uDFCD\uDFCE]\uFE0F?|[\uDFCF-\uDFD3]|[\uDFD4-\uDFDF]\uFE0F?|[\uDFE0-\uDFF0]|\uDFF3(?:\u200D(?:\u26A7\uFE0F?|\uD83C\uDF08)|\uFE0F(?:\u200D(?:\u26A7\uFE0F?|\uD83C\uDF08))?)?|\uDFF4(?:\u200D\u2620\uFE0F?|\uDB40\uDC67\uDB40\uDC62\uDB40(?:\uDC65\uDB40\uDC6E\uDB40\uDC67|\uDC73\uDB40\uDC63\uDB40\uDC74|\uDC77\uDB40\uDC6C\uDB40\uDC73)\uDB40\uDC7F)?|[\uDFF5\uDFF7]\uFE0F?|[\uDFF8-\uDFFF])|\uD83D(?:[\uDC00-\uDC07]|\uDC08(?:\u200D\u2B1B)?|[\uDC09-\uDC14]|\uDC15(?:\u200D\uD83E\uDDBA)?|[\uDC16-\uDC3A]|\uDC3B(?:\u200D\u2744\uFE0F?)?|[\uDC3C-\uDC3E]|\uDC3F\uFE0F?|\uDC40|\uDC41(?:\u200D\uD83D\uDDE8\uFE0F?|\uFE0F(?:\u200D\uD83D\uDDE8\uFE0F?)?)?|[\uDC42\uDC43](?:\uD83C[\uDFFB-\uDFFF])?|[\uDC44\uDC45]|[\uDC46-\uDC50](?:\uD83C[\uDFFB-\uDFFF])?|[\uDC51-\uDC65]|[\uDC66\uDC67](?:\uD83C[\uDFFB-\uDFFF])?|\uDC68(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:\uDC8B\u200D\uD83D)?\uDC68|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D(?:\uDC66(?:\u200D\uD83D\uDC66)?|\uDC67(?:\u200D\uD83D[\uDC66\uDC67])?|[\uDC68\uDC69]\u200D\uD83D(?:\uDC66(?:\u200D\uD83D\uDC66)?|\uDC67(?:\u200D\uD83D[\uDC66\uDC67])?)|[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92])|\uD83E[\uDDAF-\uDDB3\uDDBC\uDDBD])|\uD83C(?:\uDFFB(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:\uDC8B\u200D\uD83D)?\uDC68\uD83C[\uDFFB-\uDFFF]|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D\uDC68\uD83C[\uDFFC-\uDFFF]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFC(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:\uDC8B\u200D\uD83D)?\uDC68\uD83C[\uDFFB-\uDFFF]|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D\uDC68\uD83C[\uDFFB\uDFFD-\uDFFF]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFD(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:\uDC8B\u200D\uD83D)?\uDC68\uD83C[\uDFFB-\uDFFF]|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D\uDC68\uD83C[\uDFFB\uDFFC\uDFFE\uDFFF]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFE(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:\uDC8B\u200D\uD83D)?\uDC68\uD83C[\uDFFB-\uDFFF]|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D\uDC68\uD83C[\uDFFB-\uDFFD\uDFFF]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFF(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:\uDC8B\u200D\uD83D)?\uDC68\uD83C[\uDFFB-\uDFFF]|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D\uDC68\uD83C[\uDFFB-\uDFFE]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?))?|\uDC69(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:\uDC8B\u200D\uD83D)?[\uDC68\uDC69]|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D(?:\uDC66(?:\u200D\uD83D\uDC66)?|\uDC67(?:\u200D\uD83D[\uDC66\uDC67])?|\uDC69\u200D\uD83D(?:\uDC66(?:\u200D\uD83D\uDC66)?|\uDC67(?:\u200D\uD83D[\uDC66\uDC67])?)|[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92])|\uD83E[\uDDAF-\uDDB3\uDDBC\uDDBD])|\uD83C(?:\uDFFB(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:[\uDC68\uDC69]\uD83C[\uDFFB-\uDFFF]|\uDC8B\u200D\uD83D[\uDC68\uDC69]\uD83C[\uDFFB-\uDFFF])|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D[\uDC68\uDC69]\uD83C[\uDFFC-\uDFFF]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFC(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:[\uDC68\uDC69]\uD83C[\uDFFB-\uDFFF]|\uDC8B\u200D\uD83D[\uDC68\uDC69]\uD83C[\uDFFB-\uDFFF])|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D[\uDC68\uDC69]\uD83C[\uDFFB\uDFFD-\uDFFF]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFD(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:[\uDC68\uDC69]\uD83C[\uDFFB-\uDFFF]|\uDC8B\u200D\uD83D[\uDC68\uDC69]\uD83C[\uDFFB-\uDFFF])|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D[\uDC68\uDC69]\uD83C[\uDFFB\uDFFC\uDFFE\uDFFF]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFE(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:[\uDC68\uDC69]\uD83C[\uDFFB-\uDFFF]|\uDC8B\u200D\uD83D[\uDC68\uDC69]\uD83C[\uDFFB-\uDFFF])|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D[\uDC68\uDC69]\uD83C[\uDFFB-\uDFFD\uDFFF]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFF(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:[\uDC68\uDC69]\uD83C[\uDFFB-\uDFFF]|\uDC8B\u200D\uD83D[\uDC68\uDC69]\uD83C[\uDFFB-\uDFFF])|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D[\uDC68\uDC69]\uD83C[\uDFFB-\uDFFE]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?))?|\uDC6A|[\uDC6B-\uDC6D](?:\uD83C[\uDFFB-\uDFFF])?|\uDC6E(?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|\uDC6F(?:\u200D[\u2640\u2642]\uFE0F?)?|[\uDC70\uDC71](?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|\uDC72(?:\uD83C[\uDFFB-\uDFFF])?|\uDC73(?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|[\uDC74-\uDC76](?:\uD83C[\uDFFB-\uDFFF])?|\uDC77(?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|\uDC78(?:\uD83C[\uDFFB-\uDFFF])?|[\uDC79-\uDC7B]|\uDC7C(?:\uD83C[\uDFFB-\uDFFF])?|[\uDC7D-\uDC80]|[\uDC81\uDC82](?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|\uDC83(?:\uD83C[\uDFFB-\uDFFF])?|\uDC84|\uDC85(?:\uD83C[\uDFFB-\uDFFF])?|[\uDC86\uDC87](?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|[\uDC88-\uDC8E]|\uDC8F(?:\uD83C[\uDFFB-\uDFFF])?|\uDC90|\uDC91(?:\uD83C[\uDFFB-\uDFFF])?|[\uDC92-\uDCA9]|\uDCAA(?:\uD83C[\uDFFB-\uDFFF])?|[\uDCAB-\uDCFC]|\uDCFD\uFE0F?|[\uDCFF-\uDD3D]|[\uDD49\uDD4A]\uFE0F?|[\uDD4B-\uDD4E\uDD50-\uDD67]|[\uDD6F\uDD70\uDD73]\uFE0F?|\uDD74(?:\uD83C[\uDFFB-\uDFFF]|\uFE0F)?|\uDD75(?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?|\uFE0F(?:\u200D[\u2640\u2642]\uFE0F?)?)?|[\uDD76-\uDD79]\uFE0F?|\uDD7A(?:\uD83C[\uDFFB-\uDFFF])?|[\uDD87\uDD8A-\uDD8D]\uFE0F?|\uDD90(?:\uD83C[\uDFFB-\uDFFF]|\uFE0F)?|[\uDD95\uDD96](?:\uD83C[\uDFFB-\uDFFF])?|\uDDA4|[\uDDA5\uDDA8\uDDB1\uDDB2\uDDBC\uDDC2-\uDDC4\uDDD1-\uDDD3\uDDDC-\uDDDE\uDDE1\uDDE3\uDDE8\uDDEF\uDDF3\uDDFA]\uFE0F?|[\uDDFB-\uDE2D]|\uDE2E(?:\u200D\uD83D\uDCA8)?|[\uDE2F-\uDE34]|\uDE35(?:\u200D\uD83D\uDCAB)?|\uDE36(?:\u200D\uD83C\uDF2B\uFE0F?)?|[\uDE37-\uDE44]|[\uDE45-\uDE47](?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|[\uDE48-\uDE4A]|\uDE4B(?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|\uDE4C(?:\uD83C[\uDFFB-\uDFFF])?|[\uDE4D\uDE4E](?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|\uDE4F(?:\uD83C[\uDFFB-\uDFFF])?|[\uDE80-\uDEA2]|\uDEA3(?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|[\uDEA4-\uDEB3]|[\uDEB4-\uDEB6](?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|[\uDEB7-\uDEBF]|\uDEC0(?:\uD83C[\uDFFB-\uDFFF])?|[\uDEC1-\uDEC5]|\uDECB\uFE0F?|\uDECC(?:\uD83C[\uDFFB-\uDFFF])?|[\uDECD-\uDECF]\uFE0F?|[\uDED0-\uDED2\uDED5-\uDED7]|[\uDEE0-\uDEE5\uDEE9]\uFE0F?|[\uDEEB\uDEEC]|[\uDEF0\uDEF3]\uFE0F?|[\uDEF4-\uDEFC\uDFE0-\uDFEB])|\uD83E(?:\uDD0C(?:\uD83C[\uDFFB-\uDFFF])?|[\uDD0D\uDD0E]|\uDD0F(?:\uD83C[\uDFFB-\uDFFF])?|[\uDD10-\uDD17]|[\uDD18-\uDD1C](?:\uD83C[\uDFFB-\uDFFF])?|\uDD1D|[\uDD1E\uDD1F](?:\uD83C[\uDFFB-\uDFFF])?|[\uDD20-\uDD25]|\uDD26(?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|[\uDD27-\uDD2F]|[\uDD30-\uDD34](?:\uD83C[\uDFFB-\uDFFF])?|\uDD35(?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|\uDD36(?:\uD83C[\uDFFB-\uDFFF])?|[\uDD37-\uDD39](?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|\uDD3A|\uDD3C(?:\u200D[\u2640\u2642]\uFE0F?)?|[\uDD3D\uDD3E](?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|[\uDD3F-\uDD45\uDD47-\uDD76]|\uDD77(?:\uD83C[\uDFFB-\uDFFF])?|[\uDD78\uDD7A-\uDDB4]|[\uDDB5\uDDB6](?:\uD83C[\uDFFB-\uDFFF])?|\uDDB7|[\uDDB8\uDDB9](?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|\uDDBA|\uDDBB(?:\uD83C[\uDFFB-\uDFFF])?|[\uDDBC-\uDDCB]|[\uDDCD-\uDDCF](?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|\uDDD0|\uDDD1(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\uD83C[\uDF3E\uDF73\uDF7C\uDF84\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83E\uDDD1|[\uDDAF-\uDDB3\uDDBC\uDDBD]))|\uD83C(?:\uDFFB(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D(?:\uD83D\uDC8B\u200D)?\uD83E\uDDD1\uD83C[\uDFFC-\uDFFF]|\uD83C[\uDF3E\uDF73\uDF7C\uDF84\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83E\uDDD1\uD83C[\uDFFB-\uDFFF]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFC(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D(?:\uD83D\uDC8B\u200D)?\uD83E\uDDD1\uD83C[\uDFFB\uDFFD-\uDFFF]|\uD83C[\uDF3E\uDF73\uDF7C\uDF84\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83E\uDDD1\uD83C[\uDFFB-\uDFFF]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFD(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D(?:\uD83D\uDC8B\u200D)?\uD83E\uDDD1\uD83C[\uDFFB\uDFFC\uDFFE\uDFFF]|\uD83C[\uDF3E\uDF73\uDF7C\uDF84\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83E\uDDD1\uD83C[\uDFFB-\uDFFF]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFE(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D(?:\uD83D\uDC8B\u200D)?\uD83E\uDDD1\uD83C[\uDFFB-\uDFFD\uDFFF]|\uD83C[\uDF3E\uDF73\uDF7C\uDF84\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83E\uDDD1\uD83C[\uDFFB-\uDFFF]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFF(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D(?:\uD83D\uDC8B\u200D)?\uD83E\uDDD1\uD83C[\uDFFB-\uDFFE]|\uD83C[\uDF3E\uDF73\uDF7C\uDF84\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83E\uDDD1\uD83C[\uDFFB-\uDFFF]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?))?|[\uDDD2\uDDD3](?:\uD83C[\uDFFB-\uDFFF])?|\uDDD4(?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|\uDDD5(?:\uD83C[\uDFFB-\uDFFF])?|[\uDDD6-\uDDDD](?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|[\uDDDE\uDDDF](?:\u200D[\u2640\u2642]\uFE0F?)?|[\uDDE0-\uDDFF\uDE70-\uDE74\uDE78-\uDE7A\uDE80-\uDE86\uDE90-\uDEA8\uDEB0-\uDEB6\uDEC0-\uDEC2\uDED0-\uDED6])";

        [Command("randomselect")]
        [Summary("Selects a random option with uniform distribution, separated by quotes. \\\\\" allows escaping a quotation mark. \n\nUsage: `m!randomselect \"option 1\" \"option 2\" <...>`")]
        private async Task randomSelectAsync([Remainder] string optionsRaw = "")
        {
            var options = Func.parseQuotes(optionsRaw);
            if (options.Count < 2)
            {
                var eb = new EmbedBuilder();
                eb.WithColor(244, 178, 23);
                eb.WithDescription("Make sure that the random selection has at least 2 choices it can select");
                await Context.Channel.SendMessageAsync("", embed: eb.Build());
                return;
            }
            Random rand = new Random(DateTime.Now.ToString().GetHashCode());
            int option = rand.Next(0, options.Count);
            await Context.Channel.SendMessageAsync(options[option]);
        }
    }
    [Name("Developer commands")]
    [RequireOwner()]
    public class DeveloperTest : ModuleBase<SocketCommandContext>
    {
        [Command("ai_will_do")]
        [Summary("hi den")]
        private async Task ai_will_doAsync([Remainder()] string s = "")
        {
            var ai_list = new List<long>();
            foreach (var c in s.Split())
                ai_list.Add(Convert.ToInt64(Convert.ToDouble(c)*1000));
            var chance = ai_list[0];
            if (chance <= 0)
            {
                await Context.Channel.SendMessageAsync("This focus won't be picked.");
                return;
            }
            ai_list.Remove(chance);
            ai_list.RemoveAll(t => t <= 0);
            ai_list.Sort();
            var listGcd = (ulong)chance;
            foreach (var x in ai_list)
                listGcd = Func.GCD(listGcd, (ulong)x);
            chance /= (long)listGcd;
            for (int i = 0; i < ai_list.Count; i++)
                ai_list[i] /= (long)listGcd;
            Console.WriteLine($"Calculating chance of {chance}, with other focuses being {string.Join(' ',ai_list)}");
            ulong power = (ulong)ai_list.Count;
            if (power == 0)
            {
                await Context.Channel.SendMessageAsync("This focus will always be picked.");
                return;
            }
            ulong denominator = (ulong)ai_list.Aggregate((a, x) => a * x);
            ulong powTot = 1;
            for (ulong i = 1; i <= power; i++)
                powTot = Func.LCM(powTot, i+1);
            ulong newDem = denominator * powTot;
            ulong newDemFac = 1;
            ulong lowbound;
            ulong highbound = 0;
            double result = 0;
            ulong numerator = 0;
            for (int i = 0; i<ai_list.Count; i++)
            {
                lowbound = highbound;
                highbound = (ulong)Math.Min(ai_list[i],chance);
                Console.WriteLine($"Calculating integral from {lowbound} to {highbound}");
                result += (Math.Pow(highbound, power + 1) - Math.Pow(lowbound, power + 1)) / (denominator * (power + 1));
                numerator += newDemFac * powTot / (power + 1) * (ulong)(Math.Pow(highbound, power + 1) - Math.Pow(lowbound, power + 1));
                Console.WriteLine($"Result becomes {result}, numerator becomes {numerator}");
                if (highbound == (ulong)chance)
                    break;
                denominator /= highbound;
                newDemFac *= highbound;
                power--;
                if (i == ai_list.Count-1)
                {
                    Console.WriteLine($"Calculating integral from {highbound} to {chance}");
                    numerator += newDemFac * powTot / (power + 1) * (ulong)(Math.Pow(chance, power + 1) - Math.Pow(highbound, power + 1));
                    result += chance;
                    result -= highbound;
                    Console.WriteLine($"Result becomes {result}, numerator becomes {numerator}");
                }
            }
            result /= chance;
            newDem *= (ulong)chance;
            Console.WriteLine($"Before shortening, num - {numerator}, dem - {newDem}");
            ulong gcd = Func.GCD(newDem, numerator);
            newDem /= gcd;
            numerator /= gcd;
            await Context.Channel.SendMessageAsync($"The chance of the focus being picked is approximately {Math.Round(result * 100, 2)}% or exactly {numerator}/{newDem}.");
            Console.WriteLine($"The chance of the focus being picked is approximately {Math.Round(result * 100, 2)}% or exactly {numerator}/{newDem}.\n");
        }
        [Command("forceOverwrite")]
        [Summary(".")]
        private async Task overwriteAsync([Remainder()] string s = "")
        {
            var succrun = Info.writeDynamicInfo("info\\", 1);
            if (!succrun)
                await Context.Channel.SendMessageAsync("Files failed to overwrite.");
            else
                await Context.Channel.SendMessageAsync("Files overwritten.");
        }
        [Command("forceBackup")]
        [Summary(".")]
        private async Task backupAsync([Remainder()] string s = "")
        {
            var time = DateTime.Now;
            var folderName = $"info\\backup\\{time.Year}-{time.Month}-{time.Day}-{time.Hour}-{time.Minute}\\";
            Directory.CreateDirectory(folderName);
            Info.CreateBackup(folderName);
            await Context.Channel.SendMessageAsync("Files overwritten.");
        }
        [Command("DMessage")]
        [Summary(".")]
        private async Task dmessageAsync(ulong userID, [Remainder()] string message = " ")
        {
            SocketUser? user = null;
            var eb = new EmbedBuilder();
            if (message.Trim() == "")
            {
                eb.WithDescription("Message cannot have no text. If you want to sent an embed, you'll have to add text either way.");
                await ReplyAsync("", false, eb.Build());
                return;
            }
            foreach (SocketGuild guild in Context.Client.Guilds)
            {
                user = guild.GetUser(userID);
                if (user != null)
                    break;
            }
            if (user == null)
            {
                eb.WithDescription("Can't find user");
                await ReplyAsync("", false, eb.Build());
                return;
            }
            Attachment? attach = null;
            Task<IUserMessage> result;
            if (Context.Message.Attachments.Count > 0)
            {
                attach = Context.Message.Attachments.First();
                eb.WithImageUrl(attach.Url);
                result = user.SendMessageAsync(message, embed: eb.Build());
            }
            else
                result = user.SendMessageAsync(message);
            result.Wait();
            if (!result.IsCompleted)
            {
                eb.WithDescription("Can't find user");
                await ReplyAsync("", false, eb.Build());
                return;
            }
            eb.WithDescription($"Messaged user {user.Username}#{user.Discriminator}");
            await ReplyAsync("", false, eb.Build());
        }
        [Command("EnglishCheck")]
        [Summary(".")]
        private async Task englishAsync([Remainder()] string message = " ")
        {
            foreach (KeyValuePair<string, string> entry in Info.englishDict)
            {
                Console.WriteLine(entry.Key + "\n" + entry.Value);
            }
            ReplyAsync("Printed to console");
        }
    }
    [Name("Admin commands")]
    [Summary("Commands that require moderator permissions")]
    [RequireUserPermission(GuildPermission.ManageGuild, Group = "Permission")]
    public class Admin : ModuleBase<SocketCommandContext>
    {
        [Command("assignrole")]
        [Summary("Assigns a role to the user for the `m!colour` command.\n\nUsage: `m!assignrole <role> <user>`")]
        private async Task assignRoleAsync([Summary("")] string roleString, [Remainder] SocketGuildUser user)
        {
            SocketRole? role = Func.getRole(roleString, Context.Guild);
            var eb = new EmbedBuilder();
            if (role == null)
            {
                eb.WithColor(224, 33, 33);
                eb.WithDescription("Please specify a valid role!");
                await Context.Channel.SendMessageAsync("", embed: eb.Build());
                return;
            }
            try
            {
                var roleList = Info.guildsDict[Context.Guild.Id].AssignedRoles[user.Id];
                if (roleList.Contains(role.Id))
                {
                    eb.WithColor(224, 33, 33);
                    eb.WithDescription("The user is already assigned this role!");
                    await Context.Channel.SendMessageAsync("", embed: eb.Build());
                    return;
                }
                Info.guildsDict[Context.Guild.Id].AssignedRoles[user.Id].Add(role.Id);
            }
            catch (KeyNotFoundException)
            {
                Info.guildsDict[Context.Guild.Id].AssignedRoles.Add(user.Id, new List<ulong>() { role.Id });
            }
            eb.WithColor(72, 139, 48);
            eb.WithDescription($"The {role.Mention} role has been assigned to {user.Mention}");
            await Context.Channel.SendMessageAsync("", embed: eb.Build());
            return;
        }
        [Command("removerole")]
        [Summary("Removes a role from the user for the `m!colour` command.\n\nUsage: `m!removerole <role> <user>`")]
        private async Task removeRoleAsync([Summary("")] string roleString, [Remainder] SocketGuildUser user)
        {
            SocketRole? role = Func.getRole(roleString, Context.Guild);
            var eb = new EmbedBuilder();
            if (role == null)
            {
                eb.WithColor(224, 33, 33);
                eb.WithDescription("Please specify a valid role!");
                await Context.Channel.SendMessageAsync("", embed: eb.Build());
                return;
            }
            var roleList = Info.guildsDict[Context.Guild.Id].AssignedRoles[user.Id];
            if (!roleList.Contains(role.Id))
            {
                eb.WithColor(224, 33, 33);
                eb.WithDescription("The user doesn't contain this role!");
                await Context.Channel.SendMessageAsync("", embed: eb.Build());
                return;
            }
            eb.WithColor(72, 139, 48);
            eb.WithDescription($"The {role.Mention} role has been removed from {user.Mention}");
            await Context.Channel.SendMessageAsync("", embed: eb.Build());
            Info.guildsDict[Context.Guild.Id].AssignedRoles[user.Id].Remove(role.Id);
        }
        [Command("listroles")]
        [Summary("Lists roles the user has for the `m!colour` command.\n\nUsage: `m!removerole <user>`")]
        private async Task listRolesAsync([Remainder] SocketGuildUser user)
        {
            var eb = new EmbedBuilder();
            string s;
            try
            {
                var roleList = Info.guildsDict[Context.Guild.Id].AssignedRoles[user.Id];
                eb.WithColor(244, 178, 23);
                s = $"{user.Mention} has the following roles for `m!colour`:\n";
                if (roleList.Count == 0)
                    s = $"{user.Mention} has no roles for `m!colour`.";
                else
                    foreach (ulong i in roleList)
                        s += $"\n<@&{i}>";
            }
            catch (KeyNotFoundException)
            {
                s = $"{user.Mention} has no roles for `m!colour`.";
                Info.guildsDict[Context.Guild.Id].AssignedRoles.Add(user.Id,new List<ulong>());
            }
            eb.WithDescription(s);
            await Context.Channel.SendMessageAsync("", embed: eb.Build());
        }
        [Command("checktaxes")]
        [Summary("Checks how much in taxes the current server has stored.\n\nUsage: `m!taxes`")]
        private async Task checkTaxesAsync([Remainder] string s = "")
        {
            var eb = new EmbedBuilder();
            eb.WithColor(244, 178, 23);
            eb.WithDescription($"This server currently has {Info.guildsDict[Context.Guild.Id].Taxes} moths in the treasury collected from taxes.");
            await Context.Channel.SendMessageAsync("", embed: eb.Build());
        }
        [Command("reactionsettings")]
        [Summary("Set up settings for the starboard.\n\nUsage: `m!reactionsettings <channel> <emoji>`")]
        private async Task reactionSettingsAsync(string chanName, [Remainder] string emoji = "")
        {
            var eb = new EmbedBuilder();
            eb.WithColor(244, 178, 23);
            if (chanName.StartsWith("<#") && chanName.EndsWith(">"))
                chanName = chanName.Substring(2, chanName.Length - 3);
            ISocketMessageChannel chan = Context.Channel;
            try
            {
                chan = (ISocketMessageChannel)Context.Guild.GetChannel(Convert.ToUInt64(chanName));
            }
            catch (FormatException)
            {
                eb.WithDescription($"Failed to convert {chanName} to a channel ID.");
                await Context.Channel.SendMessageAsync("", embed: eb.Build());
                return;
            }
            if (chan == null)
            {
                eb.WithDescription($"Failed to find a channel with the ID of {chanName}.");
                await Context.Channel.SendMessageAsync("", embed: eb.Build());
                return;
            }
            var emojiList = emoji.Split(">",StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries);
            var newEmojis = new List<string>();
            foreach (var e in emojiList)
            {
                Console.WriteLine(e);
                var emote = Emote.Parse(e + ">");
                if (emote != null)
                    newEmojis.Add(e + ">");                    
            }
            if (newEmojis.Count==0)
            {
                eb.WithDescription($"Failed to find the emojis.");
                await Context.Channel.SendMessageAsync("", embed: eb.Build());
                return;
            }
            Info.guildsDict[Context.Guild.Id].ReactionChannel = chan.Id;
            Info.guildsDict[Context.Guild.Id].ReactionEmoji = newEmojis;
            eb.WithDescription($"Channel set to <#{chanName}>, emojis set to the following: {String.Join(", ",newEmojis)}.");
            await Context.Channel.SendMessageAsync("", embed: eb.Build());
        }
        [Command("slowmode")]
        [Summary("Sets up slowmode in the specified channel to be X seconds.\n\nUsage: `m!slowmode #channel 123`")]
        private async Task slowmodeAsync(string chanName = "", [Remainder] int seconds = 0)
        {
            var eb = new EmbedBuilder();
            if (chanName == "")
            {
                eb.WithDescription("Sets up slowmode in the specified channel to be X seconds.\n\nUsage: `m!slowmode #channel 123`");
                eb.WithColor(224, 33, 33);
                await Context.Channel.SendMessageAsync("", false, eb.Build());
                return;
            }
            if (chanName.StartsWith("<#") && chanName.EndsWith(">"))
                chanName = chanName.Substring(2, chanName.Length - 3);
            var chan = (ITextChannel)Context.Channel;
            try
            {
                chan = (ITextChannel)Context.Guild.GetChannel(Convert.ToUInt64(chanName));
                if (chan == null)
                {
                    seconds = int.Parse(chanName);
                    chan = (ITextChannel)Context.Channel;
                }
            }
            catch (FormatException)
            {
                seconds = int.Parse(chanName);
                chan = (ITextChannel)Context.Channel;
            }
            var attempt = chan.ModifyAsync(x => x.SlowModeInterval = seconds);
            attempt.Wait();
            if (!attempt.IsCompletedSuccessfully)
            {
                eb.WithDescription("Failed to change the slowmode. Check if it's possible to set the slowmode to this amount.");
                eb.WithColor(224, 33, 33);
                await Context.Channel.SendMessageAsync("", false, eb.Build());
                return;
            }
            eb.WithDescription($"{chan.Mention}'s slowmode interval in seconds has been set to {seconds}.");
            await Context.Channel.SendMessageAsync("", false, eb.Build());
        }
    }
    /*
    FileStream file1 = new FileStream("test.txt", FileMode.Open);
    StreamReader reader = new StreamReader(file1);
    int count;
    string longLine = "";
    while(!reader.EndOfStream)
    {
        string s = reader.ReadLine();
        if (s.Length>longLine.Length)
            longLine = s;
        count++;
    }
    reader.Close();
    Console.ReadLine();
     */
}
