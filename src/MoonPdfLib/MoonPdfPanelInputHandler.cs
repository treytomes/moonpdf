/*! MoonPdfLib - Provides a WPF user control to display PDF files
Copyright (C) 2013  (see AUTHORS file)

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.
!*/

using System.Windows;
using System.Windows.Input;
using MouseKeyboardActivityMonitor;
using MouseKeyboardActivityMonitor.WinApi;
using System.Windows.Media;
using System.Windows.Controls.Primitives;

namespace MoonPdfLib
{
	internal class MoonPdfPanelInputHandler
	{
		#region Fields

		private MouseHookListener _mouseHookListener;
		private MoonPdfPanel _source;
		private System.Windows.Point? _lastMouseDownLocation;
		private double _lastMouseDownVerticalOffset;
		private double _lastMouseDownHorizontalOffset;

		#endregion

		#region Constructors

		public MoonPdfPanelInputHandler(MoonPdfPanel source)
		{
			_source = source;

			_source.PreviewKeyDown += source_PreviewKeyDown;
			_source.PreviewMouseWheel += source_PreviewMouseWheel;
			_source.PreviewMouseLeftButtonDown += source_PreviewMouseLeftButtonDown;

			_mouseHookListener = new MouseHookListener(new GlobalHooker());
			_mouseHookListener.Enabled = false;
			_mouseHookListener.MouseMove += mouseHookListener_MouseMove;
			_mouseHookListener.MouseUp += mouseHookListener_MouseUp;
		}

		#endregion

		#region Methods

		private static bool IsScrollBarChild(DependencyObject o)
		{
			DependencyObject parent = o;

			while (parent != null)
			{
				if (parent is ScrollBar)
					return true;

				parent = VisualTreeHelper.GetParent(parent);
			}

			return false;
		}

		#endregion

		#region Event Handlers

		void source_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			/* maybe for future use
			if (e.OriginalSource is Image)
			{
				var pos = e.GetPosition((Image)e.OriginalSource);
				MessageBox.Show(pos.X + " x " + pos.Y);
			}
			*/

			if (IsScrollBarChild(e.OriginalSource as DependencyObject)) // if the mouse click comes from the scrollbar, then we do not scroll
			{
				_lastMouseDownLocation = null;
			}
			else
			{
				if (_source.ScrollViewer != null)
				{
					_mouseHookListener.Enabled = true;

					_lastMouseDownVerticalOffset = _source.ScrollViewer.VerticalOffset;
					_lastMouseDownHorizontalOffset = _source.ScrollViewer.HorizontalOffset;
					_lastMouseDownLocation = _source.PointToScreen(e.GetPosition(this._source));
				}
			}
		}

		void source_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
		{
			var ctrlDown = System.Windows.Input.Keyboard.IsKeyDown(Key.LeftCtrl) || System.Windows.Input.Keyboard.IsKeyDown(Key.RightCtrl);

			if (ctrlDown || e.RightButton == MouseButtonState.Pressed)
			{
				if (e.Delta > 0)
					_source.ZoomIn();
				else
					_source.ZoomOut();

				e.Handled = true;
			}
			else if (!ctrlDown && (_source.ScrollViewer == null || _source.ScrollViewer.ComputedVerticalScrollBarVisibility != Visibility.Visible) && _source.PageRowDisplay == PageRowDisplayType.SinglePageRow)
			{
				if (e.Delta > 0)
					_source.GotoPreviousPage();
				else
					_source.GotoNextPage();

				e.Handled = true;
			}
			else if (_source.ScrollViewer != null && (System.Windows.Input.Keyboard.IsKeyDown(Key.LeftShift) || System.Windows.Input.Keyboard.IsKeyDown(Key.RightShift)))
			{
				if (e.Delta > 0)
					_source.ScrollViewer.LineLeft();
				else
					_source.ScrollViewer.LineRight();

				e.Handled = true;
			}
		}

		void source_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Home)
				_source.GotoPage(1);
			else if (e.Key == Key.End)
				_source.GotoLastPage();
			else if (e.Key == Key.Add || e.Key == Key.OemPlus)
				_source.ZoomIn();
			else if (e.Key == Key.Subtract || e.Key == Key.OemMinus)
				_source.ZoomOut();

			if (_source.ScrollViewer != null && _source.ScrollViewer.ComputedHorizontalScrollBarVisibility == Visibility.Visible)
				return;

			if (e.Key == Key.Left)
				_source.GotoPreviousPage();
			else if (e.Key == Key.Right)
				_source.GotoNextPage();
		}

		void mouseHookListener_MouseUp(object sender, System.Windows.Forms.MouseEventArgs e)
		{
			_mouseHookListener.Enabled = false;
			_lastMouseDownLocation = null;
		}

		void mouseHookListener_MouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
		{
			if (_lastMouseDownLocation != null)
			{
				var currentPos = e.Location;
                var proposedYOffset = _lastMouseDownVerticalOffset + _lastMouseDownLocation.Value.Y - currentPos.Y;
                var proposedXOffset = _lastMouseDownHorizontalOffset + _lastMouseDownLocation.Value.X - currentPos.X;

                if (proposedYOffset <= 0 || proposedYOffset > _source.ScrollViewer.ScrollableHeight)
                {
                    _lastMouseDownVerticalOffset = proposedYOffset <= 0 ? 0 : _source.ScrollViewer.ScrollableHeight;
                    _lastMouseDownLocation = new System.Windows.Point(_lastMouseDownLocation.Value.X, e.Y);

                    proposedYOffset = _lastMouseDownVerticalOffset + _lastMouseDownLocation.Value.Y - currentPos.Y;
                }

                _source.ScrollViewer.ScrollToVerticalOffset(proposedYOffset);

                
                if (proposedXOffset <= 0 || proposedXOffset > _source.ScrollViewer.ScrollableWidth)
                {
                    _lastMouseDownHorizontalOffset = proposedXOffset <= 0 ? 0 : _source.ScrollViewer.ScrollableWidth;
                    _lastMouseDownLocation = new System.Windows.Point(e.X, _lastMouseDownLocation.Value.Y);
                    proposedXOffset = _lastMouseDownHorizontalOffset + _lastMouseDownLocation.Value.X - currentPos.X;
                }

                _source.ScrollViewer.ScrollToHorizontalOffset(proposedXOffset);   
			}
		}

		#endregion
	}
}
