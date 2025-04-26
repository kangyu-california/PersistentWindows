using System;
using System.Runtime.InteropServices;

namespace PersistentWindows.Common.WinApiBridge
{
    /*
    [StructLayout(LayoutKind.Sequential)]
    public struct WindowsPosition
    {
        public IntPtr hwnd;
        public IntPtr hwndInsertAfter;
        public int Left;
        public int Top;
        public int Width;
        public int Height;
        public int Flags;
    }
    */

    // workaround LiteDB compatibility issue in RECT data structure
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
        public POINT(int x, int y)
        {
            X = x;
            Y = y;
        }
        public override string ToString()
        {
            return string.Format($"({X}, {Y})");
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left { get; set; }
        public int Top { get; set; }
        public int Right { get; set; }
        public int Bottom { get; set; }

        public int Height
        {
            get
            {
                return Bottom - Top;
            }
        }
        public int Width
        {
            get
            {
                return Right - Left;
            }
        }

        public override string ToString()
        {
            return string.Format("({0}, {1}), {2} x {3}", Left, Top, Width, Height);
        }

        public int Diff(RECT r)
        {
            return Math.Abs(Left - r.Left) + Math.Abs(Right - r.Right) + Math.Abs(Top - r.Top) + Math.Abs(Bottom - r.Bottom);
        }
    }
}
