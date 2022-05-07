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

        public string Description { get; set; } = null;
        public string Example { get; set; } = null;

        public string[] Groups { get; set; } = null;

        public Action<BotCommandModel> Execute { get; set; }

        public MessageType MsgType { get; set; } = MessageType.Text;
        public int CountArgs = 0;
        public bool IgnoreCountArgs = false;
        public bool MayHaveNoArgs = false;

    }
}
