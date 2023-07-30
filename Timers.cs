using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MothBot
{
    [Name("Timer commands")]
    [Summary("Commands related to dealing with timers")]
    public class TimerCommands : ModuleBase<SocketCommandContext>
    {
        [Command("timerstart")]
        [Alias("starttimer", "start", "timer")]
        [Summary("Starts a timer of the specified length, arguments separate by quotes. Upon ending, the text will be sent in the channel where the command was executed. \\\\\" allows escaping a quotation mark. Optionally allows specifying a short name as separate from the text. \n\nUsage: `m!timerstart 1h 2m 3s \"Timer's name\" \"Text to fire\"`, `m!timerstart 01:02:03 \"Text to fire and timer's name\"`.")]
        private async Task timerStartAsync([Remainder] string inputRaw = "")
        {
            var eb = new EmbedBuilder();
            eb.WithColor(244, 178, 23);
            var inputs = Func.parseQuotes(inputRaw);
            if (inputs.Count < 2 || inputs.Count > 3)
            {
                eb.WithDescription("Invalid amount of arguments. Make sure that the command follows the usage examples: `m!timerstart 1h 2m 3s \"Timer's name\" \"Text to fire\"`, `m!timerstart 01:02:03 \"Text to fire and timer's name\"`");
                await Context.Channel.SendMessageAsync("", embed: eb.Build());
                return;
            }
            int? seconds = Func.HumanTimeToSeconds(inputs[0]);
            if (seconds == null)
            {
                eb.WithDescription("Failed to convert the timer to seconds. Make sure that the time is in a supported format such as `01:02:03` or `1d 2h 3m 4s`.");
                await Context.Channel.SendMessageAsync("", embed: eb.Build());
                return;
            }
            if (ClassSetups.guildsDict[Context.Guild.Id].Timers.Any(timer => timer.Name.Equals(inputs[1], StringComparison.OrdinalIgnoreCase)))
            {
                eb.WithDescription("A timer with this name already exists in this server.");
                await Context.Channel.SendMessageAsync("", embed: eb.Build());
                return;
            }
            ulong thisTime = Convert.ToUInt64(Context.Message.Timestamp.ToUnixTimeSeconds());
            eb.WithColor(51, 127, 213);
            eb.WithDescription($"The timer \"{inputs[1]}\" is set to fire <t:{thisTime + (ulong)seconds}:R>.");
            Timer timer = new Timer() { Channel = Context.Channel.Id, Name = inputs[1], Text = inputs[inputs.Count - 1], TimeToFire = thisTime + (ulong)seconds, User = Context.User.Id, OriginalDuration = (int)seconds, Active = seconds < 60 };
            ClassSetups.guildsDict[Context.Guild.Id].Timers.Add(timer);
            await Context.Channel.SendMessageAsync("", embed: eb.Build());
            if (timer.Active)
            {
                Thread childThread = new Thread(() => Func.MakeReminderAsync(timer, Context.Guild, delay: (int)seconds));
                childThread.Start();
            }
        }
        [Command("timercheck")]
        [Alias("checktimer", "check")]
        [Summary("Checks the status of the timer with the specified name. Case-insensitive, but doesn't ignore punctuation. \n\nUsage: `m!timercheck Timer's name`")]
        private async Task timerCheckAsync([Remainder] string timerName = "")
        {
            var eb = new EmbedBuilder();
            eb.WithColor(244, 178, 23);
            var timers = ClassSetups.guildsDict[Context.Guild.Id].Timers.Where(timer => timer.Name.Equals(timerName, StringComparison.OrdinalIgnoreCase));
            if (timers.Count() == 0)
            {
                eb.WithDescription("Couldn't find the timer with the specified name");
                await Context.Channel.SendMessageAsync("", embed: eb.Build());
                return;
            }
            var timer = timers.First();
            eb.WithColor(51, 127, 213);
            eb.WithTitle(timer.Name);
            string txt = timer.Text + $"\nTimer for {Func.convertSeconds((ulong)timer.OriginalDuration)}.\n\n";
            if (timer.Paused)
            {
                txt += $"*This timer is currently paused*. If unpaused right now, it would end in {Func.convertSeconds((ulong)timer.RemainingTime)}.";
            }
            else
            {
                txt += $"This timer is currently active and will end <t:{timer.TimeToFire}:R>.";
            }
            eb.WithDescription(txt);
            await Context.Channel.SendMessageAsync("", false, eb.Build());
        }
        [Command("timerpause")]
        [Alias("pausetimer", "pause")]
        [Summary("Pauses the timer with the specified name. Case-insensitive, but doesn't ignore punctuation. \n\nUsage: `m!timerpause Timer's name`")]
        private async Task timerPauseAsync([Remainder] string timerName = "")
        {
            var eb = new EmbedBuilder();
            eb.WithColor(244, 178, 23);
            var index = ClassSetups.guildsDict[Context.Guild.Id].Timers.FindIndex(t => t.Name.Equals(timerName, StringComparison.OrdinalIgnoreCase));
            if (index == -1)
            {
                eb.WithDescription("Couldn't find the timer with the specified name");
                await Context.Channel.SendMessageAsync("", embed: eb.Build());
                return;
            }
            var timer = ClassSetups.guildsDict[Context.Guild.Id].Timers[index];
            if (timer.Paused)
            {
                eb.WithDescription("This timer is already paused!");
                await Context.Channel.SendMessageAsync("", embed: eb.Build());
                return;
            }
            if (timer.User != Context.User.Id && Context.User.Id != 491998313399189504)
            {
                eb.WithDescription("You can't pause other people's timers!");
                await Context.Channel.SendMessageAsync("", embed: eb.Build());
                return;
            }
            ClassSetups.guildsDict[Context.Guild.Id].Timers[index].Paused = true;
            ClassSetups.guildsDict[Context.Guild.Id].Timers[index].Active = false;
            ulong thisTime = Convert.ToUInt64(Context.Message.Timestamp.ToUnixTimeSeconds());
            ClassSetups.guildsDict[Context.Guild.Id].Timers[index].RemainingTime = (int)(timer.TimeToFire - thisTime);
            eb.WithColor(51, 127, 213);
            eb.WithDescription($"The timer \"{timer.Name}\" has been paused. If unpaused, it will end in {Func.convertSeconds(timer.TimeToFire - thisTime)}.");
            await Context.Channel.SendMessageAsync("", false, eb.Build());
        }
        [Command("timerunpause")]
        [Alias("unpausetimer", "unpause")]
        [Summary("Unpauses the timer with the specified name. Case-insensitive, but doesn't ignore punctuation. \n\nUsage: `m!timerunpause Timer's name`")]
        private async Task timerUnpauseAsync([Remainder] string timerName = "")
        {
            var eb = new EmbedBuilder();
            eb.WithColor(244, 178, 23);
            var index = ClassSetups.guildsDict[Context.Guild.Id].Timers.FindIndex(t => t.Name.Equals(timerName, StringComparison.OrdinalIgnoreCase));
            if (index == -1)
            {
                eb.WithDescription("Couldn't find the timer with the specified name");
                await Context.Channel.SendMessageAsync("", embed: eb.Build());
                return;
            }
            var timer = ClassSetups.guildsDict[Context.Guild.Id].Timers[index];
            if (!timer.Paused)
            {
                eb.WithDescription("This timer is already unpaused!");
                await Context.Channel.SendMessageAsync("", embed: eb.Build());
                return;
            }
            if (timer.User != Context.User.Id && Context.User.Id != 491998313399189504)
            {
                eb.WithDescription("You can't unpause other people's timers!");
                await Context.Channel.SendMessageAsync("", embed: eb.Build());
                return;
            }
            ClassSetups.guildsDict[Context.Guild.Id].Timers[index].Paused = false;
            ulong thisTime = Convert.ToUInt64(Context.Message.Timestamp.ToUnixTimeSeconds());
            ClassSetups.guildsDict[Context.Guild.Id].Timers[index].TimeToFire = thisTime + (ulong)timer.RemainingTime;
            eb.WithColor(51, 127, 213);
            eb.WithDescription($"The timer \"{timer.Name}\" has been unpaused and scheduled to fire <t:{thisTime + (ulong)timer.RemainingTime}:R>.");
            if (timer.RemainingTime < 60)
            {
                ClassSetups.guildsDict[Context.Guild.Id].Timers[index].Active = true;
                Thread childThread = new Thread(() => Func.MakeReminderAsync(timer, Context.Guild, delay: timer.RemainingTime));
                childThread.Start();
            }
            await Context.Channel.SendMessageAsync("", false, eb.Build());
        }
        [Command("timerdelete")]
        [Alias("deletetimer", "timercancel", "canceltimer", "cancel")]
        [Summary("Cancels the timer with the specified name. Case-insensitive, but doesn't ignore punctuation. \n\nUsage: `m!timerdelete Timer's name`")]
        private async Task timerDeleteAsync([Remainder] string timerName = "")
        {
            var eb = new EmbedBuilder();
            eb.WithColor(244, 178, 23);
            var index = ClassSetups.guildsDict[Context.Guild.Id].Timers.FindIndex(t => t.Name.Equals(timerName, StringComparison.OrdinalIgnoreCase));
            if (index == -1)
            {
                eb.WithDescription("Couldn't find the timer with the specified name");
                await Context.Channel.SendMessageAsync("", embed: eb.Build());
                return;
            }
            var timer = ClassSetups.guildsDict[Context.Guild.Id].Timers[index];
            if (timer.User != Context.User.Id && Context.User.Id != 491998313399189504)
            {
                eb.WithDescription("You can't delete other people's timers!");
                await Context.Channel.SendMessageAsync("", embed: eb.Build());
                return;
            }
            ClassSetups.guildsDict[Context.Guild.Id].Timers.RemoveAt(index);
            eb.WithColor(51, 127, 213);
            eb.WithDescription($"The timer \"{timer.Name}\" has been deleted.");
            await Context.Channel.SendMessageAsync("", false, eb.Build());
        }
        [Command("timerlist")]
        [Alias("listtimers")]
        [Summary("Lists the timer of a user. If the user isn't specified, defaults to the person who used the command. \n\nUsage: `m!timerlist @username`")]
        private async Task timerListAsync([Remainder] SocketUser? user = null)
        {
            var eb = new EmbedBuilder();
            if (user == null)
                user = Context.User;
            var timers = ClassSetups.guildsDict[Context.Guild.Id].Timers.Where(t => t.User == user.Id);
            eb.WithColor(51, 127, 213);
            if (timers.Count() == 0)
                eb.WithDescription("No timers have been found");
            else
            {
                string txt = "";
                foreach (var timer in timers)
                {
                    txt += $"**{timer.Name}**";
                    if (timer.Text != timer.Name)
                        txt += $"\n{timer.Text}";
                    if (timer.Paused)
                        txt += $"\nCurrently paused, with {Func.convertSeconds((ulong)timer.RemainingTime)} remaining.";
                    else
                        txt += $"\nCurrently active, to end <t:{timer.TimeToFire}:R>.";
                    txt += "\n\n";
                }
                txt = txt.TrimEnd();
                eb.WithDescription(txt);
            }
            eb.WithTitle($"Timers for {user.Username}");
            await Context.Channel.SendMessageAsync("", false, eb.Build());
        }
        [Command("timerextend")]
        [Alias("extendtimer", "expand")]
        [Summary("Extends the timer by the specified time. Case-insensitive, but doesn't ignore punctuation. \n\nUsage: `m!timerextend 1h 3s \"Timer's name\"`")]
        private async Task timerExtendAsync([Remainder] string inputRaw = "")
        {
            var eb = new EmbedBuilder();
            eb.WithColor(244, 178, 23);
            var inputs = Func.parseQuotes(inputRaw);
            if (inputs.Count != 2)
            {
                eb.WithDescription("Incorrect amount of arguments! The command is used as `m!timerextend 1h 3s \"Timer's name\"`");
                await Context.Channel.SendMessageAsync("", embed: eb.Build());
                return;
            }
            var time = Func.HumanTimeToSeconds(inputs[0]);
            if (time == null)
            {
                eb.WithDescription("Failed to convert the time to seconds. Make sure that the time is in a supported format such as `01:02:03` or `1d 2h 3m 4s`.");
                await Context.Channel.SendMessageAsync("", embed: eb.Build());
                return;
            }
            var index = ClassSetups.guildsDict[Context.Guild.Id].Timers.FindIndex(t => t.Name.Equals(inputs[1], StringComparison.OrdinalIgnoreCase));
            if (index == -1)
            {
                eb.WithDescription("Couldn't find the timer with the specified name");
                await Context.Channel.SendMessageAsync("", embed: eb.Build());
                return;
            }
            var timer = ClassSetups.guildsDict[Context.Guild.Id].Timers[index];
            if (timer.User != Context.User.Id && Context.User.Id != 491998313399189504)
            {
                eb.WithDescription("You can't extend other people's timers!");
                await Context.Channel.SendMessageAsync("", embed: eb.Build());
                return;
            }
            ClassSetups.guildsDict[Context.Guild.Id].Timers[index].Active = false;
            var txt = $"The timer \"{timer.Name}\" has been extended. ";
            if (timer.Paused)
            {
                ClassSetups.guildsDict[Context.Guild.Id].Timers[index].RemainingTime += (int)time;
                txt += $"If unpaused, it will end in {Func.convertSeconds((ulong)ClassSetups.guildsDict[Context.Guild.Id].Timers[index].RemainingTime)}";
            }
            else
            {
                ClassSetups.guildsDict[Context.Guild.Id].Timers[index].TimeToFire += (ulong)time;
                txt += $"It will end <t:{ClassSetups.guildsDict[Context.Guild.Id].Timers[index].TimeToFire}:R>";
            }
            eb.WithColor(51, 127, 213);
            eb.WithDescription(txt);
            await Context.Channel.SendMessageAsync("", false, eb.Build());
            ulong thisTime = Convert.ToUInt64(Context.Message.Timestamp.ToUnixTimeSeconds());
            if (!timer.Paused && (int)(timer.TimeToFire - thisTime) < 60)
            {
                ClassSetups.guildsDict[Context.Guild.Id].Timers[index].Active = true;
                Thread childThread = new Thread(() => Func.MakeReminderAsync(timer, Context.Guild, delay: (int)(timer.TimeToFire - thisTime) + (int)time));
                childThread.Start();
            }
        }
        [Command("timershorten")]
        [Alias("shortentimer", "shorten")]
        [Summary("Shortens the timer by the specified time. Case-insensitive, but doesn't ignore punctuation. \n\nUsage: `m!timershorten 1h 3s \"Timer's name\"`")]
        private async Task timerShortenAsync([Remainder] string inputRaw = "")
        {
            var eb = new EmbedBuilder();
            eb.WithColor(244, 178, 23);
            var inputs = Func.parseQuotes(inputRaw);
            if (inputs.Count != 2)
            {
                eb.WithDescription("Incorrect amount of arguments! The command is used as `m!timerextend 1h 3s \"Timer's name\"`");
                await Context.Channel.SendMessageAsync("", embed: eb.Build());
                return;
            }
            var time = Func.HumanTimeToSeconds(inputs[0]);
            if (time == null)
            {
                eb.WithDescription("Failed to convert the time to seconds. Make sure that the time is in a supported format such as `01:02:03` or `1d 2h 3m 4s`.");
                await Context.Channel.SendMessageAsync("", embed: eb.Build());
                return;
            }
            var index = ClassSetups.guildsDict[Context.Guild.Id].Timers.FindIndex(t => t.Name.Equals(inputs[1], StringComparison.OrdinalIgnoreCase));
            if (index == -1)
            {
                eb.WithDescription("Couldn't find the timer with the specified name");
                await Context.Channel.SendMessageAsync("", embed: eb.Build());
                return;
            }
            var timer = ClassSetups.guildsDict[Context.Guild.Id].Timers[index];
            if (timer.User != Context.User.Id && Context.User.Id != 491998313399189504)
            {
                eb.WithDescription("You can't shorten other people's timers!");
                await Context.Channel.SendMessageAsync("", embed: eb.Build());
                return;
            }
            ulong thisTime = Convert.ToUInt64(Context.Message.Timestamp.ToUnixTimeSeconds());
            //Console.WriteLine($"Time to fire: {timer.TimeToFire}; This time: {thisTime}; Time to change: {time}");
            if (timer.Paused && timer.RemainingTime < time || !timer.Paused && timer.TimeToFire < thisTime + (ulong)time)
            {
                eb.WithDescription("You can't shorten the timer by more than what's left on it!");
                await Context.Channel.SendMessageAsync("", embed: eb.Build());
                return;
            }
            ClassSetups.guildsDict[Context.Guild.Id].Timers[index].Active = false;
            var txt = $"The timer \"{timer.Name}\" has been shortened. ";
            if (timer.Paused)
            {
                ClassSetups.guildsDict[Context.Guild.Id].Timers[index].RemainingTime -= (int)time;
                txt += $"If unpaused, it will end in {Func.convertSeconds((ulong)ClassSetups.guildsDict[Context.Guild.Id].Timers[index].RemainingTime)}";
            }
            else
            {
                ClassSetups.guildsDict[Context.Guild.Id].Timers[index].TimeToFire -= (ulong)time;
                txt += $"It will end <t:{ClassSetups.guildsDict[Context.Guild.Id].Timers[index].TimeToFire}:R>";
            }
            eb.WithColor(51, 127, 213);
            eb.WithDescription(txt);
            await Context.Channel.SendMessageAsync("", false, eb.Build());
            //Console.WriteLine($"Time to fire: {timer.TimeToFire}; This time: {thisTime}; Time to change: {time}");
            if (!timer.Paused && (int)(timer.TimeToFire - thisTime) < 60)
            {
                ClassSetups.guildsDict[Context.Guild.Id].Timers[index].Active = true;
                Thread childThread = new Thread(() => Func.MakeReminderAsync(timer, Context.Guild, delay: (int)(timer.TimeToFire - thisTime) - (int)time));
                childThread.Start();
            }
        }
    }
}