
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MothBot
{
    public class EconomyFunc
    {
        public static Item? findItem(string str)
        {
            Item? item = null;
            bool foundItem = false;
            if (str.Length < 3)
                return null;
            foreach (var x in ClassSetups.itemsDict)
            {
                if (x.Key.StartsWith(str, StringComparison.OrdinalIgnoreCase) || x.Value.Name.StartsWith(str, StringComparison.OrdinalIgnoreCase))
                {
                    if (foundItem)
                    {
                        item = null;
                        break;
                    }
                    item = x.Value;
                    foundItem = true;
                }
            }
            return item;
        }
        public static ulong calcItemPrice(Item item, ulong mothAmount)
        {
            ulong price = item.Price.BasePrice;
            price += (ulong)Math.Ceiling(mothAmount * item.Price.PricePortion);
            price += calcTaxPrice(item.Price.Tax, mothAmount, item.Price.BasePrice*3);
            return price;
        }
        public static ulong calcTaxPrice(double taxPercent, ulong mothAmount, ulong cap)
        {
            Console.WriteLine($"Calculating taxes:\nTax rate:{taxPercent}\nCurrent moth amount:{mothAmount}\nTax cap:{cap}");
            ulong price = 0;
            if (mothAmount>1000)
                price += (ulong)Math.Ceiling(Math.Min((mothAmount - 1000) * taxPercent / 10, 900 * taxPercent));
            Console.WriteLine($"After 1k-10k tax bracket:{price}");
            if (mothAmount > 10000)
                price += (ulong)Math.Ceiling(Math.Min((mothAmount - 10000) * taxPercent / 2, 45000 * taxPercent));
            Console.WriteLine($"After 10k-100k tax bracket:{price}");
            if (price < 0)
                return 0;
            if (mothAmount > 100000)
                price += (ulong)Math.Ceiling((mothAmount - 100000) * taxPercent);
            Console.WriteLine($"After 100k+ tax bracket:{price}");
            return Math.Min(price,cap);
        }
    }
    [Name("Economy commands")]
    [Summary("Commands that have to do with the moth currency")]
    public class Economy : ModuleBase<SocketCommandContext>
    {
        [Command("daily")]
        [Summary("Gives a daily reward.\n\nUsage: `m!daily`")]
        //[Alias("moth", "oths", "oth")]
        public async Task DailyAsync([Remainder()] string s = "")
        {
            var lastTime = ClassSetups.usersDict[Context.User.Id].LastTimes.Daily / 86400;
            ulong thisTime = Convert.ToUInt64(Context.Message.Timestamp.ToUnixTimeSeconds());
            Boolean claimedReward = false;
            if (thisTime / 86400 > lastTime)
            {
                ClassSetups.usersDict[Context.User.Id].MothAmount += 1000;
                ClassSetups.usersDict[Context.User.Id].LastTimes.Daily = thisTime;
                claimedReward = true;
            }
            var eb = new EmbedBuilder();
            string desc = "";
            string title = "";
            if (claimedReward)
            {
                title = "You have claimed your daily reward!";
                desc += $"You currently have **{ClassSetups.usersDict[Context.User.Id].MothAmount}** moth";
                eb.WithColor(72, 139, 48);
            }
            else
            {
                title = "You can't claim your daily reward yet.";
                desc += $"{Func.convertSeconds((lastTime + 1) * 86400 - thisTime)}";
                if (desc.EndsWith("s"))
                    desc += " remain";
                else
                    desc += " remains";
                desc += $" until your next daily reward.\n\nYou currently have **{ClassSetups.usersDict[Context.User.Id].MothAmount}** moth";
                eb.WithColor(224, 33, 33);
            }
            if (ClassSetups.usersDict[Context.User.Id].MothAmount != 1)
                desc += "s.";
            else
                desc += ".";
            desc += $"\n\n*Currently viewing {Context.Message.Author.Mention}*";
            eb.WithTitle(title);
            eb.WithDescription(desc);
            await Context.Channel.SendMessageAsync("", false, eb.Build());
        }
        [Command("search")]
        [Summary("You go outside and touch a moth on grass.\n\nUsage: `m!search`")]
        [Alias("outside", "s")]
        public async Task SearchAsync([Remainder()] string s = "")
        {
            if (Context.Channel.Id == 608912123317321744)
            {
                await Context.Channel.SendMessageAsync("Searching for moths has been disabled in <#608912123317321744>, please move to <#935420527051419688>.");
                return;
            }
            var eb = new EmbedBuilder();
            var lastTime = ClassSetups.usersDict[Context.User.Id].LastTimes.Search;
            ulong thisTime = Convert.ToUInt64(Context.Message.Timestamp.ToUnixTimeSeconds());
            bool claimedReward = false;
            string title = "";
            string desc = "";
            if (thisTime - 30 >= lastTime)
            {
                ClassSetups.usersDict[Context.User.Id].LastTimes.Search = thisTime;
                Random rand = new Random(DateTime.Now.ToString().GetHashCode());
                int rv = rand.Next() % 100;
                Console.WriteLine(rv + " " + Context.User.Username);
                if (rv <= 1)
                {
                    title += $"You've abducted a moth from outside! It seems to be shiny, worth 100 times as much!";
                    ClassSetups.usersDict[Context.User.Id].MothAmount += 100;
                    eb.WithColor(72, 139, 48);
                }
                else if (rv < 15)
                {
                    title += $"You've abducted a cluster of moths from outside! Seems as if there were 5.";
                    ClassSetups.usersDict[Context.User.Id].MothAmount += 5;
                    eb.WithColor(72, 139, 48);
                }
                else if (rv >= 80)
                {
                    title += $"Seems like there wasn't anyone outside.";
                    eb.WithColor(224, 33, 33);
                }
                else
                {
                    title += $"You've abducted a moth from outside!";
                    ClassSetups.usersDict[Context.User.Id].MothAmount += 1;
                    eb.WithColor(72, 139, 48);
                }
                claimedReward = true;
            }
            if (claimedReward)
            {
                desc += $"You currently have **{ClassSetups.usersDict[Context.User.Id].MothAmount}** moth";
            }
            else
            {
                title = "You're too tired to touch grass.";
                desc += $"{Func.convertSeconds(lastTime + 30 - thisTime)}";
                if (desc.EndsWith("s"))
                    desc += " remain";
                else
                    desc += " remains";
                desc += $" until you're next able to go outside.\n\nYou currently have **{ClassSetups.usersDict[Context.User.Id].MothAmount}** moth";
                eb.WithColor(224, 33, 33);
            }
            if (ClassSetups.usersDict[Context.User.Id].MothAmount != 1)
                desc += "s.";
            else
                desc += ".";
            desc += $"\n\n*Currently viewing {Context.Message.Author.Mention}*";
            eb.WithTitle(title);
            eb.WithDescription(desc);
            await Context.Channel.SendMessageAsync("", false, eb.Build());
        }

        [Command("balance")]
        [Summary("Checks the amount of moths you currently have.\n\nUsage: `m!balance`")]
        [Alias("bal", "b", "wallet")]
        private async Task BalanceAsync([Remainder()] string s = "")
        {
            var eb = new EmbedBuilder();
            string title = $"You currently have {ClassSetups.usersDict[Context.User.Id].MothAmount} moth";
            if (ClassSetups.usersDict[Context.User.Id].MothAmount != 1)
                title += "s.";
            else
                title += ".";
            eb.WithTitle(title);
            eb.WithDescription($"*Currently viewing {Context.Message.Author.Mention}*");
            eb.WithColor(226, 204, 4);
            await Context.Channel.SendMessageAsync("", false, eb.Build());
        }
        [Command("leaderboard")]
        [Summary("Checks the moth leaderboard.\n\nUsage: `m!leaderboard`")]
        [Alias("lb", "rank")]
        private async Task LeaderboardAsync([Remainder()] string s = "")
        {
            var user = (SocketGuildUser)Context.User;
            var mothCounts = new List<Tuple<ulong, SocketGuildUser>>();
            foreach (var userData in ClassSetups.usersDict)
            {
                ulong userID = userData.Key;
                var selectedUser = Context.Guild.GetUser(userID);
                //Console.WriteLine(file);
                if (selectedUser == null || userData.Value.MothAmount == 0)
                    continue;
                mothCounts.Add(Tuple.Create(userData.Value.MothAmount, selectedUser));
            }
            mothCounts = mothCounts.OrderByDescending(t => t.Item1).ThenBy(t => t.Item2.Id).ToList();
            string desc = "";
            ulong moths = mothCounts[0].Item1;
            int yourIndex = -1;
            for (int i = 0; i < mothCounts.Count; i++)
            {
                if (mothCounts[i].Item1 < moths)
                {
                    moths = mothCounts[i].Item1;
                }
                if (mothCounts[i].Item2.Id == user.Id)
                {
                    desc += "**";
                    yourIndex = i;
                }
                //Console.WriteLine(mothCounts[i]);
                desc += $"{i + 1}. {mothCounts[i].Item2.Nickname ?? mothCounts[i].Item2.Username}: {mothCounts[i].Item1} moth";
                if (mothCounts[i].Item1 != 1)
                    desc += "s";
                desc += ".";
                if (mothCounts[i].Item2.Id == user.Id)
                    desc += "**";
                desc += "\n";
            }
            if (yourIndex == -1)
                desc += "\n**Currently you don't have any moths**";
            var eb = new EmbedBuilder();
            eb.WithTitle("Server leaderboard:");
            eb.WithDescription(desc);
            eb.WithColor(244, 178, 23);
            await Context.Channel.SendMessageAsync("", embed: eb.Build());
        }
        [Command("fiftyfifty")]
        [Summary("Bets certain amount of moths to be doubled or lost.\n\nUsage: `m!fiftyfifty <amount of moths>` or `m!fiftyfifty all`")]
        [Alias("bet")]
        private async Task fiftyfiftyAsync([Summary("Amount of moths to bet")] string mothAmountStr = "")
        {
            var eb = new EmbedBuilder();
            ulong mothAmount = 0;
            if (mothAmountStr != "all")
                try
                {
                    mothAmount = Convert.ToUInt64(mothAmountStr);
                }
                catch (FormatException)
                {
                    eb.WithColor(224, 33, 33);
                    eb.WithDescription("Please enter a number or \"all\" as the amount of moths!");
                    await Context.Channel.SendMessageAsync("", embed: eb.Build());
                    return;
                }
            else
                mothAmount = ClassSetups.usersDict[Context.User.Id].MothAmount;
            if (mothAmount < 1)
            {
                eb.WithColor(224, 33, 33);
                eb.WithDescription("Please bet a positive amount of moths!");
                await Context.Channel.SendMessageAsync("", embed: eb.Build());
                return;
            }
            if (ClassSetups.usersDict[Context.User.Id].MothAmount < mothAmount)
            {
                eb.WithColor(224, 33, 33);
                eb.WithDescription("You don't have that many moths!");
                await Context.Channel.SendMessageAsync("", embed: eb.Build());
                return;
            }
            string desc = "";
            string title = "";
            Random rand = new Random(DateTime.Now.ToString().GetHashCode());
            int rv = 1 - rand.Next(0, 2);
            if (rv == 0)
            {
                ClassSetups.usersDict[Context.User.Id].MothAmount += mothAmount;
                title += $"You've won **{mothAmount}** moth";
                eb.WithColor(72, 139, 48);
                if (mothAmount != 1)
                    title += "s";
                title += "!";
            }
            else
            {
                ClassSetups.usersDict[Context.User.Id].MothAmount -= mothAmount;
                eb.WithColor(224, 33, 33);
                title += $"You've lost **{mothAmount}** moth";
                if (mothAmount != 1)
                    title += "s";
                title += ".";
            }
            desc += $"You currently have **{ClassSetups.usersDict[Context.User.Id].MothAmount}** moth";
            if (ClassSetups.usersDict[Context.User.Id].MothAmount != 1)
                desc += "s";
            desc += $".\n\n*Currently viewing {Context.User.Mention}*";
            eb.WithTitle(title);
            eb.WithDescription(desc);
            await Context.Channel.SendMessageAsync("", false, eb.Build());
        }

        [Command("gift")]
        [Summary("Gifts a certain amount of moths to a user.\n\nUsage: `m!gift <amount> <user>`")]
        [Alias("give")]
        private async Task giftAsync([Summary("Amount of moths to gift")] ulong mothAmount, [Remainder] SocketGuildUser recipient)
        {
            var eb = new EmbedBuilder();
            eb.WithColor(72, 139, 48);
            if (mothAmount < 1)
            {
                eb.WithColor(224, 33, 33);
                eb.WithDescription("Please gift a positive amount of moths!");
                await Context.Channel.SendMessageAsync("", embed: eb.Build());
                return;
            }
            if (ClassSetups.usersDict[Context.User.Id].MothAmount < mothAmount)
            {
                eb.WithColor(224, 33, 33);
                eb.WithDescription("You don't have that many moths!");
                await Context.Channel.SendMessageAsync("", embed: eb.Build());
                return;
            }
            if (Context.User.Id == recipient.Id)
            {
                eb.WithColor(224, 33, 33);
                eb.WithDescription("You can't gift moths to yourself!");
                await Context.Channel.SendMessageAsync("", embed: eb.Build());
                return;
            }
            var desc = $"Are you sure you want to gift {mothAmount} moth";
            if (mothAmount != 1)
                desc += "s";
            desc += $" to {recipient.Mention}?";
            var builder = new ComponentBuilder()
                .WithButton("Confirm", "confirmation-confirm", ButtonStyle.Success)
                .WithButton("Cancel", "confirmation-cancel", ButtonStyle.Danger);

            var message = await Context.Channel.SendMessageAsync(desc, components: builder.Build());
            var newConfirmation = new Confirmation()
            {
                MessageID = message.Id,
                ChannelID = Context.Channel.Id,
                GuildID = Context.Guild.Id,
                ULongArgument1 = recipient.Id,
                ULongArgument2 = mothAmount,
                Purpose = "gift"
            };
            ClassSetups.confirmations.Add(Context.User.Id, newConfirmation);
            var childref = new ThreadStart(GiftConfirmSetup);
            Thread childThread = new Thread(childref);
            childThread.Start();
        }

        private async void GiftConfirmSetup()
        {
            var messageID = ClassSetups.confirmations[Context.User.Id].MessageID;
            var message = (IUserMessage)Context.Channel.GetMessageAsync(messageID).Result;
            Thread.Sleep(10000);
            if (ClassSetups.confirmations.ContainsKey(Context.User.Id) && ClassSetups.confirmations[Context.User.Id].MessageID == messageID)
            {
                await Context.Channel.SendMessageAsync("Gifting cancelled!");
                ClassSetups.confirmations.Remove(Context.User.Id);
                Func.disableButtons(message);
            }
        }

        [Command("inventory")]
        [Summary("Opens the inventory, providing a list of items you own.\n\nUsage: `m!inventory`")]
        [Alias("inv")]
        private async Task inventoryAsync([Remainder()] string s = "")
        {
            var eb = new EmbedBuilder();
            eb.WithTitle("These are the items you currently have:");
            bool hasItems = false;
            string desc = "";
            foreach (var itemDict in ClassSetups.usersDict[Context.User.Id].Items)
            {
                if (itemDict.Value>0)
                {
                    hasItems = true;
                    desc += $"**{ClassSetups.itemsDict[itemDict.Key].Name}** - {itemDict.Value}\n{ClassSetups.itemsDict[itemDict.Key].ShortDesc}\n\n";
                }
            }
            if (!hasItems)
                desc = "You currently have no items\n\n";
            desc += $"*Currently viewing {Context.Message.Author.Mention}*";
            eb.WithDescription(desc);
            await Context.Channel.SendMessageAsync("", false, eb.Build());
        }

        [Command("shop")]
        [Summary("Opens the shop to see what can be bought.\n\nUsage: `m!shop <page>`")]
        [Alias("store")]
        private async Task shopAsync([Summary("Amount of moths to gift")] ulong page = 0)
        {
            var eb = new EmbedBuilder();
            var desc = "";
            var title = "";
            if (page == 0)
            {
                title += $"Moth Store - {ClassSetups.usersDict[Context.User.Id].MothAmount} moth";
                if (ClassSetups.usersDict[Context.User.Id].MothAmount != 1)
                    title += "s";
                desc += "Use `m!shop <page>` to view the items in the page.";
                foreach (var pageDesc in ClassSetups.storePagesDict)
                {
                    ulong pageID = pageDesc.Key;
                    var pageInfo = pageDesc.Value;
                    desc += $"\n\n**Page {pageID}: {pageInfo.Name}**\n{pageInfo.Desc}";
                }
                eb.WithTitle(title);
            }
            else if (!ClassSetups.storePagesDict.ContainsKey(page))
            {
                desc = "This page doesn't exist!";
            }
            else
            {
                title += $"Moth Store - {ClassSetups.usersDict[Context.User.Id].MothAmount} moth";
                if (ClassSetups.usersDict[Context.User.Id].MothAmount != 1)
                    title += "s";
                eb.WithTitle(title);
                desc += "To buy an item, use `m!buy <item>`";
                foreach (string item in ClassSetups.storePagesDict[page].Items)
                {
                    var itemInfo = ClassSetups.itemsDict[item];
                    var price = EconomyFunc.calcItemPrice(itemInfo, ClassSetups.usersDict[Context.User.Id].MothAmount);
                    desc += $"\n\n**{itemInfo.Name} - {price} moths**\n{itemInfo.ShortDesc}";
                }
            }
            eb.WithDescription(desc);
            await Context.Channel.SendMessageAsync("", embed: eb.Build());
        }
        /*mothInfo["amount"] -= price;
        title = $"Successfully purchased {itemInfo.Name}";
        desc += $"You currently have **{ClassSetups.usersDict[Context.User.Id].MothAmount}** moth";
        if (ClassSetups.usersDict[Context.User.Id].MothAmount != 1)
            desc += "s";
        desc += $".\n\n*Currently viewing {Context.User.Mention}*";
        eb.WithTitle(title);
        eb.WithDescription(desc);
        await Context.Channel.SendMessageAsync("", embed: eb.Build());*/
    }
}