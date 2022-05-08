using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types;

namespace TelegramRAT
{
    class BotCommandModel
    {
        public string Command { get; set; }
        public string[] Args { get; set; }
        public string RawArgs { get; set; }
        public Message Message { get; private set; }

        public static BotCommandModel FromMessage(Message message)
        {
            if (message == null || (message.Text == null && message.Caption == null))
                return null;

            string text = message.Type == Telegram.Bot.Types.Enums.MessageType.Text ? message.Text : message.Caption;

            if (!text.StartsWith('/'))
                return null;

            string command = text.Split(' ')[0];
            
            
            string rawArgs = text.Substring(command.Length);
            rawArgs = rawArgs.Trim();
            string[] args = ParseArgs(rawArgs);

            if (command.Contains('@'))
            {
                int index = command.IndexOf('@');
                command = command.Substring(0, index);
            }

            var botCommandModel = new BotCommandModel
            {
                Command = command,
                Args = args,
                RawArgs = rawArgs,
                Message = message
            };
            return botCommandModel;
        }

        static string[] ParseArgs(string rawArgs)
        {
            List<string> finished = new List<string>();


            bool insideQuotes = false;

            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < rawArgs.Length; i++)
            {
                switch (rawArgs[i])
                {
                    case '"':
                        if (!insideQuotes)
                        {
                            insideQuotes = true;
                        }
                        else if (insideQuotes)
                        {
                            insideQuotes = false;
                            if (sb.Length > 0) finished.Add(sb.ToString());
                            sb.Clear();
                        }
                        break;

                    case ' ':
                        if (insideQuotes)
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
                        sb.Append(rawArgs[i]);
                        break;
                }
            }
            if (sb.Length > 0) 
                finished.Add(sb.ToString());
            sb.Clear();

            return finished.ToArray();

        }

    }
}
