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
        public Message Message { get; set; }
        public FileBase[] Files { get; private set; }
        /// <summary>
        /// Document file name, null if no document was added to message.
        /// </summary>
        public string? Filename { get; private set; }

        public BotCommandModel()
        {

        }

        public static BotCommandModel FromMessage(Message message, string customCommandMarker = null)
        {
            if (message == null || (message.Text == null && message.Caption == null))
                return null;

            string commandMarker = "/";

            string text = message.Type == Telegram.Bot.Types.Enums.MessageType.Text ? message.Text : message.Caption;

            if (!text.StartsWith("/"))
            {
                if (customCommandMarker == null && !text.StartsWith(customCommandMarker))
                    return null;

                commandMarker = customCommandMarker;
            }

            string command = text.Substring(commandMarker.Length).Split(' ')[0].ToLower();
                        
            string rawArgs = text.Substring(command.Length + commandMarker.Length);
            rawArgs = rawArgs.Trim();
            string[] args = ParseArgs(rawArgs);

            if (command.Contains('@'))
            {
                int index = command.IndexOf('@');
                command = command.Substring(0, index);
            }

            List<FileBase> files = new List<FileBase>();
            string filename = null;

            if (message.ReplyToMessage != null)
            {
                if (message.ReplyToMessage.Photo != null)
                {
                    files.AddRange(message.ReplyToMessage.Photo);
                }
                if (message.ReplyToMessage.Document != null)
                {
                    files.Add(message.ReplyToMessage.Document);
                    filename = message.ReplyToMessage.Document.FileName;
                }
            }
            if (message.Document != null)
            {
                files.Clear();
                files.Add(message.Document);
                filename = message.Document.FileName;
            }
            if (message.Photo != null)
            {
                files.Clear();
                files.AddRange(message.Photo);
            }
            
            var botCommandModel = new BotCommandModel
            {
                Command = command,
                Args = args,
                RawArgs = rawArgs,
                Message = message, 
                Files = files.ToArray(),
                Filename = filename
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
