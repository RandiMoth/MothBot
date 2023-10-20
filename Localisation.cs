using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MothBot
{
    public class Localisation
    {
        private enum f_states { NORMAL, VARIABLE, FUNCTION, ESCAPE_CHAR, ESCAPE_CHAR_FUNC }
        private static List<string> falseStrings = new List<string>() { "", "false", "0", "no" };
        private class TextContext
        {
            public SocketGuildUser? User { get; set; } = null;
            public List<string> Strings { get; set; } = new List<string>();
            public DateTimeOffset? Time { get; set; } = null;
            public ulong Number { get; set; } = 0;
            public ulong Number2 { get; set; } = 0;
            public bool Bool { get; set; } = false;
            public Language Language { get; set; }
            public string parseText(string str)
            {
                f_states state = f_states.NORMAL;
                var result = "";
                List<string> args = new List<string>();
                short i = 0;
                foreach (char c in str)
                {
                    switch (state)
                    {
                        case f_states.NORMAL:
                            switch (c)
                            {
                                case '$':
                                    state = f_states.FUNCTION;
                                    args = new List<string> { "" };
                                    i = 0;
                                    break;
                                case '/':
                                    state = f_states.VARIABLE;
                                    break;
                                case '\\':
                                    state = f_states.ESCAPE_CHAR;
                                    break;
                                default:
                                    result += c;
                                    break;
                            }
                            break;
                        case f_states.VARIABLE:
                            result += getVar(c);
                            state = f_states.NORMAL;
                            break;
                        case f_states.ESCAPE_CHAR:
                            state = f_states.NORMAL;
                            result += c;
                            break;
                        case f_states.FUNCTION:
                            switch (c)
                            {
                                case '$':
                                    state = f_states.NORMAL;
                                    result += parseTextFunction(args);
                                    break;
                                case '|':
                                    args.Add("");
                                    i += 1;
                                    break;
                                case '\\':
                                    state = f_states.ESCAPE_CHAR_FUNC;
                                    break;
                                default:
                                    args[i] += c;
                                    break;
                            }
                            break;
                        case f_states.ESCAPE_CHAR_FUNC:
                            state = f_states.FUNCTION;
                            result += c;
                            break;
                    }
                }
                return result;
            }
            private string getVar(char c)
            {
                string result;
                switch (c)
                {
                    case 'm':
                        //Console.WriteLine(Info.usersDict[User.Id].MothAmount.ToString());
                        result = Info.usersDict[User.Id].MothAmount.ToString();
                        break;
                    case 'n':
                        result = User.Nickname ?? User.DisplayName;
                        break;
                    case '1':
                        result = Strings[0];
                        break;
                    case 'f':
                        result = User.Username;
                        if (User.DiscriminatorValue != 0)
                            result += $"#{User.Discriminator}";
                        break;
                    case 'd':
                        result = TimestampToHumanFormat(Time);
                        break;
                    case 't':
                        result = convertSeconds(Number);
                        break;
                    case 'b':
                        if (Bool)
                            result = "true";
                        else
                            result = "false";
                        break;
                    case 'i':
                        result = Number.ToString();
                        break;
                    case 'r':
                        result = "\n";
                        break;
                    default:
                        result = "ERROR: invalid variable %" + c;
                        break;
                }
                return result;
            }
            private string parseTextFunction(List<string> input)
            {
                input = input.Select(s => s = parseText(s)).ToList();
                Console.WriteLine($"Parsing function {input[0]} with arguments: {string.Join(" | ", input.Skip(1))}");
                switch (input[0])
                {
                    case "PLURAL":
                        long check = Convert.ToInt64(input[1]);
                        if (check == 1 || check == -1)
                        {
                            if (input.Count > 2)
                                return input[3];
                            return "";
                        }
                        return input[2];
                    case "PLURALSECS":
                        ulong checksecs = Number;
                        if (checksecs % 60 == 1 || checksecs % 60 == 0 && checksecs / 60 % 3600 == 1 || checksecs % 60 == 0 && checksecs / 60 % 60 == 0 && checksecs / 3600 % 60 == 1)
                        {
                            if (input.Count > 1)
                                return input[2];
                            return "";
                        }
                        return input[1];
                    case "MENTION":
                        switch (input[1])
                        {
                            case "ROLE":
                                return $"<@&{input[2]}>";
                            case "USER":
                                if (input.Count == 2)
                                    return User.Mention;
                                return $"<@{input[2]}>";
                            default:
                                return "ERROR: invalid mention type: " + input[1];
                        }
                    case "TIME":
                        ulong secs = Convert.ToUInt64(input[1]);
                        return convertSeconds(secs);
                    case "IF":
                        if (falseStrings.Contains(input[1]))
                            return input[3];
                        return input[2];
                    case "OR":
                        for (int i = 1; i < input.Count; i++)
                            if (falseStrings.Contains(input[i]))
                                return "false";
                        return "true";
                    case "MODULO":
                        return (Convert.ToInt32(input[1]) % Convert.ToInt32(input[2])).ToString();
                    case "FUNC":
                        return GetLoc(input[1], this);
                    default:
                        return "ERROR: invalid function $" + input[0] + "$";
                }
            }

        }
        private static string GetLoc(string key, TextContext context)
        {
            string value;
            switch (context.Language)
            {
                case Language.L_ENGLISH:
                    value = Info.englishDict[key];
                    break;
                default:
                    return "UNKNOWN LANGUAGE, SHOULD NEVER HAPPEN";
            }
            return context.parseText(value);
        }
        public static string GetLoc(string key, Language language = Language.L_ENGLISH, SocketGuildUser? user = null, string Context1 = "", DateTimeOffset? time = null, bool boolean = false, ulong number = 0)
        {
            string value;
            switch (language)
            {
                case Language.L_ENGLISH:
                    value = Info.englishDict[key];
                    break;
                default:
                    return "UNKNOWN LANGUAGE, SHOULD NEVER HAPPEN";
            }
            TextContext Context = new TextContext() { User = user, Strings = new List<string>() { Context1 }, Time = time, Bool = boolean, Number = number, Language = language};
            return Context.parseText(value);
        }
        private static string TimestampToHumanFormat(DateTimeOffset? time)
        {
            if (!time.HasValue)
                return "Time not provided, should never happen!";
            return TimestampToHumanFormat(time.Value);
        }
        private static string TimestampToHumanFormat(DateTimeOffset time)
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
        private static string convertSeconds(ulong secs)
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
                else if (days > 0 && (mins > 0 || secs > 0))
                    res += ", and ";
                else if (mins > 0 || secs > 0)
                    res += " and ";
            }
            if (mins > 0)
            {
                res += $"{mins} minute";
                if (mins != 1)
                    res += "s";
                if (fcount > 1 && secs > 0)
                    res += ", and ";
                else if (secs > 0)
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
    }
}
