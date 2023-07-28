using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Reddit;
using Reddit.Controllers;
using Reddit.Inputs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace MothBot
{
    public class Price
    {
        public ulong BasePrice { get; set; } = 0;
        public double PricePortion { get; set; } = 0;
        public double Tax { get; set; } = 0;
    }
    public class Item
    {
        public string Id { get; set; } = "ID ERROR: Ping Randi if you see this, may Moth be with you.";
        public string Name { get; set; } = "NAME ERROR: Ping Randi if you see this, may Moth be with you.";
        public Price Price { get; set; } = new Price();
        public string ShortDesc { get; set; } = "SHORTDESC ERROR: Ping Randi if you see this, may Moth be with you.";
        public string LongDesc { get; set; } = "LONGDESC ERROR: Ping Randi if you see this, may Moth be with you.";
        public ulong Cooldown { get; set; } = 0;
        public ulong Max { get; set; } = 0;
    }
    public class Role
    {
        public ulong Id { get; set; }
        public ulong Guild { get; set; }
    }
    public class Guild
    {
        public Dictionary<ulong, List<ulong>> AssignedRoles { get; set; } = new Dictionary<ulong, List<ulong>>();
        public ulong MuteRole { get; set; } = 0;
        public ulong Taxes { get; set; } = 0;
        public ulong ReactionChannel { get; set; } = 0;
        public ulong PollChannel { get; set; } = 0;
        public List<string> ReactionEmoji { get; set; } = new List<string>();
        public List<IEmote> ConvertedEmoji()
        {
            var result = new List<IEmote>();
            foreach (var s in ReactionEmoji)
                result.Add(Emote.Parse(s));
            return result;
        }
        public Dictionary<ulong, Message> MessageReactions { get; set; } = new Dictionary<ulong, Message>();
        public ulong LastPoll { get; set; } = 0;
        public List<Timer> Timers { get; set; } = new List<Timer>();
    }
    public class Timer
    {
        public string Name { get; set; } = string.Empty;
        public ulong Channel { get; set; } = 0;
        public ulong TimeToFire { get; set; } = 0;
        public string Text { get; set; } = string.Empty;
        public ulong User { get; set; } = 0;
        public bool Active { get; set; } = false;
        public bool Paused { get; set; } = false;
        public int RemainingTime { get; set; } = 0;
        public int OriginalDuration { get; set; } = 0;
    }
    public class User
    {
        public ulong MothAmount { get; set; } = 0;
        public LastTimes LastTimes { get; set; } = new LastTimes();

        public Dictionary<string, ulong> Items = new Dictionary<string, ulong>();
    }
    public class Confirmation
    {
        public ulong GuildID { get; set; }      //The guild the confirmation is in
        public ulong MessageID { get; set; }    //The message the confirmation was made in
        public ulong ChannelID { get; set; }    //The channel the confirmation was made in
        public string Purpose { get; set; } = "Default";
        public ulong ULongArgument1 { get; set; }
        public ulong ULongArgument2 { get; set; }
        public Item? ItemArgument1 { get; set; }
    }
    public class LastTimes
    {
        public ulong Daily { get; set; } = 0;
        public ulong Search { get; set; } = 0;
        public Dictionary<string, ulong> Item { get; set; } = new Dictionary<string, ulong>();
        public ulong Poll { get; set; } = 0;
    }
    public class StorePage
    {
        public string Name { get; set; } = "NAME ERROR: Ping Randy if you see this, may Moth be with you.";
        public string Desc { get; set; } = "DESC ERROR: Ping Randy if you see this, may Moth be with you.";
        public List<string> Items { get; set; } = new List<string>() { "ITEM ERROR: Ping Randy if you see this, may Moth be with you." };
    }
    public class Message
    {
        public ulong Id { get; set; } = 0;
        public ulong Channel { get; set; } = 0;
        public ulong Author { get; set; } = 0;
        public bool Disabled { get; set; } = false;
    }
    public class ConfirmationResponses
    {
        public static async void confirmConfirmation(SocketCommandContext context, ulong userID)
        {
            var confirmation = ClassSetups.confirmations[userID];
            ClassSetups.confirmations.Remove(userID);
            var guild = context.Guild;
            var channel = context.Channel;
            var message = context.Message;
            Func.disableButtons(message);
            switch (confirmation.Purpose)
            {
                case "gift":
                    GiftConfirm(guild, channel, userID, confirmation.ULongArgument1, confirmation.ULongArgument2);
                    break;
                case "buyItem":
                    BuyConfirm(guild, channel, userID, confirmation.ItemArgument1, confirmation.ULongArgument1, confirmation.ULongArgument2);
                    break;
                case "muteUser":
                    MuteConfirm(context, userID, confirmation.ULongArgument1);
                    break;
                default:
                    await channel.SendMessageAsync("This should never appear. Ping Randy if it does pls, may the Moth be with you.");
                    break;
            }
        }
        public static async void cancelConfirmation(ulong userID, DiscordSocketClient _client)
        {
            var confirmation = ClassSetups.confirmations[userID];
            ClassSetups.confirmations.Remove(userID);
            var guild = _client.GetGuild(confirmation.GuildID);
            var channel = (ISocketMessageChannel)guild.GetChannel(confirmation.ChannelID);
            var message = (IUserMessage)channel.GetMessageAsync(confirmation.MessageID).Result;
            Func.disableButtons(message);
            switch (confirmation.Purpose)
            {
                case "gift":
                    await channel.SendMessageAsync("Gifting cancelled!");
                    break;
                case "buyItem":
                    await channel.SendMessageAsync("Item purchase cancelled!");
                    break;
                case "help":
                    break;
                case "muteUser":
                    await channel.SendMessageAsync("Muting cancelled!");
                    break;
                default:
                    await channel.SendMessageAsync("This should never appear. Ping Randy if it does pls, may the Moth be with you.");
                    break;
            }
        }
        public static async void GiftConfirm(SocketGuild guild, ISocketMessageChannel channel, ulong userID, ulong recipientID, ulong mothAmount)
        {
            ClassSetups.usersDict[userID].MothAmount -= mothAmount;
            ClassSetups.usersDict[recipientID].MothAmount += mothAmount;
            var title = $"You've gifted {mothAmount} moth";
            if (mothAmount != 1)
                title += "s";
            var recipient = guild.GetUser(recipientID);
            title += $" to {recipient.Nickname ?? recipient.Username}.";
            var desc = $"You currently have **{ClassSetups.usersDict[userID].MothAmount}** moth";
            if (ClassSetups.usersDict[userID].MothAmount != 1)
                desc += "s";
            desc += $".\n\n*Currently viewing <@{userID}>*";
            var eb = new EmbedBuilder();
            eb.WithColor(72, 139, 48);
            eb.WithTitle(title);
            eb.WithDescription(desc);
            await channel.SendMessageAsync("", embed: eb.Build());
        }
        public static async void BuyConfirm(SocketGuild guild, ISocketMessageChannel channel, ulong userID, Item? item, ulong mothAmount, ulong taxAmount)
        {
            if (item == null)
            {
                await channel.SendMessageAsync("Failed buying the item as it was NULL. Ping Randy if this appears pls, may the Moth be with you.");
                return;
            }
            if (item.Cooldown > 0)
                ClassSetups.usersDict[userID].LastTimes.Item[item.Id] = Convert.ToUInt64(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            try
            {
                ClassSetups.usersDict[userID].Items[item.Id]++;
            }
            catch (KeyNotFoundException)
            {
                ClassSetups.usersDict[userID].Items.Add(item.Id, 1);
            }
            ClassSetups.guildsDict[guild.Id].Taxes += taxAmount;
            ClassSetups.usersDict[userID].MothAmount -= mothAmount;
            var title = $"You've successfully bought {item.Name}.";
            var desc = $"{mothAmount} moth";
            if (mothAmount != 1)
                desc += "s";
            desc += $" were spent on the purchase, including {taxAmount} in taxes.\n\nYou currently have **{ClassSetups.usersDict[userID].MothAmount}** moth";
            if (ClassSetups.usersDict[userID].MothAmount != 1)
                desc += "s";
            desc += $" and {ClassSetups.usersDict[userID].Items[item.Id]} of this item.\n\n*Currently viewing <@{userID}>*";
            var eb = new EmbedBuilder();
            eb.WithColor(72, 139, 48);
            eb.WithTitle(title);
            eb.WithDescription(desc);
            await channel.SendMessageAsync("", embed: eb.Build());
        }
        public static async void MuteConfirm(SocketCommandContext Context, ulong userID, ulong muteID)
        {
            var eb = new EmbedBuilder();
            var user = Context.Guild.GetUser(muteID);
            if (user == null)
            {
                eb.WithDescription("Couldn't find the user. Maybe they've left the server already?");
                eb.WithColor(224, 33, 33);
                await Context.Channel.SendMessageAsync("", false, eb.Build());
                return;
            }
            var muteRole = Context.Guild.GetRole(ClassSetups.guildsDict[Context.Guild.Id].MuteRole);
            if (muteRole == null)
            {
                eb.WithDescription("Couldn't find a role to mute. Please ask the mods to assign it with `m!muterole`.");
                eb.WithColor(224, 33, 33);
                await Context.Channel.SendMessageAsync("", false, eb.Build());
                return;
            }
            var result = user.AddRoleAsync(muteRole);
            result.Wait();
            if (!result.IsCompleted)
            {
                eb.WithDescription("Failed to assign the role. Perhaps the bot is lacking permissions to do so?");
                eb.WithColor(224, 33, 33);
                await Context.Channel.SendMessageAsync("", false, eb.Build());
                return;
            }
            eb.WithDescription($"{user.Mention} has been muted for 5 minutes.");
            eb.WithColor(72, 139, 48);
            await Context.Channel.SendMessageAsync("", false, eb.Build());
            ClassSetups.usersDict[userID].Items["muteitem"]--;
            var t = new Thread(() => RememberUnmute(muteRole, user, Context.Guild));
            t.Start();
        }
        private static async void RememberUnmute(SocketRole muteRole, SocketGuildUser user, SocketGuild guild)
        {
            Thread.Sleep(300000);
            await user.RemoveRoleAsync(muteRole);
            await user.SendMessageAsync($"Your mute on {guild.Name} has run out.");
        }
    }
    public static class ClassSetups
    {
        public static Dictionary<string, ulong> emojisDict = new Dictionary<string, ulong>();
        public static Dictionary<string,Item> itemsDict = new Dictionary<string, Item>();
        public static List<Post> posts = new List<Post>();
        public static ulong lastReddit = 0;
        public static Dictionary<ulong, Confirmation> confirmations = new Dictionary<ulong, Confirmation>();
        public static Dictionary<ulong, Guild> guildsDict = new Dictionary<ulong, Guild>();
        public static Dictionary<ulong, User> usersDict = new Dictionary<ulong, User>();
        public static Dictionary<ulong, StorePage> storePagesDict = new Dictionary<ulong, StorePage>();
        public static void setUpDicts()
        {
            var deserializer = new DeserializerBuilder().Build();
            guildsDict = deserializer.Deserialize<Dictionary<ulong,Guild>>(new StringReader(File.ReadAllText("info/guild_info.yaml")));
            itemsDict = deserializer.Deserialize<Dictionary<string, Item>>(new StringReader(File.ReadAllText("info/item_info.yaml")));
            emojisDict = deserializer.Deserialize<Dictionary<string, ulong>>(new StringReader(File.ReadAllText("info/emoji_info.yaml")));
            usersDict = deserializer.Deserialize<Dictionary<ulong, User>>(new StringReader(File.ReadAllText("info/user_info.yaml")));
            storePagesDict = deserializer.Deserialize<Dictionary<ulong, StorePage>>(new StringReader(File.ReadAllText("info/store_info.yaml")));
            if (guildsDict == null || usersDict == null)
            {
                Console.WriteLine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
                File.CreateText(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) + "\\MOTHBOT DOWN.txt");
            }
            else {
                var childref = new ThreadStart(UpdateDynamicInfoTimer);
                Thread childThread = new Thread(childref);
                childThread.Start();
                var childref2 = new ThreadStart(CreateBackupTimer);
                Thread childThread2 = new Thread(childref2);
                childThread2.Start();
            }
            
        }
        private static async void UpdateDynamicInfoTimer()
        {
            var timer = new PeriodicTimer(TimeSpan.FromSeconds(60));
            int period = 2;
            while (await timer.WaitForNextTickAsync())
            {
                period = 3 - period;
                var succrun = writeDynamicInfo("info\\", period);
                if (!succrun)
                    period = 3 - period;
            }
        }
        public static bool writeDynamicInfo(string folder, int period)
        {
            var serializer = new SerializerBuilder().Build();
            var stringToWrite = serializer.Serialize(guildsDict);
            var stringToWrite2 = serializer.Serialize(usersDict);
            var f = File.CreateText(folder + "guild_info_temp.yaml");
            f.WriteLine(stringToWrite);
            f.Close();
            f = File.CreateText(folder + "user_info_temp.yaml");
            f.WriteLine(stringToWrite2);
            f.Close();
            var deserializer = new DeserializerBuilder().Build();
            var testData = deserializer.Deserialize<Dictionary<ulong, Guild>>(new StringReader(File.ReadAllText($"info/guild_info_{3-period}.yaml")));
            if (testData == null)
                return false;
            File.Delete(folder + $"guild_info_{period}.yaml");
            File.Move(folder + "guild_info.yaml", folder + $"guild_info_{period}.yaml");
            File.Move(folder + "guild_info_temp.yaml", folder + "guild_info.yaml");
            var testData2 = deserializer.Deserialize<Dictionary<ulong, User>>(new StringReader(File.ReadAllText($"info/user_info_{3-period}.yaml")));
            if (testData2 == null)  
                return false;
            File.Delete(folder + $"user_info_{period}.yaml");
            File.Move(folder + "user_info.yaml", folder + $"user_info_{period}.yaml");
            File.Move(folder + "user_info_temp.yaml", folder + "user_info.yaml");
            return true;
        }
        private static async void CreateBackupTimer()
        {
            Thread.Sleep(30);
            var timer = new PeriodicTimer(TimeSpan.FromSeconds(10800));
            while (await timer.WaitForNextTickAsync())
            {
                var time = DateTime.Now;
                var folderName = $"info\\backup\\{time.Year}-{time.Month}-{time.Day}-{time.Hour}-{time.Minute}\\";
                Directory.CreateDirectory(folderName);
                CreateBackup(folderName);
            }
        }
        public static void CreateBackup(string folder)
        {
            var serializer = new SerializerBuilder().Build();
            var stringToWrite = serializer.Serialize(guildsDict);
            var stringToWrite2 = serializer.Serialize(usersDict);
            var f = File.CreateText(folder + "guild_info.yaml");
            f.WriteLine(stringToWrite);
            f.Close();
            f = File.CreateText(folder + "user_info.yaml");
            f.WriteLine(stringToWrite2);
            f.Close();
        }

        public static void redditUpdate()
        {
            var id = Environment.GetEnvironmentVariable("MOTHBOT_REDDIT_ID", EnvironmentVariableTarget.User);
            var secret = Environment.GetEnvironmentVariable("MOTHBOT_REDDIT_SECRET", EnvironmentVariableTarget.User);
            var token = Environment.GetEnvironmentVariable("MOTHBOT_REDDIT_TOKEN", EnvironmentVariableTarget.User);
            var reddit = new RedditClient(appId: id, appSecret: secret, refreshToken: token);
            posts = reddit.Subreddit("Moths").Posts.GetTop(new TimedCatSrListingInput(t: "week", limit: 30));
            posts.RemoveAll(post =>
            {
                try
                {
                    var obj = (LinkPost)post;
                    if (obj.URL.Contains("v.redd.it") || obj.URL.Contains("reddit.com/gallery"))
                        return true;
                    else
                        return false;
                }
                catch (InvalidCastException)
                {
                    return true;
                }
            });
        }
    }
}