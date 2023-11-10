using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Media.Animation;

namespace StageManager
{
	using HWND = IntPtr;
	class Program
	{

		static List<Window> windows;
		static HWND activeHandle;
		static List<HWND> skipHandles;
		static bool mouseDown = false;
		static bool mouseDrag = false;
		static void Main()
		{
			windows = new List<Window>();
			skipHandles = new List<HWND>();
			activeHandle = GetForegroundWindow();
			UpdateActiveWindows();

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
					bool changedActiveWindow = false;
					// verify if new foreground window is different from the old one, then update positions
					var newActiveHandle = GetForegroundWindow();
					changedActiveWindow = newActiveHandle != activeHandle;

					if (mouseDown && mouseDrag || changedActiveWindow)
					{
						mouseDown = false;
						mouseDrag = false;

						if (changedActiveWindow)
						{
							activeHandle = newActiveHandle;
						}
						await Task.Delay(10);
						UpdatePositions();
					}
					else
					{
						mouseDown = false;
						mouseDrag = false;
					}
				}
			};
			MouseHook.Start();

			Console.WriteLine("Ctrl + Shift + S to ignore/add window");

			KeyboardHook.KeyPressed += (object sender, KeyPressedArgs e) =>
			{
				if (e.KeyCode == Keys.S && (Control.ModifierKeys & Keys.Shift) == Keys.Shift && (Control.ModifierKeys & Keys.Control) == Keys.Control)
				{
					if (skipHandles.Contains(activeHandle))
					{
						skipHandles.Remove(activeHandle);
						Console.WriteLine("Added");
					}
					else
					{
						skipHandles.Add(activeHandle);
						Console.WriteLine("Ignored");
					}
					UpdatePositions();
				}
			};
			KeyboardHook.Start();

			UpdatePositions();



			// Handle window switching
			dele = new WinEventDelegate(WinEventProc);
			HWND m_hhook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, HWND.Zero, dele, 0, 0, WINEVENT_OUTOFCONTEXT);
			Application.Run(); //<----

			MouseHook.Stop();
		}

		private static void UpdateActiveWindows()
		{
			var newWindows = OpenWindowGetter.GetOpenWindows();
			newWindows.Sort(delegate (Window x, Window y)
			{
				return FindWindowIndexByHandle(x.handle) - FindWindowIndexByHandle(y.handle);
			});

			windows = newWindows;
		}
		static bool IsFullscreen(HWND wndHandle, Screen screen)
		{
			RECT r = new RECT();
			GetWindowRect(wndHandle, ref r);
			return new Rectangle(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top)
								  .Contains(screen.Bounds);
		}


		static void UpdatePositions()
		{
			if (mouseDown && mouseDrag)
			{
				return;
			}

			UpdateActiveWindows();

			foreach (var window in windows)
			{
				if (IsFullscreen(window.handle, Screen.PrimaryScreen))
				{
					return;
				}
			}

			var notSkippedWindows = new List<Window>();
			foreach (var window in windows)
			{
				if (!skipHandles.Contains(window.handle))
				{
					notSkippedWindows.Add(window);
				}
			}

			int centerNewIndex = notSkippedWindows.Count / 2;
			for (int i = 0; i < notSkippedWindows.Count; i++)
			{
				var window = notSkippedWindows[i];
				if (window.handle == activeHandle)
				{
					notSkippedWindows.RemoveAt(i);
					notSkippedWindows.Insert(centerNewIndex, window);
				}
			}
			Dictionary<Window, Point> newPositions = new Dictionary<Window, Point>();
			for (int i = 0; i < notSkippedWindows.Count; i++)
			{
				var window = notSkippedWindows[i];
				if (skipHandles.Contains(window.handle))
				{
					continue;
				}
				newPositions[window] = GetWindowNewPoint(window, notSkippedWindows.Count + (notSkippedWindows.Count % 2 == 0 ? 1 : 0), i + 1);
			}

			StartBatchedWindowAnimations(0.4f, newPositions);
		}

		static int FindWindowIndexByHandle(HWND handle)
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
		static Task animationTask = null;
		static CancellationTokenSource cancellationTokenSource = null;
		static async void StartBatchedWindowAnimations(double seconds, Dictionary<Window, Point> newPositions)
		{
			cancellationTokenSource?.Cancel();
			cancellationTokenSource = new CancellationTokenSource();
			var cancellationToken = cancellationTokenSource.Token;

			if (animationTask != null)
			{
				try
				{
					await animationTask;
				}
				catch (TaskCanceledException)
				{
					// Ignora l'eccezione se l'attività è stata annullata
				}
			}

			animationTask = Task.Run(() =>
			{
				if (!animationsEnabled)
				{
					foreach (var value in newPositions)
					{
						var window = value.Key;
						var newX = value.Value.X;
						var newY = value.Value.Y;
						SetWindowPos(window.handle, 0,
							newX,
							newY,
						0, 0, SWP_NOZORDER | SWP_NOSIZE | SWP_SHOWWINDOW
						);
					}
				}
				else
				{
					double FPS = 60;
					var ease = new PowerEase
					{
						EasingMode = EasingMode.EaseIn
					};
					for (int i = 0; i <= FPS * seconds; i++)
					{
						if (cancellationToken.IsCancellationRequested)
						{
							return;
						}

						double actualTime = 1 / (FPS * seconds) * i;
						foreach (var value in newPositions)
						{
							var window = value.Key;
							var newX = value.Value.X;
							var newY = value.Value.Y;
							int originalX = window.Rect.Left;
							int originalY = window.Rect.Top;
							SetWindowPos(window.handle, 0,
								(int)(originalX + (newX - originalX) * ease.Ease(actualTime)),
								(int)(originalY + (newY - originalY) * ease.Ease(actualTime)),
							0, 0, SWP_NOZORDER | SWP_NOSIZE | SWP_SHOWWINDOW
							);
						}
						Thread.Sleep((int)(1000 / FPS));
					}
				}

			}, cancellationToken);
		}


		static Point GetWindowNewPoint(Window window, int sections, int position)
		{
			int sectionWidth = Screen.PrimaryScreen.WorkingArea.Width / sections;
			int horizzontalScreenPosition = sectionWidth / 2 + sectionWidth * (position - 1);
			return new Point(
				horizzontalScreenPosition - window.Rect.Width() / 2,
				Screen.PrimaryScreen.WorkingArea.Height / 2 - window.Rect.Height() / 2 + 300);
		}

		static WinEventDelegate dele = null; //STATIC
		delegate void WinEventDelegate(HWND hWinEventHook, uint eventType, HWND hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);
		public static void WinEventProc(HWND hWinEventHook, uint eventType, HWND hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime) //STATIC
		{
			var newActiveHandle = GetForegroundWindow();
			if (newActiveHandle != activeHandle)
			{
				activeHandle = newActiveHandle;
				UpdatePositions();
			}
		}

		[DllImport("user32.dll")]
		static extern HWND SetWinEventHook(uint eventMin, uint eventMax, HWND hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

		private const uint WINEVENT_OUTOFCONTEXT = 0;
		private const uint EVENT_SYSTEM_FOREGROUND = 3;
		const short SWP_NOSIZE = 1;
		const short SWP_NOZORDER = 0X4;
		const int SWP_SHOWWINDOW = 0x0040;

		[DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
		private static extern HWND GetForegroundWindow();

		[DllImport("user32.dll", EntryPoint = "SetWindowPos")]
		public static extern HWND SetWindowPos(HWND hWnd, int hWndInsertAfter, int x, int Y, int cx, int cy, int wFlags);

		[DllImport("user32.dll")]
		private static extern bool GetWindowRect(HWND hWnd, [In, Out] ref RECT rect);


	}
}
