using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace TelegramRAT
{
    class BotCommand
    {
        public string Command { get; set; }

        public string? Description { get; set; } = null;
        public string? Example { get; set; } = null;

        public string[]? Groups { get; set; } = null;

        public Action<BotCommandModel, Update> Execute { get; set; }

        public MessageType MsgType { get; set; } = MessageType.Text;
        public int CountArgs = 0;
        public bool IgnoreCountArgs = false;
        public bool MayHaveNoArgs = false;

        public static BotCommandModel Parse(string text)
        {
            if (text.StartsWith("/"))
            {
                var splits = text.Split(' ');
                var name = splits?.FirstOrDefault().ToLower();
                var args = splits.Skip(1).Take(splits.Count()).ToArray();
                List<string> finished = new List<string>();

                string fullargs = string.Join(" ", args);
                bool concating = false;

                StringBuilder sb = new StringBuilder();


                for (int i = 0; i < string.Join(" ", args).Length; i++)
                {
                    switch (fullargs[i])
                    {
                        case '"':
                            if (!concating)
                            {
                                concating = true;

                            }
                            else if (concating)
                            {
                                concating = false;
                                if (sb.Length > 0) finished.Add(sb.ToString());
                                sb.Clear();
                            }
                            break;

                        case ' ':
                            if (concating)
                            {
                                sb.Append(' ');
                            }
                            else
                            {
                                if (sb.Length > 0) finished.Add(sb.ToString());
                                sb.Clear();
                            }
                            break;
                        default:
                            sb.Append(fullargs[i]);
                            break;
                    }
                }
                if (sb.Length > 0) finished.Add(sb.ToString());
                sb.Clear();

                return new BotCommandModel
                {
                    Command = name,
                    Args = finished.ToArray(),
                    RawArgs = (args.Length != 0) ? text.Substring(name.Length + 1) : null
                };
            }
            return null;
        }
    }
}
