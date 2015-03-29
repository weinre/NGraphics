﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SharpDX;
using D3D = SharpDX.Direct3D;
using D3D11 = SharpDX.Direct3D11;
using D2D1 = SharpDX.Direct2D1;
using DXGI = SharpDX.DXGI;
using WIC = SharpDX.WIC;

namespace NGraphics
{
	public class WicRenderTargetCanvas : RenderTargetCanvas, IImageCanvas
	{
		protected readonly WIC.Bitmap Bmp;
		readonly Size size;
		readonly double scale;
		readonly Direct2DFactories factories;

		public WicRenderTargetCanvas (Size size, double scale = 1.0, bool transparency = true, Direct2DFactories factories = null)
			: this (
				new WIC.Bitmap ((factories ?? Direct2DFactories.Shared).WicFactory, (int)(Math.Ceiling (size.Width * scale)), (int)(Math.Ceiling (size.Height * scale)), SharpDX.WIC.PixelFormat.Format32bppBGR, WIC.BitmapCreateCacheOption.CacheOnLoad),
				new D2D1.RenderTargetProperties (D2D1.RenderTargetType.Default, new D2D1.PixelFormat (DXGI.Format.Unknown, D2D1.AlphaMode.Unknown), (float)(160.0 * scale), (float)(160.0 * scale), D2D1.RenderTargetUsage.None, D2D1.FeatureLevel.Level_DEFAULT))
		{
		}

		public WicRenderTargetCanvas (WIC.Bitmap bmp, D2D1.RenderTargetProperties properties, Direct2DFactories factories = null)
			: base (new D2D1.WicRenderTarget ((factories ?? Direct2DFactories.Shared).D2DFactory, bmp, properties))
		{
			this.Bmp = bmp;
			this.scale = properties.DpiX / 160.0;
			var bmpSize = bmp.Size;
			this.size = new Size (bmpSize.Width, bmpSize.Height);
			this.factories = factories ?? Direct2DFactories.Shared;
		}

		public Task<IImage> GetImageAsync ()
		{
			renderTarget.EndDraw ();
			return Task.FromResult<IImage> (new WicBitmapImage (Bmp, factories));
		}

		public Size Size
		{
			get { return size; }
		}

		public double Scale
		{
			get { return scale; }
		}
	}

	public class WicBitmapImage : IImage
	{
		readonly WIC.Bitmap bmp;
		readonly Direct2DFactories factories;

		public WicBitmapImage (WIC.Bitmap bmp, Direct2DFactories factories = null)
		{
			if (bmp == null)
				throw new ArgumentNullException ("bmp");
			this.bmp = bmp;
			this.factories = factories ?? Direct2DFactories.Shared;
		}

		public void SaveAsPng (string path)
		{
			throw new NotSupportedException ("WinRT does not support saving to files. Please use the Stream override instead.");
		}

		public Task SaveAsPngAsync (System.IO.Stream stream)
		{
			return Task.Run (() => SaveAsPng (stream));
		}

		void SaveAsPng (System.IO.Stream stream)
		{
			using (var encoder = new WIC.PngBitmapEncoder (factories.WicFactory, stream)) {
				using (var bitmapFrameEncode = new WIC.BitmapFrameEncode (encoder)) {
					bitmapFrameEncode.Initialize ();
					var size = bmp.Size;
					bitmapFrameEncode.SetSize (size.Width, size.Height);
					var pf = bmp.PixelFormat;
					bitmapFrameEncode.SetPixelFormat (ref pf);

					bitmapFrameEncode.WriteSource (bmp);

					bitmapFrameEncode.Commit ();
					encoder.Commit ();
				}
			}
		}
	}

	/// <summary>
	/// ICanvas wrapper over a Direct2D RenderTarget.
	/// </summary>
	public class RenderTargetCanvas : ICanvas
	{
		protected readonly D2D1.RenderTarget renderTarget;
		readonly Direct2DFactories factories;
		readonly Stack<D2D1.DrawingStateBlock> stateStack = new Stack<D2D1.DrawingStateBlock> ();

		public RenderTargetCanvas (DXGI.Surface surface, D2D1.RenderTargetProperties properties, Direct2DFactories factories = null)
		{
			if (surface == null)
				throw new ArgumentNullException ("surface");
			this.factories = factories ?? Direct2DFactories.Shared;
			this.renderTarget = new D2D1.RenderTarget (this.factories.D2DFactory, surface, properties);
			renderTarget.BeginDraw ();
		}

		public RenderTargetCanvas (D2D1.RenderTarget renderTarget, Direct2DFactories factories = null)
		{
			if (renderTarget == null)
				throw new ArgumentNullException ("renderTarget");
			this.factories = factories ?? Direct2DFactories.Shared;
			this.renderTarget = renderTarget;
			renderTarget.BeginDraw ();
		}

		public void SaveState ()
		{
			var s = new D2D1.DrawingStateBlock (factories.D2DFactory);
			renderTarget.SaveDrawingState (s);
			stateStack.Push (s);
		}

		public void Transform (Transform transform)
		{
			// TODO: Implement Transform
		}

		public void RestoreState ()
		{
			if (stateStack.Count > 0) {
				var s = stateStack.Pop ();
				renderTarget.RestoreDrawingState (s);
			}
		}

		public void DrawText (string text, Rect frame, Font font, TextAlignment alignment = TextAlignment.Left, Pen pen = null, Brush brush = null)
		{
			DrawRectangle (frame, pen, brush);
		}

		public void DrawPath (IEnumerable<PathOp> ops, Pen pen = null, Brush brush = null)
		{
		}

		public void DrawRectangle (Rect frame, Pen pen = null, Brush brush = null)
		{
			var p = GetBrush (pen);
			var b = GetBrush (brush);
			if (b != null) {
				renderTarget.FillRectangle (frame.ToRectangleF (), b);
			}
			if (p != null) {
				renderTarget.DrawRectangle (frame.ToRectangleF (), b, (float)pen.Width);
			}
		}

		public void DrawEllipse (Rect frame, Pen pen = null, Brush brush = null)
		{
			DrawRectangle (frame, pen, brush);
		}

		public void DrawImage (IImage image, Rect frame, double alpha = 1.0)
		{
			DrawRectangle (frame, null, Brushes.Red);
		}

		D2D1.Brush GetBrush (Pen pen)
		{
			if (pen == null) return null;
			return new D2D1.SolidColorBrush (renderTarget, pen.Color.ToColor4 ());
		}

		D2D1.Brush GetBrush (Brush brush)
		{
			if (brush == null) return null;
			var sb = brush as SolidBrush;
			if (sb != null) {
				return new D2D1.SolidColorBrush (renderTarget, sb.Color.ToColor4 ());
			}
			else {
				// TODO: Gradient brushes
				return new D2D1.SolidColorBrush (renderTarget, Colors.Green.ToColor4 ());
			}
		}
	}

	public class Direct2DFactories
	{
		public readonly SharpDX.WIC.ImagingFactory WicFactory;
		public readonly D2D1.Factory D2DFactory;
		public readonly SharpDX.DirectWrite.Factory _dWriteFactory;
		public readonly D2D1.DeviceContext _d2DDeviceContext;

		public static readonly Direct2DFactories Shared = new Direct2DFactories ();

		public Direct2DFactories ()
		{
			WicFactory = new SharpDX.WIC.ImagingFactory ();
			_dWriteFactory = new SharpDX.DirectWrite.Factory ();

			var d3DDevice = new D3D11.Device (
				D3D.DriverType.Hardware,
				D3D11.DeviceCreationFlags.BgraSupport
#if DEBUG
 | D3D11.DeviceCreationFlags.Debug
#endif
,
				D3D.FeatureLevel.Level_11_1,
				D3D.FeatureLevel.Level_11_0,
				D3D.FeatureLevel.Level_10_1,
				D3D.FeatureLevel.Level_10_0,
				D3D.FeatureLevel.Level_9_3,
				D3D.FeatureLevel.Level_9_2,
				D3D.FeatureLevel.Level_9_1
				);

			var dxgiDevice = ComObject.As<SharpDX.DXGI.Device> (d3DDevice.NativePointer);
			var d2DDevice = new D2D1.Device (dxgiDevice);
			D2DFactory = d2DDevice.Factory;
			_d2DDeviceContext = new D2D1.DeviceContext (d2DDevice, D2D1.DeviceContextOptions.None);
			var dpi = DisplayDpi;
			_d2DDeviceContext.DotsPerInch = new Size2F (dpi, dpi);
		}

		static float DisplayDpi
		{
			get
			{
				return Windows.Graphics.Display.DisplayInformation.GetForCurrentView ().LogicalDpi;
			}
		}
	}

	public static partial class Conversions
	{
		public static Color4 ToColor4 (this Color color)
		{
			return new Color4 (color.Abgr);
		}

		public static RectangleF ToRectangleF (this Rect rect)
		{
			return new RectangleF ((float)rect.X, (float)rect.Y, (float)rect.Width, (float)rect.Height);
		}
	}
}
