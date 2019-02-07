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

using MoonPdfLib.Helper;
using MoonPdfLib.MuPdf;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace MoonPdfLib
{
	internal partial class SinglePageMoonPdfPanel : UserControl, IMoonPdfPanel
	{
		#region Fields

		private MoonPdfPanel _parent;
		private ScrollViewer _scrollViewer;
		private PdfImageProvider _imageProvider;

		/// <summary>
		/// starting at 0
		/// </summary>
		private int _currentPageIndex = 0;

		#endregion

		#region Constructors

		public SinglePageMoonPdfPanel(MoonPdfPanel parent)
		{
			InitializeComponent();
			_parent = parent;
			SizeChanged += SinglePageMoonPdfPanel_SizeChanged;
		}

		#endregion

		#region Properties

		ScrollViewer IMoonPdfPanel.ScrollViewer
		{
			get
			{
				return _scrollViewer;
			}
		}

		UserControl IMoonPdfPanel.Instance
		{
			get
			{
				return this;
			}
		}

		public float CurrentZoom
		{
			get
			{
				if (_imageProvider != null)
				{
					return _imageProvider.Settings.ZoomFactor;
				}
				return 1.0f;
			}
		}

		#endregion

		#region Methods

		public void Load(IPdfSource source, string password = null)
		{
			_scrollViewer = VisualTreeHelperEx.FindChild<ScrollViewer>(this);
			_imageProvider = new PdfImageProvider(source, _parent.TotalPages, new PageDisplaySettings(_parent.GetPagesPerRow(), _parent.ViewType, _parent.HorizontalMargin, _parent.Rotation), false, password);

			_currentPageIndex = 0;

			if (_scrollViewer != null)
				_scrollViewer.Visibility = Visibility.Visible;

			if (_parent.ZoomType == ZoomType.Fixed)
			{
				SetItemsSource();
			}
			else if (_parent.ZoomType == ZoomType.FitToHeight)
			{
				ZoomToHeight();
			}
			else if (_parent.ZoomType == ZoomType.FitToWidth)
			{
				ZoomToWidth();
			}
		}

		public void Unload()
		{
			_scrollViewer.Visibility = Visibility.Collapsed;
			_scrollViewer.ScrollToHorizontalOffset(0);
			_scrollViewer.ScrollToVerticalOffset(0);
			_currentPageIndex = 0;

			_imageProvider = null;
		}

		void IMoonPdfPanel.GotoPage(int pageNumber)
		{
			_currentPageIndex = pageNumber - 1;
			SetItemsSource();

			if (_scrollViewer != null)
			{
				_scrollViewer.ScrollToTop();
			}
		}

		void IMoonPdfPanel.GotoPreviousPage()
		{
			var prevPageIndex = PageHelper.GetPreviousPageIndex(_currentPageIndex, _parent.ViewType);

			if (prevPageIndex == -1)
			{
				return;
			}

			_currentPageIndex = prevPageIndex;

			SetItemsSource();

			if (_scrollViewer != null)
			{
				_scrollViewer.ScrollToTop();
			}
		}

		void IMoonPdfPanel.GotoNextPage()
		{
			var nextPageIndex = PageHelper.GetNextPageIndex(_currentPageIndex, _parent.TotalPages, _parent.ViewType);

			if (nextPageIndex == -1)
			{
				return;
			}

			_currentPageIndex = nextPageIndex;

			SetItemsSource();

			if (_scrollViewer != null)
			{
				_scrollViewer.ScrollToTop();
			}
		}

		private void SetItemsSource()
		{
			var startIndex = PageHelper.GetVisibleIndexFromPageIndex(_currentPageIndex, _parent.ViewType);
			itemsControl.ItemsSource = _imageProvider.FetchRange(startIndex, _parent.GetPagesPerRow()).FirstOrDefault();
		}

		public int GetCurrentPageIndex(ViewType viewType)
		{
			return _currentPageIndex;
		}

		#region Zoom specific code

		public void ZoomToWidth()
		{
			var scrollBarWidth = _scrollViewer.ComputedVerticalScrollBarVisibility == Visibility.Visible ? SystemParameters.VerticalScrollBarWidth : 0;
			var zoomFactor = (_parent.ActualWidth - scrollBarWidth) / _parent.PageRowBounds[_currentPageIndex].SizeIncludingOffset.Width;
			var pageBound = _parent.PageRowBounds[_currentPageIndex];

			if (scrollBarWidth == 0 && ((pageBound.Size.Height * zoomFactor) + pageBound.VerticalOffset) >= _parent.ActualHeight)
			{
				scrollBarWidth += SystemParameters.VerticalScrollBarWidth;
			}

			scrollBarWidth += 2; // Magic number, sorry :)
			zoomFactor = (_parent.ActualWidth - scrollBarWidth) / _parent.PageRowBounds[_currentPageIndex].SizeIncludingOffset.Width;

			ZoomInternal(zoomFactor);
		}

		public void ZoomToHeight()
		{
			var scrollBarHeight = _scrollViewer.ComputedHorizontalScrollBarVisibility == Visibility.Visible ? SystemParameters.HorizontalScrollBarHeight : 0;
			var zoomFactor = (_parent.ActualHeight - scrollBarHeight) / _parent.PageRowBounds[_currentPageIndex].SizeIncludingOffset.Height;
			var pageBound = _parent.PageRowBounds[_currentPageIndex];

			if (scrollBarHeight == 0 && ((pageBound.Size.Width * zoomFactor) + pageBound.HorizontalOffset) >= _parent.ActualWidth)
			{
				scrollBarHeight += SystemParameters.HorizontalScrollBarHeight;
			}

			zoomFactor = (_parent.ActualHeight - scrollBarHeight) / _parent.PageRowBounds[_currentPageIndex].SizeIncludingOffset.Height;

			ZoomInternal(zoomFactor);
		}

		public void ZoomIn()
		{
			ZoomInternal(CurrentZoom + _parent.ZoomStep);
		}

		public void ZoomOut()
		{
			ZoomInternal(CurrentZoom - _parent.ZoomStep);
		}

		public void Zoom(double zoomFactor)
		{
			ZoomInternal(zoomFactor);
		}

		private void ZoomInternal(double zoomFactor)
		{
			if (zoomFactor > _parent.MaxZoomFactor)
			{
				zoomFactor = _parent.MaxZoomFactor;
			}
			else if (zoomFactor < _parent.MinZoomFactor)
			{
				zoomFactor = _parent.MinZoomFactor;
			}

			_imageProvider.Settings.ZoomFactor = (float)zoomFactor;
			SetItemsSource();
		}

		#endregion

		#region Event Handlers

		void SinglePageMoonPdfPanel_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			_scrollViewer = VisualTreeHelperEx.FindChild<ScrollViewer>(this);
		}

		#endregion

		#endregion
	}
}
