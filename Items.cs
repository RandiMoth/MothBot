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

    [Name("Item commands")]
    [Summary("Commands that have to do with using items")]
    public class Items : ModuleBase<SocketCommandContext>
    {

        [Command("item")]
        [Summary("Sees the item info.\n\nUsage: `m!item <item>`")]
        private async Task itemAsync([Remainder] string item)
        {
            var eb = new EmbedBuilder();
            var desc = "";
            var itemInfo = EconomyFunc.findItem(item);
            if (itemInfo == null)
            {
                eb.WithColor(224, 33, 33);
                eb.WithDescription($"Failed to find the item {item}");
                await Context.Channel.SendMessageAsync("", embed: eb.Build());
                return;
            }
            eb.WithTitle(itemInfo.Name);
            desc += itemInfo.LongDesc;
            desc += $"\n\nFull price: {EconomyFunc.calcItemPrice(itemInfo, Info.usersDict[Context.User.Id].MothAmount)}\n**Price breakdown**:\n­ ­ ­ ­ Base price: {itemInfo.Price.BasePrice}\n";
            if (itemInfo.Price.PricePortion != 0)
                desc += $"­ ­ ­ ­ {itemInfo.Price.PricePortion * 100}% total moth amount: {(ulong)Math.Ceiling(itemInfo.Price.PricePortion * Info.usersDict[Context.User.Id].MothAmount)}\n";
            desc += $"­ ­ ­ ­ {itemInfo.Price.Tax * 100}% tax: {EconomyFunc.calcTaxPrice(itemInfo.Price.Tax, Info.usersDict[Context.User.Id].MothAmount, itemInfo.Price.BasePrice * 3)}\n";
            //if (itemInfo.Cooldown != 0)
                //desc += $"\nThis item can only be purchased once every {Func.convertSeconds(itemInfo.Cooldown)}.\n";
            if (itemInfo.Max != 0)
                desc += $"\nYou cannot have more than {itemInfo.Max} of this item.\n";
            desc += $"\nYou currently have {Info.usersDict[Context.User.Id].MothAmount} moth";
            if (Info.usersDict[Context.User.Id].MothAmount != 0)
                desc += "s and ";
            try
            {
                desc += $"{Info.usersDict[Context.User.Id].Items[itemInfo.Id]}";
            }
            catch (KeyNotFoundException)
            {
                desc += "0";
            }
            desc += " of this item.";
            desc += $"\n\n*Currently viewing {Context.User.Mention}*";
            eb.WithDescription(desc);
            await Context.Channel.SendMessageAsync("", embed: eb.Build());
        }

        [Command("buy")]
        [Summary("Purchases the specified item.\n\nUsage: `m!buy <item>`")]
        [Alias("purchase")]
        private async Task buyAsync([Remainder] string item)
        {
            item = item.Trim().ToLower();
            var eb = new EmbedBuilder();
            var desc = "";
            var itemInfo = EconomyFunc.findItem(item);
            if (itemInfo == null)
            {
                eb.WithColor(224, 33, 33);
                eb.WithDescription($"Failed to find the item {item}");
                await Context.Channel.SendMessageAsync("", embed: eb.Build());
                return;
            }
            var price = EconomyFunc.calcItemPrice(itemInfo, Info.usersDict[Context.User.Id].MothAmount);
            if (price > Info.usersDict[Context.User.Id].MothAmount)
            {
                eb.WithColor(224, 33, 33);
                eb.WithDescription("You don't have that many moths!");
                await Context.Channel.SendMessageAsync("", embed: eb.Build());
                return;
            }
            if (itemInfo.Max > 0)
            {
                try
                {
                    var itemCount = Info.usersDict[Context.User.Id].Items[itemInfo.Id];
                    if (itemCount >= itemInfo.Max)
                    {
                        eb.WithColor(224, 33, 33);
                        eb.WithDescription($"You cannot have more than {itemInfo.Max} of {itemInfo.Name} at a time!");
                        await Context.Channel.SendMessageAsync("", embed: eb.Build());
                        return;
                    }
                }
                catch (KeyNotFoundException)
                {
                    Info.usersDict[Context.User.Id].Items.Add(itemInfo.Id, 0);
                }
            }
            if (itemInfo.Cooldown > 0)
            {
                try
                {
                    var lastTime = Info.usersDict[Context.User.Id].LastTimes.Item[itemInfo.Id];
                    ulong thisTime = Convert.ToUInt64(Context.Message.Timestamp.ToUnixTimeSeconds());
                    if (thisTime - lastTime < itemInfo.Cooldown)
                    {
                        eb.WithColor(224, 33, 33);
                        //eb.WithDescription($"You can't buy this item yet as it's on a cooldown of {Func.convertSeconds(itemInfo.Cooldown)}!\n\nYou'll be able to purchase this item in {Func.convertSeconds(lastTime - thisTime + itemInfo.Cooldown)}.");
                        await Context.Channel.SendMessageAsync("", embed: eb.Build());
                        return;
                    }
                }
                catch (KeyNotFoundException)
                {
                    Info.usersDict[Context.User.Id].LastTimes.Item.Add(itemInfo.Id, 0);
                }
            }
            desc = $"Are you sure you want to purchase {itemInfo.Name} for {price} moth";
            if (price != 1)
                desc += "s";
            desc += "?";
            var newConfirmation = new Confirmation()
            {
                ULongArgument1 = price,
                ULongArgument2 = EconomyFunc.calcTaxPrice(itemInfo.Price.Tax, Info.usersDict[Context.User.Id].MothAmount, itemInfo.Price.BasePrice * 3),
                ItemArgument1 = itemInfo,
                Purpose = "buyItem"
            };
            newConfirmation.Setup(Context, desc);
        }
        [Command("mute")]
        [Summary("Mutes the specified user.\n\nUsage: `m!mute <user>`")]
        private async Task muteAsync(SocketGuildUser user)
        {
            var eb = new EmbedBuilder();
            try
            {
                if (Info.usersDict[Context.User.Id].Items["muteitem"]==0)
                {
                    eb.WithDescription("You don't have enough mutes!");
                    eb.WithColor(224, 33, 33);
                    await Context.Channel.SendMessageAsync("", false, eb.Build());
                    return;
                }
            }
            catch (KeyNotFoundException)
            {
                eb.WithDescription("You don't have enough mutes!");
                eb.WithColor(224, 33, 33);
                await Context.Channel.SendMessageAsync("", false, eb.Build());
                return;
            }
            var muteRole = Context.Guild.GetRole(Info.guildsDict[Context.Guild.Id].MuteRole);
            if (muteRole == null)
            {
                eb.WithDescription("Couldn't find a role to mute. Please ask the mods to assign it with `m!muterole`.");
                eb.WithColor(224, 33, 33);
                await Context.Channel.SendMessageAsync("", false, eb.Build());
                return;
            }
            if (user.IsBot)
            {
                eb.WithDescription("You cannot mute a bot!");
                eb.WithColor(224, 33, 33);
                await Context.Channel.SendMessageAsync("", false, eb.Build());
                return;
            }
            if (user.GuildPermissions.ManageGuild)
            {
                eb.WithDescription("You cannot mute a moderator!");
                eb.WithColor(224, 33, 33);
                await Context.Channel.SendMessageAsync("", false, eb.Build());
                return;
            }
            if (user.Roles.Any(x => x.Id == muteRole.Id))
            {
                eb.WithDescription("The target is already muted!");
                eb.WithColor(224, 33, 33);
                await Context.Channel.SendMessageAsync("", false, eb.Build());
                return;
            }
            string desc = $"Are you sure you want to mute {user.Mention} for 5 minutes?";
            var newConfirmation = new Confirmation()
            {
                ULongArgument1 = user.Id,
                Purpose = "muteUser"
            };
            newConfirmation.Setup(Context, desc);
        }
    }
}