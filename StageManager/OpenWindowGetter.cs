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

	public struct Window
	{
		public HWND handle;
		private RECT? _rect;
		public RECT Rect
		{
			get { 
				if(_rect == null)
				{
					UpdateRect();
				}
				return _rect.Value;
			}
		}
		public string title;
		public string program;

		void UpdateRect()
		{
			GetWindowRect(new HandleRef(null, handle), out RECT newRect);
			_rect = newRect;
		}

		public Window(HWND handle,
		string title,
		string program)
		{
			this.handle = handle;
			this._rect = null;
			this.title = title;
			this.program = program;
		}

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		static extern bool GetWindowRect(HandleRef hWnd, out RECT lpRect);
	}

	public static class OpenWindowGetter
	{

		public static List<Window> GetOpenWindows()
		{
			HWND shellWindow = GetShellWindow();
			List<Window> windows = new List<Window>();
			List<string> blacklistTitles = new List<string> { "Host popup", "Notifica di Microsoft Teams" };

			EnumWindows(delegate (HWND hWnd, int lParam)
			{
				if (hWnd == shellWindow) return true;
				if (!IsWindowVisible(hWnd) || IsIconic(hWnd)) return true;

				if (HasSomeExtendedWindowsStyles(hWnd))
					return true;
				DwmGetWindowAttribute(hWnd, (int)DwmWindowAttribute.DWMWA_CLOAKED, out bool isCloacked, Marshal.SizeOf(typeof(bool)));

				if (isCloacked)
				{
					return true;
				}

				int length = GetWindowTextLength(hWnd);
				if (length == 0) return true;

				StringBuilder builder = new StringBuilder(length);
				GetWindowText(hWnd, builder, length + 1);
				var title = builder.ToString();
				if(blacklistTitles.Contains(title))
				{
					return true;
				}

				GetWindowThreadProcessId(hWnd, out uint pID);

				IntPtr proc;
				if ((proc = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, false, (int)pID)) == IntPtr.Zero)
					return true;

				int capacity = 2000;
				StringBuilder sb = new StringBuilder(capacity);
				QueryFullProcessImageName(proc, 0, sb, ref capacity);

				windows.Add(new Window(hWnd, title, sb.ToString(0, capacity)));

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
		public const UInt32 PROCESS_QUERY_INFORMATION = 0x400;
		public const UInt32 PROCESS_VM_READ = 0x010;


		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern bool QueryFullProcessImageName([In] IntPtr hProcess, [In] int dwFlags, [Out] StringBuilder lpExeName, ref int lpdwSize);


		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern IntPtr OpenProcess(
			UInt32 dwDesiredAccess,
			[MarshalAs(UnmanagedType.Bool)]
			Boolean bInheritHandle,
			Int32 dwProcessId
		);
		[DllImport("user32.dll", SetLastError = true)]
		public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
		// When you don't want the ProcessId, use this overload and pass IntPtr.Zero for the second parameter
		[DllImport("user32.dll", SetLastError = true)]
		public static extern IntPtr GetWindowThreadProcessId(IntPtr hWnd, IntPtr ProcessId);


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
	}
}
