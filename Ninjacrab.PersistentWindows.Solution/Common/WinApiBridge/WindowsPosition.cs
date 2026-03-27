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

        public override bool Equals(object obj)
        {
            if (!(obj is POINT)) return false;
            POINT other = (POINT)obj;
            return X == other.X && Y == other.Y;
        }

        public override int GetHashCode()
        {
            unchecked { return (X * 397) ^ Y; }
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
            int diff = Math.Abs(Left - r.Left) + Math.Abs(Right - r.Right) + Math.Abs(Top - r.Top) + Math.Abs(Bottom - r.Bottom);
            return diff / 4;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is RECT)) return false;
            RECT other = (RECT)obj;
            return Left == other.Left && Top == other.Top && Right == other.Right && Bottom == other.Bottom;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Left;
                hash = (hash * 397) ^ Top;
                hash = (hash * 397) ^ Right;
                hash = (hash * 397) ^ Bottom;
                return hash;
            }
        }
    }
}
