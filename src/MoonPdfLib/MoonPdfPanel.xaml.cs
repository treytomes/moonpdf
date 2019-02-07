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
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace MoonPdfLib
{
	public partial class MoonPdfPanel : UserControl
	{
		#region Events

		public event EventHandler PdfLoaded;
		public event EventHandler ZoomTypeChanged;
		public event EventHandler ViewTypeChanged;
		public event EventHandler PageRowDisplayChanged;
		public event EventHandler<PasswordRequiredEventArgs> PasswordRequired;

		#endregion

		#region Fields

		private ZoomType _zoomType = ZoomType.Fixed;
		private IMoonPdfPanel _innerPanel;
		private MoonPdfPanelInputHandler _inputHandler;
		private PageRowBound[] _pageRowBounds;
		private DispatcherTimer _resizeTimer;

		#endregion

		#region Dependency Properties

		#region Page Margin

		public static readonly DependencyProperty PageMarginProperty = DependencyProperty.Register(nameof(PageMargin), typeof(Thickness), typeof(MoonPdfPanel), new FrameworkPropertyMetadata(new Thickness(0, 2, 4, 2)));

		public Thickness PageMargin
		{
			get
			{
				return (Thickness)GetValue(PageMarginProperty);
			}
			set
			{
				SetValue(PageMarginProperty, value);
			}
		}

		#endregion

		#region ZoomStep

		public static readonly DependencyProperty ZoomStepProperty = DependencyProperty.Register(nameof(ZoomStep), typeof(double), typeof(MoonPdfPanel), new FrameworkPropertyMetadata(0.25));

		public double ZoomStep
		{
			get
			{
				return (double)GetValue(ZoomStepProperty);
			}
			set
			{
				SetValue(ZoomStepProperty, value);
			}
		}

		#endregion

		#region MinZoomFactor

		public static readonly DependencyProperty MinZoomFactorProperty = DependencyProperty.Register(nameof(MinZoomFactor), typeof(double), typeof(MoonPdfPanel), new FrameworkPropertyMetadata(0.15));

		public double MinZoomFactor
		{
			get
			{
				return (double)GetValue(MinZoomFactorProperty);
			}
			set
			{
				SetValue(MinZoomFactorProperty, value);
			}
		}

		#endregion

		#region MaxZoomFactor

		public static readonly DependencyProperty MaxZoomFactorProperty = DependencyProperty.Register(nameof(MaxZoomFactor), typeof(double), typeof(MoonPdfPanel), new FrameworkPropertyMetadata(6.0));

		public double MaxZoomFactor
		{
			get
			{
				return (double)GetValue(MaxZoomFactorProperty);
			}
			set
			{
				SetValue(MaxZoomFactorProperty, value);
			}
		}

		#endregion

		#region ViewType

		public static readonly DependencyProperty ViewTypeProperty = DependencyProperty.Register(nameof(ViewType), typeof(ViewType), typeof(MoonPdfPanel), new FrameworkPropertyMetadata(MoonPdfLib.ViewType.SinglePage));

		public ViewType ViewType
		{
			get
			{
				return (ViewType)GetValue(ViewTypeProperty);
			}
			set
			{
				SetValue(ViewTypeProperty, value);
			}
		}

		#endregion

		#region Rotation

		public static readonly DependencyProperty RotationProperty = DependencyProperty.Register(nameof(Rotation), typeof(ImageRotation), typeof(MoonPdfPanel), new FrameworkPropertyMetadata(ImageRotation.None));

		public ImageRotation Rotation
		{
			get
			{
				return (ImageRotation)GetValue(RotationProperty);
			}
			set
			{
				SetValue(RotationProperty, value);
			}
		}

		#endregion

		#region PageRowDisplay

		public static readonly DependencyProperty PageRowDisplayProperty = DependencyProperty.Register(nameof(PageRowDisplay), typeof(PageRowDisplayType), typeof(MoonPdfPanel), new FrameworkPropertyMetadata(PageRowDisplayType.SinglePageRow));

		public PageRowDisplayType PageRowDisplay
		{
			get
			{
				return (PageRowDisplayType)GetValue(PageRowDisplayProperty);
			}
			set
			{
				SetValue(PageRowDisplayProperty, value);
			}
		}

		#endregion

		#region CurrentZoom

		public static readonly DependencyProperty CurrentZoomProperty = DependencyProperty.Register(nameof(CurrentZoom), typeof(double), typeof(MoonPdfPanel), new FrameworkPropertyMetadata(1.0, OnCurrentZoomChanged));

		public double CurrentZoom
		{
			get
			{
				return (double)GetValue(CurrentZoomProperty);
			}
			set
			{
				SetValue(CurrentZoomProperty, value);
			}
		}

		private static void OnCurrentZoomChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
		{
			var panel = sender as MoonPdfPanel;
			if (panel == null)
			{
				return;
			}

			panel._innerPanel.Zoom(Convert.ToDouble(e.NewValue));
			panel.ZoomType = ZoomType.Fixed;
		}

		#endregion

		#region CurrentPage

		public static readonly DependencyProperty CurrentPageProperty = DependencyProperty.Register(nameof(CurrentPage), typeof(int), typeof(MoonPdfPanel), new FrameworkPropertyMetadata(1, OnCurrentPageChanged));

		public int CurrentPage
		{
			get
			{
				return (int)GetValue(CurrentPageProperty);
			}
			set
			{
				SetValue(CurrentPageProperty, value);
			}
		}

		private static void OnCurrentPageChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
		{
			var panel = sender as MoonPdfPanel;
			if (panel == null)
			{
				return;
			}

			var newValue = Convert.ToInt32(e.NewValue);
			if (newValue < 1)
			{
				newValue = 1;
			}
			else if (newValue > panel.TotalPages)
			{
				newValue = panel.TotalPages;
			}
			panel._innerPanel.GotoPage(newValue);
		}

		#endregion

		#endregion

		public double HorizontalMargin
		{
			get
			{
				return PageMargin.Right;
			}
		}

		public IPdfSource CurrentSource { get; private set; }
		public string CurrentPassword { get; private set; }
		public int TotalPages { get; private set; }

		internal PageRowBound[] PageRowBounds
		{
			get
			{
				return _pageRowBounds;
			}
		}

		public ZoomType ZoomType
		{
			get
			{
				return _zoomType;
			}
			private set
			{
				if (_zoomType != value)
				{
					_zoomType = value;

					ZoomTypeChanged?.Invoke(this, EventArgs.Empty);
				}
			}
		}

		internal ScrollViewer ScrollViewer
		{
			get
			{
				return _innerPanel.ScrollViewer;
			}
		}

		//public float CurrentZoom
		//{
		//	get { return this._innerPanel.CurrentZoom; }
		//}

		public MoonPdfPanel()
		{
			InitializeComponent();

			ChangeDisplayType(PageRowDisplay);
			_inputHandler = new MoonPdfPanelInputHandler(this);

			SizeChanged += PdfViewerPanel_SizeChanged;

			_resizeTimer = new DispatcherTimer();
			_resizeTimer.Interval = TimeSpan.FromMilliseconds(150);
			_resizeTimer.Tick += resizeTimer_Tick;
		}

		void PdfViewerPanel_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			if (this.CurrentSource == null)
				return;

			_resizeTimer.Stop();
			_resizeTimer.Start();
		}

		void resizeTimer_Tick(object sender, EventArgs e)
		{
			_resizeTimer.Stop();

			if (this.CurrentSource == null)
				return;

			if (this.ZoomType == ZoomType.FitToWidth)
				ZoomToWidth();
			else if (this.ZoomType == ZoomType.FitToHeight)
				ZoomToHeight();
		}

		public void OpenFile(string pdfFilename, string password = null)
		{
			if (!File.Exists(pdfFilename))
				throw new FileNotFoundException(string.Empty, pdfFilename);

			this.Open(new FileSource(pdfFilename), password);
		}

		public void Open(IPdfSource source, string password = null)
		{
			var pw = password;

			if (PasswordRequired != null && MuPdfWrapper.NeedsPassword(source) && pw == null)
			{
				var e = new PasswordRequiredEventArgs();
				PasswordRequired(this, e);

				if (e.Cancel)
					return;

				pw = e.Password;
			}

			LoadPdf(source, pw);
			CurrentSource = source;
			CurrentPassword = pw;
			CurrentPage = 1;

			PdfLoaded?.Invoke(this, EventArgs.Empty);
		}

		public void Unload()
		{
			CurrentSource = null;
			CurrentPassword = null;
			TotalPages = 0;

			_innerPanel.Unload();

			PdfLoaded?.Invoke(this, EventArgs.Empty);
		}

		private void LoadPdf(IPdfSource source, string password)
		{
			var pageBounds = MuPdfWrapper.GetPageBounds(source, Rotation, password);
			_pageRowBounds = CalculatePageRowBounds(pageBounds, ViewType);
			TotalPages = pageBounds.Length;
			_innerPanel.Load(source, password);
		}

		private PageRowBound[] CalculatePageRowBounds(Size[] singlePageBounds, ViewType viewType)
		{
			var pagesPerRow = Math.Min(GetPagesPerRow(), singlePageBounds.Length); // if multiple page-view, but pdf contains less pages than the pages per row
			var finalBounds = new List<PageRowBound>();
			var verticalBorderOffset = PageMargin.Top + PageMargin.Bottom;

			if (viewType == ViewType.SinglePage)
			{
				finalBounds.AddRange(singlePageBounds.Select(p => new PageRowBound(p, verticalBorderOffset, 0)));
			}
			else
			{
				var horizontalBorderOffset = HorizontalMargin;

				for (int i = 0; i < singlePageBounds.Length; i++)
				{
					if (i == 0 && viewType == ViewType.BookView)
					{
						finalBounds.Add(new PageRowBound(singlePageBounds[0], verticalBorderOffset, 0));
						continue;
					}

					var subset = singlePageBounds.Take(i, pagesPerRow).ToArray();

					// we get the max page-height from all pages in the subset and the sum of all page widths of the subset plus the offset between the pages
					finalBounds.Add(new PageRowBound(new Size(subset.Sum(f => f.Width), subset.Max(f => f.Height)), verticalBorderOffset, horizontalBorderOffset * (subset.Length - 1)));
					i += (pagesPerRow - 1);
				}
			}

			return finalBounds.ToArray();
		}

		internal int GetPagesPerRow()
		{
			return ViewType == ViewType.SinglePage ? 1 : 2;
		}

		public int GetCurrentPageNumber()
		{
			if (_innerPanel == null)
				return -1;

			return _innerPanel.GetCurrentPageIndex(ViewType) + 1;
		}

		public void ZoomToWidth()
		{
			_innerPanel.ZoomToWidth();
			ZoomType = ZoomType.FitToWidth;
			CurrentZoom = _innerPanel.CurrentZoom;
		}

		public void ZoomToHeight()
		{
			_innerPanel.ZoomToHeight();
			ZoomType = ZoomType.FitToHeight;
			CurrentZoom = _innerPanel.CurrentZoom;
		}

		public void ZoomIn()
		{
			_innerPanel.ZoomIn();
			ZoomType = ZoomType.Fixed;
			CurrentZoom = _innerPanel.CurrentZoom;
		}

		public void ZoomOut()
		{
			_innerPanel.ZoomOut();
			ZoomType = ZoomType.Fixed;
			CurrentZoom = _innerPanel.CurrentZoom;
		}

		public void Zoom(double zoomFactor)
		{
			CurrentZoom = zoomFactor;
			//_innerPanel.Zoom(zoomFactor);
			//ZoomType = ZoomType.Fixed;
		}

		/// <summary>
		/// Sets the ZoomType back to Fixed
		/// </summary>
		public void SetFixedZoom()
		{
			ZoomType = ZoomType.Fixed;
		}

		public void GotoPreviousPage()
		{
			_innerPanel.GotoPreviousPage();
			CurrentPage = GetCurrentPageNumber();
		}

		public void GotoNextPage()
		{
			_innerPanel.GotoNextPage();
			CurrentPage = GetCurrentPageNumber();
		}

		public void GotoPage(int pageNumber)
		{
			CurrentPage = pageNumber;
		}

		public void GotoFirstPage()
		{
			GotoPage(1);
		}

		public void GotoLastPage()
		{
			GotoPage(TotalPages);
		}

		public void RotateRight()
		{
			if (Rotation != ImageRotation.Rotate270)
				Rotation = (ImageRotation)Rotation + 1;
			else
				Rotation = ImageRotation.None;
		}

		public void RotateLeft()
		{
			if ((int)Rotation > 0)
				Rotation = (ImageRotation)Rotation - 1;
			else
				Rotation = ImageRotation.Rotate270;
		}

		public void Rotate(ImageRotation rotation)
		{
			var currentPage = _innerPanel.GetCurrentPageIndex(ViewType) + 1;
			LoadPdf(CurrentSource, CurrentPassword);
			_innerPanel.GotoPage(currentPage);
		}

		public void TogglePageDisplay()
		{
			PageRowDisplay = (PageRowDisplay == PageRowDisplayType.SinglePageRow) ? PageRowDisplayType.ContinuousPageRows : PageRowDisplayType.SinglePageRow;
		}

		protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
		{
			base.OnPropertyChanged(e);

			if (e.Property.Name.Equals("PageRowDisplay"))
				ChangeDisplayType((PageRowDisplayType)e.NewValue);
			else if (e.Property.Name.Equals("Rotation"))
				Rotate((ImageRotation)e.NewValue);
			else if (e.Property.Name.Equals("ViewType"))
				ApplyChangedViewType((ViewType)e.OldValue);
		}

		private void ApplyChangedViewType(ViewType oldViewType)
		{
			UpdateAndReload(() => { }, oldViewType);

			ViewTypeChanged?.Invoke(this, EventArgs.Empty);
		}

		private void ChangeDisplayType(PageRowDisplayType pageRowDisplayType)
		{
			UpdateAndReload(() =>
				{
					// we need to remove the current innerPanel
					pnlMain.Children.Clear();

					if (pageRowDisplayType == PageRowDisplayType.SinglePageRow)
						_innerPanel = new SinglePageMoonPdfPanel(this);
					else
						_innerPanel = new ContinuousMoonPdfPanel(this);

					pnlMain.Children.Add(_innerPanel.Instance);
				}, ViewType);

			PageRowDisplayChanged?.Invoke(this, EventArgs.Empty);
		}

		private void UpdateAndReload(Action updateAction, ViewType viewType)
		{
			var currentPage = -1;
			var zoom = 1.0f;

			if (CurrentSource != null)
			{
				currentPage = _innerPanel.GetCurrentPageIndex(viewType) + 1;
				zoom = _innerPanel.CurrentZoom;
			}

			updateAction();

			if (currentPage > -1)
			{
				Action reloadAction = () =>
					{
						LoadPdf(CurrentSource, CurrentPassword);
						_innerPanel.Zoom(zoom);
						_innerPanel.GotoPage(currentPage);
					};

				if (_innerPanel.Instance.IsLoaded)
					reloadAction();
				else
				{
					// we need to wait until the controls are loaded and then reload the pdf
					_innerPanel.Instance.Loaded += (s, e) => { reloadAction(); };
				}
			}
		}

		/// <summary>
		/// Will only be triggered if the AllowDrop-Property is set to true
		/// </summary>
		/// <param name="e"></param>
		protected override void OnDrop(DragEventArgs e)
		{
			base.OnDrop(e);

			if (e.Data.GetDataPresent(DataFormats.FileDrop))
			{
				var filenames = (string[])e.Data.GetData(DataFormats.FileDrop);
				var filename = filenames.FirstOrDefault();

				if (filename != null && File.Exists(filename))
				{
					string pw = null;

					if (MuPdfWrapper.NeedsPassword(new FileSource(filename)))
					{
						if (PasswordRequired == null)
							return;

						var args = new PasswordRequiredEventArgs();
						PasswordRequired(this, args);

						if (args.Cancel)
							return;

						pw = args.Password;
					}

					try
					{
						OpenFile(filename, pw);
					}
					catch (Exception ex)
					{
						MessageBox.Show(string.Format("An error occured: " + ex.Message));
					}
				}
			}
		}
	}

	public class PasswordRequiredEventArgs : EventArgs
	{
		public string Password { get; set; }
		public bool Cancel { get; set; }
	}
}
