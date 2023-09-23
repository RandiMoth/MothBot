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
        private enum f_states { NORMAL, VARIABLE, FUNCTION, ESCAPE_CHAR }
        public static string GetLoc(string key, Language language = Language.L_ENGLISH, SocketGuildUser? user = null, string Context1 = "")
        {
            switch (language)
            { 
                case Language.L_ENGLISH:
                    return parseText(Info.englishDict[key], user, Context1);
                default:
                    return "UNKNOWN LANGUAGE, SHOULD NEVER HAPPEN";
            }
        }
        private static string parseText(string str, SocketGuildUser? user = null, string Context1 = "")
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
                        result += getVar(c, user, Context1);
                        state = f_states.NORMAL;
                        break;
                    case f_states.FUNCTION:
                        switch (c)
                        {
                            case '$':
                                state = f_states.NORMAL;
                                result += parseTextFunction(args, user, Context1);
                                break;
                            case '|':
                                args.Add("");
                                i += 1;
                                break;
                            default:
                                args[i] += c;
                                break;
                        }
                        break;
                    case f_states.ESCAPE_CHAR:
                        state = f_states.NORMAL;
                        result += c;
                        break;
                }
            }
            return result;
        }
        private static string getVar(char c, SocketGuildUser? user = null, string Context1 = "")
        {
            string result;
            switch (c)
            {
                case 'm':
                    
                    result = Info.usersDict[user.Id].MothAmount.ToString();
                    break;
                case 'n':
                    result = user.Nickname ?? user.DisplayName;
                    break;
                case '1':
                    result = Context1;
                    break;
                case 'f':
                    result = user.Username;
                    if (user.DiscriminatorValue != 0)
                        result += $"#{user.Discriminator}";
                    break;
                default:
                    result = "ERROR: invalid variable %" + c;
                    break;
            }
            return result;
        }
        private static string parseTextFunction(List<string> input, SocketGuildUser? user = null, string Context1 = "")
        {
            switch (input[0])
            {
                case "PLURAL":
                    long check = Convert.ToInt64(parseText(input[1], user));
                    if (check == 1 || check == -1)
                        return parseText(input[2], user);
                    return parseText(input[3], user);
                case "MENTION":
                    switch (input[1])
                    {
                        case "ROLE":
                            return $"<@&{parseText(input[2], user, Context1)}>";
                        case "USER":
                            return $"<@{parseText(input[2], user, Context1)}>";
                        default:
                            return "ERROR: invalid mention type: " + input[1];
                    }
                default:
                    return "ERROR: invalid function $" + input[0] + "$";
            }
        }
    }
}
