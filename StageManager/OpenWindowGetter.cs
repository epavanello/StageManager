using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StageManager
{
    using System.Runtime.InteropServices;
    using HWND = System.IntPtr;

    /// <summary>Contains functionality to get all the open windows.</summary>
    /// 


    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;        // x position of upper-left corner
        public int Top;         // y position of upper-left corner
        public int Right;       // x position of lower-right corner
        public int Bottom;      // y position of lower-right corner

        public int Width()
		{
            return Right - Left;
		}
        public int Height()
		{
            return Bottom - Top;
		}
    }

    public struct Window {
        public HWND handle;
        public RECT rect;
        public string title;

        public Window(HWND handle,
        RECT rect,
        string title)
		{
            this.handle = handle;
            this.rect = rect;
            this.title = title;
		}
    }

    public static class OpenWindowGetter
    {
        /// <summary>Returns a dictionary that contains the handle and title of all the open windows.</summary>
        /// <returns>A dictionary that contains the handle and title of all the open windows.</returns>
        public static List<Window> GetOpenWindows(object context)
        {
            HWND shellWindow = GetShellWindow();
            List<Window> windows = new List<Window>();

            EnumWindows(delegate (HWND hWnd, int lParam)
            {
                if (hWnd == shellWindow) return true;
                if (!IsWindowVisible(hWnd) || IsIconic(hWnd)) return true;

                if (HasSomeExtendedWindowsStyles(hWnd))
                    return true;
                DwmGetWindowAttribute(hWnd, (int)DwmWindowAttribute.DWMWA_CLOAKED, out bool isCloacked, Marshal.SizeOf(typeof(bool)));

                if(isCloacked)
				{
                    return true;
				}

                int length = GetWindowTextLength(hWnd);
                if (length == 0) return true;

                StringBuilder builder = new StringBuilder(length);
                GetWindowText(hWnd, builder, length + 1);
                RECT rect;
                GetWindowRect(new HandleRef(context, hWnd), out rect);
                windows.Add(new Window(hWnd, rect, builder.ToString()));

                return true;

            }, 0);

            return windows;
        }

        static bool HasSomeExtendedWindowsStyles(IntPtr hwnd)
        {
            const int GWL_EXSTYLE = -20;
            const uint WS_EX_TOOLWINDOW = 0x00000080U;

            uint i = GetWindowLong(hwnd, GWL_EXSTYLE);
            if ((i & (WS_EX_TOOLWINDOW)) != 0)
            {
                return true;
            }

            return false;
        }
        [Flags]
        public enum DwmWindowAttribute : uint
        {
            DWMWA_NCRENDERING_ENABLED = 1,
            DWMWA_NCRENDERING_POLICY,
            DWMWA_TRANSITIONS_FORCEDISABLED,
            DWMWA_ALLOW_NCPAINT,
            DWMWA_CAPTION_BUTTON_BOUNDS,
            DWMWA_NONCLIENT_RTL_LAYOUT,
            DWMWA_FORCE_ICONIC_REPRESENTATION,
            DWMWA_FLIP3D_POLICY,
            DWMWA_EXTENDED_FRAME_BOUNDS,
            DWMWA_HAS_ICONIC_BITMAP,
            DWMWA_DISALLOW_PEEK,
            DWMWA_EXCLUDED_FROM_PEEK,
            DWMWA_CLOAK,
            DWMWA_CLOAKED,
            DWMWA_FREEZE_REPRESENTATION,
            DWMWA_LAST
        }

        private delegate bool EnumWindowsProc(HWND hWnd, int lParam);
        [DllImport("dwmapi.dll")]
        static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out bool pvAttribute, int cbAttribute);

        [DllImport("user32.dll", SetLastError = true)]
        static extern System.UInt32 GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("USER32.DLL")]
        private static extern bool EnumWindows(EnumWindowsProc enumFunc, int lParam);

        [DllImport("USER32.DLL")]
        private static extern int GetWindowText(HWND hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("USER32.DLL")]
        private static extern int GetWindowTextLength(HWND hWnd);

        [DllImport("USER32.DLL")]
        private static extern bool IsWindowVisible(HWND hWnd);

        [DllImport("USER32.DLL")]
        private static extern bool IsIconic(HWND hWnd);

        [DllImport("USER32.DLL")]
        private static extern IntPtr GetShellWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetWindowRect(HandleRef hWnd, out RECT lpRect);

    }
}
