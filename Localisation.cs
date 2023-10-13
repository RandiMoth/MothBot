using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MothBot
{
    public class Localisation
    {
        private enum f_states { NORMAL, VARIABLE, FUNCTION, ESCAPE_CHAR, ESCAPE_CHAR_FUNC }
        private class TextContext
        {
            public SocketGuildUser? User { get; set; } = null;
            public List<string> Strings { get; set; } = new List<string>();
        }
        public static string GetLoc(string key, Language language = Language.L_ENGLISH, SocketGuildUser? user = null, string Context1 = "")
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
            TextContext Context = new TextContext() { User = user, Strings = new List<string>() { Context1 } }; 
            return parseText(value, Context);
        }
        private static string parseText(string str, TextContext Context)
        {
            //Console.WriteLine(str);
            f_states state = f_states.NORMAL;
            var result = "";
            List<String> args = new List<string>();
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
                                break;
                            case '/':
                                state = f_states.VARIABLE;
                                args = new List<string> { "" };
                                i = 0;
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
                        result += getVar(c, Context);
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
                                result += parseTextFunction(args, Context);
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
        private static string getVar(char c, TextContext Context)
        {
            string result;
            switch (c)
            {
                case 'm':
                    result = Info.usersDict[Context.User.Id].MothAmount.ToString();
                    break;
                case 'n':
                    result = Context.User.Nickname ?? Context.User.DisplayName;
                    break;
                case '1':
                    result = Context.Strings[0];
                    break;
                case 'f':
                    result = Context.User.Username;
                    if (Context.User.DiscriminatorValue != 0)
                        result += $"#{Context.User.Discriminator}";
                    break;
                default:
                    result = "ERROR: invalid variable %" + c;
                    break;
            }
            return result;
        }
        private static string parseTextFunction(List<string> input, TextContext Context)
        {
            input.ForEach(s => s = parseText(s, Context));
            switch (input[0])
            {
                case "PLURAL":
                    long check = Convert.ToInt64(parseText(input[1], Context));
                    if (check == 1 || check == -1)
                        return parseText(input[2], Context);
                    return parseText(input[3], Context);
                case "MENTION":
                    switch (input[1])
                    {
                        case "ROLE":
                            return $"<@&{parseText(input[2], Context)}>";
                        case "USER":
                            return $"<@{parseText(input[2], Context)}>";
                        default:
                            return "ERROR: invalid mention type: " + input[1];
                    }
                default:
                    return "ERROR: invalid function $" + input[0] + "$";
            }
        }
    }
}
