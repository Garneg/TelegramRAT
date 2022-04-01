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

                return new BotCommandModel
                {
                    Command = name,
                    Args = args,
                    RawArgs = (args.Length != 0) ? text.Substring(name.Length + 1) : null
                };
            }
            return null;
        }
    }
}
