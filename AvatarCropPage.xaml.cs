using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using AzurChat.Services;
namespace AzurChat.Pages;

public partial class AvatarCropPage : ContentPage
{
    private SKBitmap? _bitmap;
    private readonly IAuthService _authService;

    private float _scale = 1f;
    private float _minScale = 1f;

    private float _offsetX;
    private float _offsetY;

    private float _circleX;
    private float _circleY;
    private float _radius;

    private readonly Dictionary<long, SKPoint> _touches = new();

    private float _lastDistance;
    private SKPoint _lastCenter;

    private bool _initialized;
    

    public AvatarCropPage(FileResult file, IAuthService authService)
    {
        InitializeComponent();

        _authService = authService;
        _ = LoadImageAsync(file);
    }

    public AvatarCropPage(string imageUri, IAuthService authService)
    {
        InitializeComponent();

        _authService = authService;
        _ = LoadUriAsync(imageUri);
    }

    private async Task LoadImageAsync(FileResult file)
    {
        try
        {
            await using var stream = await file.OpenReadAsync();

            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);

            var bitmap = SKBitmap.Decode(memory.ToArray());

            if (bitmap == null)
                throw new Exception("无法解析图片");

            _bitmap?.Dispose();
            _bitmap = bitmap;

            _initialized = false;

            CropCanvas.InvalidateSurface();
        }
        catch (Exception ex)
        {
            await DisplayAlert("图片加载失败", ex.Message, "确定");
        }
    }

    private async Task LoadUriAsync(string uri)
    {
#if ANDROID
        try
        {
            var context = Platform.AppContext;

            using var stream = context.ContentResolver!
                .OpenInputStream(Android.Net.Uri.Parse(uri));

            if (stream == null)
                throw new Exception("无法读取图片");

            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);

            var bitmap = SKBitmap.Decode(memory.ToArray());

            if (bitmap == null)
                throw new Exception("无法解析图片");

            _bitmap?.Dispose();
            _bitmap = bitmap;

            _initialized = false;

            CropCanvas.InvalidateSurface();
        }
        catch (Exception ex)
        {
            await DisplayAlert("图片加载失败", ex.Message, "确定");
        }
#endif
    }

    private void CropCanvas_PaintSurface(
        object sender,
        SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var info = e.Info;

        canvas.Clear(new SKColor(23, 25, 30));

        float width = info.Width;
        float height = info.Height;

        _circleX = width / 2f;
        _circleY = height / 2f;

        _radius = Math.Min(width * 0.42f, height * 0.35f);

        if (_bitmap == null)
            return;

        if (!_initialized)
        {
            _minScale = Math.Max(
                _radius * 2f / _bitmap.Width,
                _radius * 2f / _bitmap.Height);

            _scale = _minScale;

            _offsetX = 0;
            _offsetY = 0;

            _initialized = true;
        }

        ClampOffsets();

        canvas.Save();

        using (var circle = new SKPath())
        {
            circle.AddCircle(_circleX, _circleY, _radius);

            canvas.ClipPath(circle, SKClipOperation.Intersect, true);
        }

        DrawBitmap(canvas, _circleX, _circleY);

        canvas.Restore();

        using var borderPaint = new SKPaint
        {
            Color = SKColors.White,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3,
            IsAntialias = true
        };

        canvas.DrawCircle(
            _circleX,
            _circleY,
            _radius,
            borderPaint);
    }

    private void DrawBitmap(
        SKCanvas canvas,
        float centerX,
        float centerY)
    {
        if (_bitmap == null)
            return;

        float drawWidth = _bitmap.Width * _scale;
        float drawHeight = _bitmap.Height * _scale;

        float x = centerX - drawWidth / 2f + _offsetX;
        float y = centerY - drawHeight / 2f + _offsetY;

        var rect = new SKRect(
            x,
            y,
            x + drawWidth,
            y + drawHeight);

        using var paint = new SKPaint
        {
            IsAntialias = true
        };

        canvas.DrawBitmap(
            _bitmap,
            rect,
            new SKSamplingOptions(
                SKFilterMode.Linear,
                SKMipmapMode.Linear),
            paint);

    }

    private void ClampOffsets()
    {
        if (_bitmap == null)
            return;

        float halfWidth = _bitmap.Width * _scale / 2f;
        float halfHeight = _bitmap.Height * _scale / 2f;

        float maxX = Math.Max(0, halfWidth - _radius);
        float maxY = Math.Max(0, halfHeight - _radius);

        _offsetX = Math.Clamp(_offsetX, -maxX, maxX);
        _offsetY = Math.Clamp(_offsetY, -maxY, maxY);
    }

    private void CropCanvas_Touch(
        object sender,
        SKTouchEventArgs e)
    {
        var point = e.Location;

        switch (e.ActionType)
        {
            case SKTouchAction.Pressed:

                _touches[e.Id] = point;
                ResetTouchReference();
                break;

            case SKTouchAction.Moved:

                if (!_touches.ContainsKey(e.Id))
                    break;

                if (_touches.Count == 1)
                {
                    var previous = _touches[e.Id];

                    _offsetX += point.X - previous.X;
                    _offsetY += point.Y - previous.Y;

                    _touches[e.Id] = point;
                }
                else if (_touches.Count == 2)
                {
                    _touches[e.Id] = point;

                    var points = _touches.Values.ToArray();

                    var center = new SKPoint(
                        (points[0].X + points[1].X) / 2f,
                        (points[0].Y + points[1].Y) / 2f);

                    float distance = Distance(points[0], points[1]);

                    if (_lastDistance > 0)
                    {
                        float newScale = Math.Clamp(
                            _scale * distance / _lastDistance,
                            _minScale,
                            _minScale * 5f);

                        float ratio = newScale / _scale;

                        // 缩放时尽量保持手指中心下的图片位置
                        _offsetX =
                            (center.X - _circleX) -
                            ((center.X - _circleX) - _offsetX) * ratio;

                        _offsetY =
                            (center.Y - _circleY) -
                            ((center.Y - _circleY) - _offsetY) * ratio;

                        _scale = newScale;

                        _offsetX += center.X - _lastCenter.X;
                        _offsetY += center.Y - _lastCenter.Y;
                    }

                    _lastDistance = distance;
                    _lastCenter = center;
                }

                ClampOffsets();
                CropCanvas.InvalidateSurface();
                break;

            case SKTouchAction.Released:
            case SKTouchAction.Cancelled:
                _touches.Remove(e.Id);
                ResetTouchReference();
                break;
        }

        e.Handled = true;
    }

    private void ResetTouchReference()
    {
        _lastDistance = 0;

        if (_touches.Count == 2)
        {
            var points = _touches.Values.ToArray();

            _lastCenter = new SKPoint(
                (points[0].X + points[1].X) / 2f,
                (points[0].Y + points[1].Y) / 2f);

            _lastDistance = Distance(points[0], points[1]);
        }
    }

    private static float Distance(SKPoint a, SKPoint b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;

        return MathF.Sqrt(dx * dx + dy * dy);
    }

    private void Reset_Clicked(
        object sender,
        EventArgs e)
    {
        _scale = _minScale;
        _offsetX = 0;
        _offsetY = 0;

        CropCanvas.InvalidateSurface();
    }

    private async void Confirm_Clicked(
        object sender,
        EventArgs e)
    {
        if (_bitmap == null || !_initialized || _radius <= 0)
            return;

        const int outputSize = 512;

        using var output = new SKBitmap(
            outputSize,
            outputSize,
            SKColorType.Rgba8888,
            SKAlphaType.Premul);

        using var canvas = new SKCanvas(output);

        canvas.Clear(SKColors.Transparent);

        float factor = outputSize / (_radius * 2f);

        canvas.Save();

        canvas.Scale(factor);

        canvas.Translate(
            _radius - _circleX,
            _radius - _circleY);

        using (var circle = new SKPath())
        {
            circle.AddCircle(
                _circleX,
                _circleY,
                _radius);

            canvas.ClipPath(
                circle,
                SKClipOperation.Intersect,
                true);
        }

        DrawBitmap(canvas, _circleX, _circleY);

        canvas.Restore();
        canvas.Flush();

        using var image = SKImage.FromBitmap(output);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);

        string path = Path.Combine(
            FileSystem.AppDataDirectory,
            $"avatar_{Guid.NewGuid():N}.png");

        await using (var stream = File.Create(path))
        {
            data.SaveTo(stream);
        }

        try
        {
            await _authService.UpdateAvatarAsync(path);

            var personPage = Navigation.NavigationStack
                .OfType<PersonPage>()
                .LastOrDefault();

            if (personPage == null)
                throw new InvalidOperationException("导航栈中找不到指挥官信息页面");

            var stack = Navigation.NavigationStack.ToList();

            int personIndex = stack.IndexOf(personPage);

            if (personIndex < 0)
                throw new InvalidOperationException("找不到指挥官信息页面");

            for (int i = stack.Count - 2; i > personIndex; i--)
            {
                Navigation.RemovePage(stack[i]);
            }

            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("头像保存失败", ex.Message, "确定");
        }
    }

    private async void Back_Tapped(
        object sender,
        TappedEventArgs e)
    {
        await Navigation.PopAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        _touches.Clear();
    }
}