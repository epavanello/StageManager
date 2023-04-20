using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
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
		static bool mouseDown = false;
		static bool mouseDrag = false;
		static void Main(string[] args)
		{
			MouseHook.Start();
			windows = new List<Window>();
			activeHandle = GetForegroundWindow();
			updateActiveWindows();

			MouseHook.MouseAction += async (object sender, MouseHook.MouseMessages e) =>
			{
				if (e == MouseHook.MouseMessages.WM_LBUTTONDOWN)
				{
					mouseDown = true;
				}
				else if (mouseDown && e == MouseHook.MouseMessages.WM_MOUSEMOVE)
				{
					mouseDrag = true;
				}
				else if (e == MouseHook.MouseMessages.WM_LBUTTONUP)
				{
					if (mouseDown && mouseDrag)
					{
						mouseDown = false;
						mouseDrag = false;

						await Task.Delay(50);
						updatePositions();
					}
					else
					{
						mouseDown = false;
						mouseDrag = false;
					}
				}
			};


			updatePositions();




			dele = new WinEventDelegate(WinEventProc);
			IntPtr m_hhook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, dele, 0, 0, WINEVENT_OUTOFCONTEXT);
			Application.Run(); //<----

			MouseHook.stop();
		}

		private static void updateActiveWindows()
		{
			var newWindows = OpenWindowGetter.GetOpenWindows();
			newWindows.Sort(delegate (Window x, Window y)
			{
				return findWindowIndexByHandle(x.handle) - findWindowIndexByHandle(y.handle);
			});

			windows = newWindows;
		}
		static bool IsFullscreen(IntPtr wndHandle, Screen screen)
		{
			RECT r = new RECT();
			GetWindowRect(wndHandle, ref r);
			return new Rectangle(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top)
								  .Contains(screen.Bounds);
		}


		static void updatePositions()
		{
			if (mouseDown && mouseDrag)
			{
				Console.WriteLine("Skipped: Mouse down or drag");
				return;
			}

			updateActiveWindows();

			foreach (var window in windows)
			{
				if (IsFullscreen(window.handle, Screen.PrimaryScreen))
				{
					return;
				}
			}

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
			Dictionary<Window, Point> newPositions = new Dictionary<Window, Point>();
			for (int i = 0; i < windows.Count; i++)
			{
				var window = windows[i];
				newPositions[window] = getWindowNewPoint(window, windows.Count + (windows.Count % 2 == 0 ? 1 : 0), i + 1);
			}

			Console.Clear();
			foreach (var window in windows)
			{
				Console.WriteLine(window.program);

			}

			startBatchedWindowAnimations(0.4f, newPositions);
		}

		static Nullable<Window> findWindowByHandle(HWND handle)
		{

			foreach (var window in windows)
			{
				if (window.handle == handle)
				{
					return window;
				}
			}
			return null;
		}

		static int findWindowIndexByHandle(HWND handle)
		{
			for (int i = 0; i < windows.Count; i++)
			{
				var window = windows[i];
				if (window.handle == handle)
				{
					return i;
				}
			}
			return -1;
		}

		static bool animationsEnabled = true;
		static Thread animationThread = null;
		static void startBatchedWindowAnimations(double seconds, Dictionary<Window, Point> newPositions)
		{
			if (animationThread != null)
			{
				animationThread.Abort();
			}

			animationThread = new Thread(() =>
			{
				Console.WriteLine("Animation started");
				if (!animationsEnabled)
				{
					foreach (var value in newPositions)
					{
						var window = value.Key;
						var newX = value.Value.X;
						var newY = value.Value.Y;
						SetWindowPos(window.handle, 0,
							(int)newX,
							(int)newY,
						0, 0, SWP_NOZORDER | SWP_NOSIZE | SWP_SHOWWINDOW
						);
					}
				}
				else
				{
					double FPS = 60;
					var ease = new PowerEase();
					ease.EasingMode = EasingMode.EaseIn;
					for (int i = 0; i <= FPS * seconds; i++)
					{
						double actualTime = 1 / (FPS * seconds) * i;
						foreach (var value in newPositions)
						{
							var window = value.Key;
							var newX = value.Value.X;
							var newY = value.Value.Y;
							int originalX = window.rect.Left;
							int originalY = window.rect.Top;
							SetWindowPos(window.handle, 0,
								(int)(originalX + (newX - originalX) * ease.Ease(actualTime)),
								(int)(originalY + (newY - originalY) * ease.Ease(actualTime)),
							0, 0, SWP_NOZORDER | SWP_NOSIZE | SWP_SHOWWINDOW
							);
						}
						Thread.Sleep((int)(1000 / FPS));
					}
				}
				Console.WriteLine("Animation ended");

			});
			animationThread.Start();
		}

		static Point getWindowNewPoint(Window window, int sections, int position)
		{
			int sectionWidth = Screen.PrimaryScreen.WorkingArea.Width / sections;
			int horizzontalScreenPosition = sectionWidth / 2 + sectionWidth * (position - 1);
			return new Point(
				horizzontalScreenPosition - window.rect.Width() / 2,
				Screen.PrimaryScreen.WorkingArea.Height / 2 - window.rect.Height() / 2);
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

		[DllImport("user32.dll")]
		private static extern bool GetWindowRect(IntPtr hWnd, [In, Out] ref RECT rect);


	}
}
