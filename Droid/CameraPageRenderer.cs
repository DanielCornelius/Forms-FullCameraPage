using System;
using Android.App;
using Android.Graphics;
using Android.Views;
using Xamarin.Forms.Platform.Android;
using Android.Widget;
using Android.Content;
using FullCameraPage;
using System.Linq;
using System.Threading.Tasks;
using Android.Runtime;
using FullCameraPage.Droid;
using Camera = Android.Hardware.Camera;

[assembly: Xamarin.Forms.ExportRenderer(typeof(CameraPage), typeof(CameraPageRenderer))]
namespace FullCameraPage.Droid
{
	public class CameraPageRenderer : PageRenderer, TextureView.ISurfaceTextureListener
	{
		private RelativeLayout _mainLayout;
		private TextureView _liveView;
		private PaintCodeButton _capturePhotoButton;
		private Camera _camera; // This one is deprecated from API Version 21, so in the future when we go up to 21 as minimum, we should switch to Camera2

		public Activity Activity => Context as Activity;

		protected override void OnElementChanged(ElementChangedEventArgs<Xamarin.Forms.Page> e)
		{
			base.OnElementChanged(e);
			SetupUserInterface();
			SetupEventHandlers();
		}

		private void SetupUserInterface()
		{
			_mainLayout = new RelativeLayout(Context);
			_liveView = new TextureView(Context);

			var liveViewParams = new RelativeLayout.LayoutParams(LayoutParams.MatchParent, LayoutParams.MatchParent);
			_liveView.LayoutParameters = liveViewParams;
			_mainLayout.AddView(_liveView);

			_capturePhotoButton = new PaintCodeButton(Context);
			var captureButtonParams = new RelativeLayout.LayoutParams(LayoutParams.WrapContent, LayoutParams.WrapContent)
			{
				Height = 120,
				Width = 120
			};
			_capturePhotoButton.LayoutParameters = captureButtonParams;
			_mainLayout.AddView(_capturePhotoButton);

			AddView(_mainLayout);
		}

		public void SetupEventHandlers()
		{
			_capturePhotoButton.Click += async (sender, e) =>
			{
				var bytes = await TakePhoto();
				var cameraPage = Element as CameraPage;
				cameraPage?.SetPhotoResult(bytes, _liveView.Bitmap.Width, _liveView.Bitmap.Height);
			};
			_liveView.SurfaceTextureListener = this;
		}

		protected override void OnLayout(bool changed, int l, int t, int r, int b)
		{
			base.OnLayout(changed, l, t, r, b);
			SetCameraDisplayOrientation();
			if (!changed)
				return;
			var msw = MeasureSpec.MakeMeasureSpec(r - l, MeasureSpecMode.Exactly);
			var msh = MeasureSpec.MakeMeasureSpec(b - t, MeasureSpecMode.Exactly);
			_mainLayout.Measure(msw, msh);
			_mainLayout.Layout(0, 0, r - l, b - t);

			_capturePhotoButton.SetX(_mainLayout.Width / 2 - 60);
			_capturePhotoButton.SetY(_mainLayout.Height - 200);
		}

		public void SetCameraDisplayOrientation()
		{
			if (_camera == null) return;
			var windowManager = Application.Context.GetSystemService(Context.WindowService).JavaCast<IWindowManager>();
			var rotation = windowManager.DefaultDisplay.Rotation;
			int degrees;
			switch (rotation)
			{
				case SurfaceOrientation.Rotation0:
					degrees = 90;
					break;
				case SurfaceOrientation.Rotation90:
					degrees = 0;
					break;
				case SurfaceOrientation.Rotation180:
					degrees = 270;
					break;
				case SurfaceOrientation.Rotation270:
					degrees = 180;
					break;
				default:
					degrees = 0;
					break;
			}
			
			_camera.SetDisplayOrientation(degrees);
		}

		public override bool OnKeyDown(Keycode keyCode, KeyEvent e)
		{
			if (keyCode == Keycode.Back)
			{
				var cameraPage = Element as CameraPage;
				cameraPage?.Cancel();
				return false;
			}
			return base.OnKeyDown(keyCode, e);
		}

		public async Task<byte[]> TakePhoto()
		{
			_camera.StopPreview();
			var ratio = (decimal)Height / Width;
			var image = Bitmap.CreateBitmap(_liveView.Bitmap, 0, 0, _liveView.Bitmap.Width, (int)(_liveView.Bitmap.Width * ratio));
			byte[] imageBytes;
			using (var imageStream = new System.IO.MemoryStream())
			{
				await image.CompressAsync(Bitmap.CompressFormat.Jpeg, 80, imageStream);
				image.Recycle();
				imageBytes = imageStream.ToArray();
			}
			_camera.StartPreview();
			return imageBytes;
		}

		private void StopCamera()
		{
			_camera.StopPreview();
			_camera.Release();
			GC.Collect();
			var mem = GC.GetTotalMemory(true);
			System.Diagnostics.Debug.WriteLine($"Memory: { mem }");
		}

		private void StartCamera()
		{
			var properties = _camera.GetParameters();
			// Set the camera to autofocus and autoflash
			properties.FocusMode = Camera.Parameters.FocusModeContinuousPicture;
			properties.FlashMode = Camera.Parameters.FlashModeAuto;
			_camera.SetParameters(properties);
			SetCameraDisplayOrientation();
			_camera.StartPreview();
		}

		#region TextureView.ISurfaceTextureListener implementations

		public void OnSurfaceTextureAvailable(SurfaceTexture surface, int width, int height)
		{
			_camera = Camera.Open();
			var parameters = _camera.GetParameters();
			var aspect = height / (decimal)width;

			// Find the preview aspect ratio that is closest to the surface aspect
			var previewSize = parameters.SupportedPreviewSizes
										.OrderBy(s => Math.Abs(s.Width / (decimal)s.Height - aspect))
										.First();

			System.Diagnostics.Debug.WriteLine($"Preview sizes: { parameters.SupportedPreviewSizes.Count }");

			parameters.SetPreviewSize(previewSize.Width, previewSize.Height);
			_camera.SetParameters(parameters);

			_camera.SetPreviewTexture(surface);
			StartCamera();
		}

		public bool OnSurfaceTextureDestroyed(SurfaceTexture surface)
		{
			StopCamera();
			return true;
		}

		public void OnSurfaceTextureSizeChanged(SurfaceTexture surface, int width, int height)
		{
		}

		public void OnSurfaceTextureUpdated(SurfaceTexture surface)
		{
		}

		#endregion
	}

	public class PaintCodeButton : Button
	{
		public PaintCodeButton(Context context) : base(context)
		{
			// ReSharper disable once VirtualMemberCallInContructor
			Background.Alpha = 0;
		}

		protected override void OnDraw(Canvas canvas)
		{
			var frame = new Rect(Left, Top, Right, Bottom);

			// Local Colors
			var color = Color.White;

			var bezierRect = new RectF(
				frame.Left + (float)Java.Lang.Math.Floor((frame.Width() - 120f) * 0.5f + 0.5f),
				frame.Top + (float)Java.Lang.Math.Floor((frame.Height() - 120f) * 0.5f + 0.5f),
				frame.Left + (float)Java.Lang.Math.Floor((frame.Width() - 120f) * 0.5f + 0.5f) + 120f,
				frame.Top + (float)Java.Lang.Math.Floor((frame.Height() - 120f) * 0.5f + 0.5f) + 120f);

			var bezierPath = new Path();
			bezierPath.MoveTo(frame.Left + frame.Width() * 0.5f, frame.Top + frame.Height() * 0.08333f);
			bezierPath.CubicTo(frame.Left + frame.Width() * 0.41628f, frame.Top + frame.Height() * 0.08333f, frame.Left + frame.Width() * 0.33832f, frame.Top + frame.Height() * 0.10803f, frame.Left + frame.Width() * 0.27302f, frame.Top + frame.Height() * 0.15053f);
			bezierPath.CubicTo(frame.Left + frame.Width() * 0.15883f, frame.Top + frame.Height() * 0.22484f, frame.Left + frame.Width() * 0.08333f, frame.Top + frame.Height() * 0.3536f, frame.Left + frame.Width() * 0.08333f, frame.Top + frame.Height() * 0.5f);
			bezierPath.CubicTo(frame.Left + frame.Width() * 0.08333f, frame.Top + frame.Height() * 0.73012f, frame.Left + frame.Width() * 0.26988f, frame.Top + frame.Height() * 0.91667f, frame.Left + frame.Width() * 0.5f, frame.Top + frame.Height() * 0.91667f);
			bezierPath.CubicTo(frame.Left + frame.Width() * 0.73012f, frame.Top + frame.Height() * 0.91667f, frame.Left + frame.Width() * 0.91667f, frame.Top + frame.Height() * 0.73012f, frame.Left + frame.Width() * 0.91667f, frame.Top + frame.Height() * 0.5f);
			bezierPath.CubicTo(frame.Left + frame.Width() * 0.91667f, frame.Top + frame.Height() * 0.26988f, frame.Left + frame.Width() * 0.73012f, frame.Top + frame.Height() * 0.08333f, frame.Left + frame.Width() * 0.5f, frame.Top + frame.Height() * 0.08333f);
			bezierPath.Close();
			bezierPath.MoveTo(frame.Left + frame.Width(), frame.Top + frame.Height() * 0.5f);
			bezierPath.CubicTo(frame.Left + frame.Width(), frame.Top + frame.Height() * 0.77614f, frame.Left + frame.Width() * 0.77614f, frame.Top + frame.Height(), frame.Left + frame.Width() * 0.5f, frame.Top + frame.Height());
			bezierPath.CubicTo(frame.Left + frame.Width() * 0.22386f, frame.Top + frame.Height(), frame.Left, frame.Top + frame.Height() * 0.77614f, frame.Left, frame.Top + frame.Height() * 0.5f);
			bezierPath.CubicTo(frame.Left, frame.Top + frame.Height() * 0.33689f, frame.Left + frame.Width() * 0.0781f, frame.Top + frame.Height() * 0.19203f, frame.Left + frame.Width() * 0.19894f, frame.Top + frame.Height() * 0.10076f);
			bezierPath.CubicTo(frame.Left + frame.Width() * 0.28269f, frame.Top + frame.Height() * 0.03751f, frame.Left + frame.Width() * 0.38696f, frame.Top, frame.Left + frame.Width() * 0.5f, frame.Top);
			bezierPath.CubicTo(frame.Left + frame.Width() * 0.77614f, frame.Top, frame.Left + frame.Width(), frame.Top + frame.Height() * 0.22386f, frame.Left + frame.Width(), frame.Top + frame.Height() * 0.5f);
			bezierPath.Close();

			var paint = new Paint();
			paint.SetStyle(Android.Graphics.Paint.Style.Fill);
			paint.Color = color;
			canvas.DrawPath(bezierPath, paint);

			paint = new Paint
			{
				StrokeWidth = 1f,
				StrokeMiter = 10f
			};
			canvas.Save();
			paint.SetStyle(Android.Graphics.Paint.Style.Stroke);
			paint.Color = Color.Black;
			canvas.DrawPath(bezierPath, paint);
			canvas.Restore();

			var ovalRect = new RectF(
				frame.Left + (float)Java.Lang.Math.Floor(frame.Width() * 0.12917f) + 0.5f,
				frame.Top + (float)Java.Lang.Math.Floor(frame.Height() * 0.12083f) + 0.5f,
				frame.Left + (float)Java.Lang.Math.Floor(frame.Width() * 0.87917f) + 0.5f,
				frame.Top + (float)Java.Lang.Math.Floor(frame.Height() * 0.87083f) + 0.5f);

			var ovalPath = new Path();
			ovalPath.AddOval(ovalRect, Path.Direction.Cw);

			paint = new Paint();
			paint.SetStyle(Android.Graphics.Paint.Style.Fill);
			paint.Color = color;
			canvas.DrawPath(ovalPath, paint);

			paint = new Paint
			{
				StrokeWidth = 1f,
				StrokeMiter = 10f
			};
			canvas.Save();
			paint.SetStyle(Android.Graphics.Paint.Style.Stroke);
			paint.Color = Color.Black;
			canvas.DrawPath(ovalPath, paint);
			canvas.Restore();
		}
	}
}