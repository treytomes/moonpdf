using System;
using System.Windows.Forms;
using MouseKeyboardActivityMonitor.WinApi;

namespace MouseKeyboardActivityMonitor
{
	/// <summary>
	/// This class monitors all mouse activities and provides appropriate events.
	/// </summary>
	public class MouseHookListener : IDisposable
	{
		#region Events

		/// <summary>
		/// Occurs when the mouse pointer is moved.
		/// </summary>
		public event MouseEventHandler MouseMove;

		/// <summary>
		/// Occurs when the mouse pointer is moved.
		/// </summary>
		/// <remarks>
		/// This event provides extended arguments of type <see cref = "MouseEventArgs" /> enabling you to 
		/// supress further processing of mouse movement in other applications.
		/// </remarks>
		public event EventHandler<MouseEventExtArgs> MouseMoveExt;

		/// <summary>
		/// Occurs when a click was performed by the mouse.
		/// </summary>
		public event MouseEventHandler MouseClick;

		/// <summary>
		/// Occurs when a click was performed by the mouse.
		/// </summary>
		/// <remarks>
		/// This event provides extended arguments of type <see cref = "MouseEventArgs" /> enabling you to 
		/// supress further processing of mouse click in other applications.
		/// </remarks>
		[Obsolete("To supress mouse clicks use MouseDownExt event instead.")]
		public event EventHandler<MouseEventExtArgs> MouseClickExt;

		/// <summary>
		/// Occurs when the mouse a mouse button is pressed.
		/// </summary>
		public event MouseEventHandler MouseDown;

		/// <summary>
		/// Occurs when the mouse a mouse button is pressed.
		/// </summary>
		/// <remarks>
		/// This event provides extended arguments of type <see cref = "MouseEventArgs" /> enabling you to 
		/// supress further processing of mouse click in other applications.
		/// </remarks>
		public event EventHandler<MouseEventExtArgs> MouseDownExt;

		/// <summary>
		/// Occurs when a mouse button is released.
		/// </summary>
		public event MouseEventHandler MouseUp;

		/// <summary>
		/// Occurs when the mouse wheel moves.
		/// </summary>
		public event MouseEventHandler MouseWheel;

		/// <summary>
		/// Occurs when a mouse button is double-clicked.
		/// </summary>
		public event MouseEventHandler MouseDoubleClick;

		#endregion

		#region Fields

		private Hooker _hooker;

		private Point _previousPosition;
        private int _previousClickedTime;
		private MouseButtons _previousClicked;
		private MouseButtons _downButtonsWaitingForMouseUp;
		private MouseButtons _suppressButtonUpFlags;
        private int _systemDoubleClickTime;

		#endregion

		#region Constructors

		/// <summary>
		/// Initializes a new instance of <see cref="MouseHookListener"/>.
		/// </summary>
		/// <param name="hooker">Depending on this parameter the listener hooks either application or global mouse events.</param>
		/// <remarks>
		/// Hooks are not active after installation. You need to use either <see cref="BaseHookListener.Enabled"/> property or call <see cref="BaseHookListener.Start"/> method.
		/// </remarks>
		public MouseHookListener(Hooker hooker)
		{
			_hooker = hooker ?? throw new ArgumentNullException("hooker");

			_previousPosition = new Point(-1, -1);
            _previousClickedTime = 0;
			_downButtonsWaitingForMouseUp = MouseButtons.None;
            _suppressButtonUpFlags = MouseButtons.None;
			_previousClicked = MouseButtons.None;
            _systemDoubleClickTime = Mouse.GetDoubleClickTime();
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets or Sets the enabled status of the Hook.
		/// </summary>
		/// <value>
		/// True - The Hook is presently installed, activated, and will fire events.
		/// <para>
		/// False - The Hook is not part of the hook chain, and will not fire events.
		/// </para>
		/// </value>
		public bool Enabled
		{
			get
			{
				return HookHandle != 0;
			}
			set
			{
				bool mustEnable = value;
				if (mustEnable)
				{
					if (!Enabled)
					{
						Start();
					}
				}
				else
				{
					if (Enabled)
					{
						Stop();
					}
				}
			}
		}

		/// <summary>
		/// Stores the handle to the Keyboard or Mouse hook procedure.
		/// </summary>
		protected int HookHandle { get; set; }

		/// <summary>
		/// Keeps the reference to prevent garbage collection of delegate. See: CallbackOnCollectedDelegate http://msdn.microsoft.com/en-us/library/43yky316(v=VS.100).aspx
		/// </summary>
		protected HookCallback HookCallbackReferenceKeeper { get; set; }

		internal bool IsGlobal
		{
			get
			{
				return _hooker.IsGlobal;
			}
		}

		#endregion

		#region Methods

		//##################################################################
		#region ProcessCallback and related subroutines

		/// <summary>
		/// This method processes the data from the hook and initiates event firing.
		/// </summary>
		/// <param name="wParam">The first Windows Messages parameter.</param>
		/// <param name="lParam">The second Windows Messages parameter.</param>
		/// <returns>
		/// True - The hook will be passed along to other applications.
		/// <para>
		/// False - The hook will not be given to other applications, effectively blocking input.
		/// </para>
		/// </returns>
		protected bool ProcessCallback(int wParam, IntPtr lParam)
        {
            MouseEventExtArgs e = MouseEventExtArgs.FromRawData(wParam, lParam, IsGlobal);

            if (e.IsMouseKeyDown)
            {
                ProcessMouseDown(ref e);
            }

            if (e.Clicks == 1 && e.IsMouseKeyUp && !e.Handled)
            {
                ProcessMouseClick(ref e);
            }

            if (e.Clicks == 2 && !e.Handled)
            {
                InvokeMouseEventHandler(MouseDoubleClick, e);
            }

            if (e.IsMouseKeyUp)
            {
                ProcessMouseUp(ref e);
            }

            if (e.WheelScrolled)
            {
                InvokeMouseEventHandler(MouseWheel, e);
            }

            if (HasMoved(e.Point))
            {
                ProcessMouseMove(ref e);
            }

            return !e.Handled;
        }

        private void ProcessMouseDown(ref MouseEventExtArgs e)
        {
            if (IsGlobal)
            {
                ProcessPossibleDoubleClick(ref e);
            }
            else
            {
                // These are only used for global. No need for them in AppHooks
                _downButtonsWaitingForMouseUp = MouseButtons.None;
                _previousClicked = MouseButtons.None;
                _previousClickedTime = 0;
            } 
            

            InvokeMouseEventHandler(MouseDown, e);
            InvokeMouseEventHandlerExt(MouseDownExt, e);
            if (e.Handled)
            {
                SetSupressButtonUpFlag(e.Button);
                e.Handled = true;
            }
        }

        private void ProcessPossibleDoubleClick(ref MouseEventExtArgs e)
        {
            if (IsDoubleClick(e.Button, e.Timestamp))
            {
                e = e.ToDoubleClickEventArgs();
                _downButtonsWaitingForMouseUp = MouseButtons.None;
                _previousClicked = MouseButtons.None;
                _previousClickedTime = 0;
            }
            else
            {
                _downButtonsWaitingForMouseUp |= e.Button;
                _previousClickedTime = e.Timestamp;
            }
        }

        private void ProcessMouseClick(ref MouseEventExtArgs e)
        {
            if ((_downButtonsWaitingForMouseUp & e.Button) != MouseButtons.None)
            {
                _previousClicked = e.Button;
                _downButtonsWaitingForMouseUp = MouseButtons.None;
                InvokeMouseEventHandler(MouseClick, e);
                InvokeMouseEventHandlerExt(MouseClickExt, e);
            }
        }

        private void ProcessMouseUp(ref MouseEventExtArgs e)
        {
            if (!HasSupressButtonUpFlag(e.Button))
            {
                InvokeMouseEventHandler(MouseUp, e);
            }
            else
            {
                RemoveSupressButtonUpFlag(e.Button);
                e.Handled = true;
            }
        }

        private void ProcessMouseMove(ref MouseEventExtArgs e)
        {
            _previousPosition = e.Point;

            InvokeMouseEventHandler(MouseMove, e);
            InvokeMouseEventHandlerExt(MouseMoveExt, e);
        }

		#endregion

		/// <summary>
		/// A callback function which will be called every time a keyboard or mouse activity detected.
		/// <see cref="WinApi.HookCallback"/>
		/// </summary>
		protected int HookCallback(int nCode, Int32 wParam, IntPtr lParam)
		{
			if (nCode == 0)
			{
				bool shouldProcess = ProcessCallback(wParam, lParam);

				if (!shouldProcess)
				{
					return -1;
				}
			}

			return CallNextHook(nCode, wParam, lParam);
		}

		private int CallNextHook(int nCode, int wParam, IntPtr lParam)
		{
			return Hooker.CallNextHookEx(HookHandle, nCode, wParam, lParam);
		}

		/// <summary>
		/// Subscribes to the hook and starts firing events.
		/// </summary>
		/// <exception cref="System.ComponentModel.Win32Exception"></exception>
		public void Start()
		{
			if (Enabled)
			{
				throw new InvalidOperationException("Hook listener is already started. Call Stop() method firts or use Enabled property.");
			}

			HookCallbackReferenceKeeper = new HookCallback(HookCallback);
			try
			{
				HookHandle = _hooker.Subscribe(GetHookId(), HookCallbackReferenceKeeper);
			}
			catch (Exception)
			{
				HookCallbackReferenceKeeper = null;
				HookHandle = 0;
				throw;
			}
		}

		/// <summary>
		/// Unsubscribes from the hook and stops firing events.
		/// </summary>
		/// <exception cref="System.ComponentModel.Win32Exception"></exception>
		public void Stop()
		{
			try
			{
				_hooker.Unsubscribe(HookHandle);
			}
			finally
			{
				HookCallbackReferenceKeeper = null;
				HookHandle = 0;
			}
		}

		/// <summary>
		/// Enables you to switch from application hooks to global hooks and vice versa on the fly
		/// without unsubscribing from events. Component remains enabled or disabled state after this call as it was before.
		/// </summary>
		/// <param name="hooker">An AppHooker or GlobalHooker object.</param>
		public void Replace(Hooker hooker)
		{
			bool rememberEnabled = Enabled;
			Enabled = false;
			_hooker = hooker;
			Enabled = rememberEnabled;
		}

		private void RemoveSupressButtonUpFlag(MouseButtons button)
		{
			_suppressButtonUpFlags = _suppressButtonUpFlags ^ button;
		}

		private bool HasSupressButtonUpFlag(MouseButtons button)
		{
			return (_suppressButtonUpFlags & button) != 0;
		}

		private void SetSupressButtonUpFlag(MouseButtons button)
		{
			_suppressButtonUpFlags = _suppressButtonUpFlags | button;
		}

		/// <summary>
		/// Returns the correct hook id to be used for <see cref="Hooker.SetWindowsHookEx"/> call.
		/// </summary>
		/// <returns>WH_MOUSE (0x07) or WH_MOUSE_LL (0x14) constant.</returns>
		protected int GetHookId()
		{
			return IsGlobal ? GlobalHooker.WH_MOUSE_LL : AppHooker.WH_MOUSE;
		}

		private bool HasMoved(Point actualPoint)
		{
			return _previousPosition != actualPoint;
		}

		private bool IsDoubleClick(MouseButtons button, int timestamp)
		{
            return button == _previousClicked && timestamp - _previousClickedTime <= _systemDoubleClickTime; // Mouse.GetDoubleClickTime();
		}

		private void InvokeMouseEventHandler(MouseEventHandler handler, MouseEventArgs e)
		{
			handler?.Invoke(this, e);
		}


		private void InvokeMouseEventHandlerExt(EventHandler<MouseEventExtArgs> handler, MouseEventExtArgs e)
		{
			handler?.Invoke(this, e);
		}

		/// <summary>
		/// Release delegates, unsubscribes from hooks.
		/// </summary>
		/// <filterpriority>2</filterpriority>
		public void Dispose()
		{
			MouseClick = null;
			MouseClickExt = null;
			MouseDown = null;
			MouseDownExt = null;
			MouseMove = null;
			MouseMoveExt = null;
			MouseUp = null;
			MouseWheel = null;
			MouseDoubleClick = null;

			Stop();
		}

		/// <summary>
		/// Unsubscribes from global hooks skiping error handling.
		/// </summary>
		~MouseHookListener()
		{
			if (HookHandle != 0)
			{
				Hooker.UnhookWindowsHookEx(HookHandle);
			}
		}

		#endregion
	}
}