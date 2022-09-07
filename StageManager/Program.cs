using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Media.Animation;

namespace StageManager
{
	using HWND = System.IntPtr;
	class Program
	{

		static List<Window> windows;
		static HWND activeHandle;
		static void Main(string[] args)
		{

			activeHandle = GetForegroundWindow();
			updateActiveWindows();

			updatePositions();


			dele = new WinEventDelegate(WinEventProc);
			IntPtr m_hhook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, dele, 0, 0, WINEVENT_OUTOFCONTEXT);
			Application.Run(); //<----
		}

		private static void updateActiveWindows()
		{
			object context = new { };
			windows = OpenWindowGetter.GetOpenWindows(context);


			Console.Clear();
			foreach (var window in windows)
			{
				Console.WriteLine(window.title);

			}
		}

		static void updatePositions()
		{
			updateActiveWindows();

			int centerNewIndex = windows.Count / 2;
			for (int i = 0; i < windows.Count; i++)
			{
				var window = windows[i];
				if (window.handle == activeHandle)
				{
					windows.RemoveAt(i);
					windows.Insert(centerNewIndex, window);
				}
			}
			for (int i = 0; i < windows.Count; i++)
			{
				var window = windows[i];
				positionAppOnFraction(window, windows.Count + (windows.Count % 2 == 0 ? 1 : 0), i + 1);
			}
		}

		static void positionAppOnFraction(Window window, int sections, int position)
		{
			int sectionWidth = Screen.PrimaryScreen.WorkingArea.Width / sections;
			int horizzontalScreenPosition = sectionWidth / 2 + sectionWidth * (position - 1);
			SetWindowPos(window.handle, 0,
				horizzontalScreenPosition - window.rect.Width() / 2,
				Screen.PrimaryScreen.WorkingArea.Height / 2 - window.rect.Height() / 2,
				0, 0, SWP_NOZORDER | SWP_NOSIZE | SWP_SHOWWINDOW
				);
		}

		static void centerApp(Window window)
		{
			SetWindowPos(window.handle, 0,
				Screen.PrimaryScreen.WorkingArea.Width / 2 - window.rect.Width() / 2,
				Screen.PrimaryScreen.WorkingArea.Height / 2 - window.rect.Height() / 2,
				0, 0, SWP_NOZORDER | SWP_NOSIZE | SWP_SHOWWINDOW
				);

		}

		static WinEventDelegate dele = null; //STATIC
		delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);
		public static void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime) //STATIC
		{
			activeHandle = GetForegroundWindow();
			updatePositions();
		}

		[DllImport("user32.dll")]
		static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

		private const uint WINEVENT_OUTOFCONTEXT = 0;
		private const uint EVENT_SYSTEM_FOREGROUND = 3;

		const short SWP_NOMOVE = 0X2;
		const short SWP_NOSIZE = 1;
		const short SWP_NOZORDER = 0X4;
		const int SWP_SHOWWINDOW = 0x0040;

		[DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
		private static extern IntPtr GetForegroundWindow();

		[DllImport("user32.dll", EntryPoint = "SetWindowPos")]
		public static extern IntPtr SetWindowPos(IntPtr hWnd, int hWndInsertAfter, int x, int Y, int cx, int cy, int wFlags);

	}
}
