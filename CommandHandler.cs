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
using System.Windows.Forms.Design;
using YamlDotNet.RepresentationModel;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;

namespace MothBot
{
    public class Func
    {
        private enum states { NOT_IN_QUOTES, IN_QUOTES, ESCAPE_CHAR }
        public static string ConvertEmojis(string str)
        {
            foreach (var x in ClassSetups.emojisDict)
            {
                str = str.Replace($":{x.Key}:", $"<:{x.Key}:{x.Value}>", comparisonType: StringComparison.OrdinalIgnoreCase);
                str = str.Replace($"\\<:{x.Key}:{x.Value}>", $"\\:{x.Key}:", comparisonType: StringComparison.OrdinalIgnoreCase);
                str = Regex.Replace(str,$"<<:{x.Key}:{x.Value}>([0-9]*)>", $"<:{x.Value}:$1>", RegexOptions.IgnoreCase);
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
            if (!ClassSetups.usersDict.ContainsKey(userID))
                ClassSetups.usersDict.Add(userID, new User());
        }
        public static void GuildFailsafe(ulong guildID)
        {
            if (!ClassSetups.guildsDict.ContainsKey(guildID))
                ClassSetups.guildsDict.Add(guildID, new Guild());
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
                ClassSetups.confirmations.Add(Context.User.Id, newConfirmation);
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
            var messageID = ClassSetups.confirmations[Context.User.Id].MessageID;
            var message = (IUserMessage)Context.Channel.GetMessageAsync(messageID).Result;
            Thread.Sleep(30000);
            if (ClassSetups.confirmations.ContainsKey(Context.User.Id))
            {
                ClassSetups.confirmations.Remove(Context.User.Id);
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
            if (thisTime - ClassSetups.lastReddit >= 21600)
            {
                var childref = new ThreadStart(MothUpdate);
                Thread childThread = new Thread(childref);
                childThread.Start();
                return;
            }
            Random rand = new Random(DateTime.Now.ToString().GetHashCode());
            int bound = ClassSetups.posts.Count;
            if (bound > 20)
                bound = 20;
            int index = rand.Next(0, bound);
            var post = (LinkPost)ClassSetups.posts[index];
            var eb = new EmbedBuilder();
            eb.WithImageUrl(post.URL);
            eb.WithDescription($"[{post.Title}](https://np.reddit.com{post.Permalink})");
            await Context.Channel.SendMessageAsync("", false, eb.Build());
        }
        private async void MothUpdate()
        {
            var eb = new EmbedBuilder();
            eb.WithDescription("Currently fetching moths, please await...");
            var message = await Context.Channel.SendMessageAsync("", false, eb.Build());
            ClassSetups.redditUpdate();
            ClassSetups.lastReddit = Convert.ToUInt64(Context.Message.Timestamp.ToUnixTimeSeconds());
            Random rand = new Random(DateTime.Now.ToString().GetHashCode());
            int bound = ClassSetups.posts.Count;
            if (bound > 20)
                bound = 20;
            int index = rand.Next(0, bound);
            var post = (LinkPost)ClassSetups.posts[index];
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
                eb.WithDescription("Please enter what to say with `m!say <text>`.");
                eb.WithColor(224, 33, 33);
                await Context.Channel.SendMessageAsync("", false, eb.Build());
            }
            else
            {
                if (chanName.StartsWith("<#") && chanName.EndsWith(">"))
                    chanName = chanName.Substring(2, chanName.Length - 3);
                ISocketMessageChannel chan = Context.Channel;
                try
                {
                    chan = (ISocketMessageChannel)Context.Guild.GetChannel(Convert.ToUInt64(chanName));
                    if (chan == null)
                    {
                        echo = chanName + " " + echo;
                        chan = Context.Channel;
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
                    }
                    else
                    {
                        eb.WithDescription("You can only use the bot to send messages in channels you can send messages in yourself.");
                        eb.WithColor(224, 33, 33);
                        await Context.Channel.SendMessageAsync("", false, eb.Build());
                    }
                }
                else
                {
                    eb.WithDescription("Please enter a message to send in the specified channel.");
                    eb.WithColor(224, 33, 33);
                    await Context.Channel.SendMessageAsync("", false, eb.Build());
                }
            }
        }
        [Command("react")]
        [Summary("Reacts to a message.\n\nUsage: `m!react <message link> <emoji>`")]
        public async Task ReactAsync([Summary("Channel")] string messageLink = "", [Remainder][Summary("The emoji to react with")] string emojiName = "")
        {
            var eb = new EmbedBuilder();
            if (messageLink == ""|| emojiName == "")
            {
                eb.WithDescription("Please specify the message and the reaction with `m!react <message link> <emoji>`.");
                eb.WithColor(224, 33, 33);
                await Context.Channel.SendMessageAsync("", false, eb.Build());
                return;
            }
            if (!Regex.IsMatch(messageLink, @"\Ahttps://discord.com/channels/\d*/\d*/\d*\Z"))
            {
                eb.WithDescription("Please enter a valid message link!");
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
                eb.WithDescription("Couldn't find the emoji!");
                eb.WithColor(224, 33, 33);
                await Context.Channel.SendMessageAsync("", false, eb.Build());
                return;
            }
            var messageArray = messageLink.Split("/",StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
            if (Convert.ToUInt64(messageArray[3])!=Context.Guild.Id)
            {
                eb.WithDescription("The message has to be in the same server!");
                eb.WithColor(224, 33, 33);
                await Context.Channel.SendMessageAsync("", false, eb.Build());
                return;
            }
            ISocketMessageChannel chan = Context.Channel;
            chan = (ISocketMessageChannel)Context.Guild.GetChannel(Convert.ToUInt64(messageArray[4]));
            if (chan == null)
            {
                eb.WithDescription("Couldn't find the channel!");
                eb.WithColor(224, 33, 33);
                await Context.Channel.SendMessageAsync("", false, eb.Build());
                return;
            }
            var message = await chan.GetMessageAsync(Convert.ToUInt64(messageArray[5]));
            if (message == null)
            {
                eb.WithDescription("Couldn't find the message!");
                eb.WithColor(224, 33, 33);
                await Context.Channel.SendMessageAsync("", false, eb.Build());
                return;
            }
            var result = message.AddReactionAsync(emoji);
            result.Wait();
            if (!result.IsCompleted)
            {
                eb.WithDescription("Failed to add the reaction!");
                eb.WithColor(224, 33, 33);
                await Context.Channel.SendMessageAsync("", false, eb.Build());
                return;
            }
            eb.WithDescription("Reaction added successfully.");
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
            SocketGuildUser userHot = user ?? (SocketGuildUser)Context.Message.Author;
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
            eb.WithDescription($"{userHot.Nickname ?? userHot.Username} is {rv}% hot.");
            eb.WithColor(247, 71, 91);
            await Context.Channel.SendMessageAsync("", false, eb.Build());
        }
        [Command("howstupid")]
        [Alias("stupid","howdumb","dumb")]
        [Summary("Randomly rates how stupid a user is.\n\nUsage: `m!howstupid <User>`")]
        public async Task StupidAsync([Remainder][Summary("The user")] SocketGuildUser? user = null)
        {
            SocketGuildUser userHot = user ?? (SocketGuildUser)Context.Message.Author;
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
            eb.WithDescription($"{userHot.Nickname ?? userHot.Username} is {rv}% stupid.");
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
            nick = nick.Replace("­","").Trim();
            if (nick == "")
            {
                eb.WithDescription("Please enter a nickname to change to with `m!nickname <bot name>`.");
                eb.WithColor(224, 33, 33);
                await Context.Channel.SendMessageAsync("", false, eb.Build());
                return;
            }
            if (nick.Length>32)
            {
                eb.WithDescription("This is too long to be a nickname!");
                eb.WithColor(224, 33, 33);
                await Context.Channel.SendMessageAsync("", false, eb.Build());
                return;
            }
            var user = Context.Guild.GetUser(Context.Client.CurrentUser.Id);
            await user.ModifyAsync(x => { x.Nickname = nick; });
            eb.WithDescription("Nickname change successful!");
            eb.WithColor(72, 139, 48);
            await Context.Channel.SendMessageAsync("",embed:eb.Build());
        }
        [Command("colour")]
        [Summary("Changes the colour of a role you have been assigned to a hexcode or resets it to being colourless.\n\nUsage: `m!colour <role> FFFF00`, `m!colour <role> reset`, `m!colour #00FF00`")]
        [Alias("color")]
        public async Task ColourAsync([Summary("Role")] string roleString, [Summary("Colour")] string hexCode = "")
        {
            var eb = new EmbedBuilder();
            if (hexCode=="")
            {
                await ColourNoRoleAsync(roleString, Context);
                return;
            }
            if (hexCode == "reset")
                hexCode = "#000000";
            var role = Func.getRole(roleString, Context.Guild);
            if (role == null)
            {
                eb.WithDescription("Please enter a valid role!");
                eb.WithColor(224, 33, 33);
                await Context.Channel.SendMessageAsync("", false, eb.Build());
                return;
            }
            var user = (SocketGuildUser)Context.User;
            if (!user.GuildPermissions.ManageGuild)
            {
                var roleList = ClassSetups.guildsDict[Context.Guild.Id].AssignedRoles[user.Id];
                if (!roleList.Contains(role.Id))
                {
                    eb.WithDescription("You are not assigned this role.");
                    eb.WithColor(224, 33, 33);
                    await Context.Channel.SendMessageAsync("", false, eb.Build());
                    return;
                }
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
            await role.ModifyAsync(x => { x.Color = c; } );
            eb.WithDescription($"The colour of <@&{role.Id}> has been changed!");
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
            var roleList = ClassSetups.guildsDict[Context.Guild.Id].AssignedRoles[Context.User.Id];
            var eb = new EmbedBuilder();
            if (roleList.Count==0)
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
            if (c == new Color(0,0,0))
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
        [Summary("Creates a poll with the specified options.\n\nUsage: `m!date <ID>")]
        private async Task pollAsync([Remainder] string pollOptionsRaw = "")
        {
            var pollOptions = Func.parseQuotes(pollOptionsRaw);
            Console.WriteLine(pollOptionsRaw);
            foreach (var x in pollOptions)
                Console.WriteLine(x);
            var eb = new EmbedBuilder();
            eb.WithColor(244, 178, 23);
            if (pollOptions.Count < 3||pollOptions.Count>11)
            {
                eb.WithDescription("Make sure that the poll includes a title and between 2 and 10 options, inclusionary.");
                await Context.Channel.SendMessageAsync("", embed: eb.Build());
                return;
            }
            ulong currentTime = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var user = (SocketGuildUser)Context.User;
            if (!user.GuildPermissions.ManageGuild)
            {
                if (currentTime - ClassSetups.guildsDict[Context.Guild.Id].LastPoll < 60)
                {
                    eb.WithDescription("A poll was already posted in the last minute. To prevent spam, this has been restricted.");
                    await Context.Channel.SendMessageAsync("", embed: eb.Build());
                    return;
                }
                if (currentTime - ClassSetups.usersDict[user.Id].LastTimes.Poll < 18000)
                {
                    eb.WithDescription("A poll was already made by you in the last 5 hours. To prevent spam, this has been restricted.");
                    await Context.Channel.SendMessageAsync("", embed: eb.Build());
                    return;
                }
            }
            string desc = "";
            for (int i = 1; i < pollOptions.Count; i++)
            {
                var emote = Func.getKeycapEmoji(i);
                desc += $"{emote} {Func.ConvertEmojis(pollOptions[i])}\n\n";
            }
            desc.TrimEnd();
            eb.WithColor(51, 127, 213);
            eb.WithTitle(Func.ConvertEmojis(pollOptions[0]));
            eb.WithDescription(desc);
            eb.WithFooter($"This poll was created by {Context.User.Username}#{Context.User.Discriminator}");
            var channel = (ISocketMessageChannel)Context.Guild.GetChannel(825980405239906374);
            var message = await channel.SendMessageAsync("", false, eb.Build());
            Thread childThread = new Thread(() => PollReaction(message, pollOptions.Count));
            childThread.Start();
            ClassSetups.guildsDict[Context.Guild.Id].LastPoll = currentTime;
            ClassSetups.usersDict[Context.User.Id].LastTimes.Poll = currentTime;
            eb = new EmbedBuilder();
            eb.WithColor(51, 127, 213);
            eb.WithTitle("The poll has been sent.");
            await Context.Channel.SendMessageAsync("", false, eb.Build());
        }
        private async void PollReaction(RestUserMessage message, int reactCount)
        {
            var channel = (SocketTextChannel)message.Channel;
            for (int i = 1; i < reactCount; i++)
            {
                var emote = Func.getKeycapEmoji(i);
                await message.AddReactionAsync(emote);
            }
            var thread = await channel.CreateThreadAsync(message.Embeds.First().Title, autoArchiveDuration: ThreadArchiveDuration.OneDay, message: message);
            await thread.SendMessageAsync("Discuss this poll in the thread, such as why you've picked an option. If the poll has an \"Other\" option, you can say what you've picked that doesn't fall in the options.");
        }
    }
    [Name("Developer commands")]
    [RequireOwner(ErrorMessage = "you can't do this unless you're Randi lol")]
    public class DeveloperTest : ModuleBase<SocketCommandContext>
    {
        [Command("ai_will_do")]
        [Summary("hi den")]
        private async Task ai_will_doAsync([Remainder()] string s = "")
        {
            var ai_list = new List<double>();
            foreach (var c in s.Split())
                ai_list.Add(Convert.ToDouble(c));
            var chance = ai_list[0];
            if (chance <= 0)
            {
                await Context.Channel.SendMessageAsync("This focus won't be picked.");
                return;
            }
            ai_list.Remove(chance);
            ai_list.RemoveAll(t => t <= 0);
            ai_list.Sort();
            Console.WriteLine($"Calculating chance of {chance}, with other focuses being {string.Join(' ',ai_list)}");
            int power = ai_list.Count;
            if (power == 0)
            {
                await Context.Channel.SendMessageAsync("This focus will always be picked.");
                return;
            }
            double denominator = ai_list.Aggregate((a, x) => a * x);
            double lowbound;
            double highbound = 0;
            double result = 0;
            for (int i = 0; i<ai_list.Count; i++)
            {
                lowbound = highbound;
                highbound = Math.Min(ai_list[i],chance);
                Console.WriteLine($"Calculating integral from {lowbound} to {highbound}");
                result += (Math.Pow(highbound, power + 1) - Math.Pow(lowbound, power + 1)) / (denominator * (power + 1));
                Console.WriteLine($"Result becomes {result}");
                if (highbound == chance)
                    break;
                denominator /= highbound;
                power--;
                if (i == ai_list.Count-1)
                {
                    Console.WriteLine($"Calculating integral from {highbound} to {chance}");
                    result += chance;
                    result -= highbound;
                    Console.WriteLine($"Result becomes {result}");
                }
            }
            result /= chance;
            await Context.Channel.SendMessageAsync($"The chance of the focus of being picked is approximately {Math.Round(result*100,2)}%.");
        }
        [Command("forceOverwrite")]
        [Summary("how did you learn of this?")]
        private async Task overwriteAsync([Remainder()] string s = "")
        {
            ClassSetups.writeDynamicInfo("info\\");
            await Context.Channel.SendMessageAsync("Files overwritten.");
        }
        [Command("forceBackup")]
        [Summary("how did you learn of this?")]
        private async Task backupAsync([Remainder()] string s = "")
        {
            var time = DateTime.Now;
            var folderName = $"info\\backup\\{time.Year}-{time.Month}-{time.Day}-{time.Hour}-{time.Minute}\\";
            Directory.CreateDirectory(folderName);
            ClassSetups.writeDynamicInfo(folderName);
            await Context.Channel.SendMessageAsync("Files overwritten.");
        }
        [Command("DMessage")]
        [Summary("how did you learn of this?")]
        private async Task dmessageAsynd(ulong userID, [Remainder()] string message = " ")
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
                var roleList = ClassSetups.guildsDict[Context.Guild.Id].AssignedRoles[user.Id];
                if (roleList.Contains(role.Id))
                {
                    eb.WithColor(224, 33, 33);
                    eb.WithDescription("The user is already assigned this role!");
                    await Context.Channel.SendMessageAsync("", embed: eb.Build());
                    return;
                }
                ClassSetups.guildsDict[Context.Guild.Id].AssignedRoles[user.Id].Add(role.Id);
            }
            catch (KeyNotFoundException)
            {
                ClassSetups.guildsDict[Context.Guild.Id].AssignedRoles.Add(user.Id, new List<ulong>() { role.Id });
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
            var roleList = ClassSetups.guildsDict[Context.Guild.Id].AssignedRoles[user.Id];
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
            ClassSetups.guildsDict[Context.Guild.Id].AssignedRoles[user.Id].Remove(role.Id);
        }
        [Command("listroles")]
        [Summary("Lists roles the user has for the `m!colour` command.\n\nUsage: `m!removerole <user>`")]
        private async Task listRolesAsync([Remainder] SocketGuildUser user)
        {
            var eb = new EmbedBuilder();
            string s;
            try
            {
                var roleList = ClassSetups.guildsDict[Context.Guild.Id].AssignedRoles[user.Id];
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
                ClassSetups.guildsDict[Context.Guild.Id].AssignedRoles.Add(user.Id,new List<ulong>());
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
            eb.WithDescription($"This server currently has {ClassSetups.guildsDict[Context.Guild.Id].Taxes} moths in the treasury collected from taxes.");
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
            ClassSetups.guildsDict[Context.Guild.Id].ReactionChannel = chan.Id;
            ClassSetups.guildsDict[Context.Guild.Id].ReactionEmoji = newEmojis;
            eb.WithDescription($"Channel set to <#{chanName}>, emojis set to the following: {String.Join(", ",newEmojis)}.");
            await Context.Channel.SendMessageAsync("", embed: eb.Build());
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
