using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace IT_viddil_monitoring;

// Rasterizes only the app's own ambient backdrop into a frozen blurred texture.
// Data changes cannot invalidate the cached glass or create a live render loop.
public sealed class GlassPanel : ContentControl
{
    public static readonly DependencyProperty BackdropSourceProperty = DependencyProperty.Register(
        nameof(BackdropSource), typeof(FrameworkElement), typeof(GlassPanel),
        new PropertyMetadata(null, OnBackdropChanged));
    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
        nameof(CornerRadius), typeof(CornerRadius), typeof(GlassPanel), new PropertyMetadata(new CornerRadius(28)));
    public static readonly DependencyProperty BackdropOpacityProperty = DependencyProperty.Register(
        nameof(BackdropOpacity), typeof(double), typeof(GlassPanel), new PropertyMetadata(1d, OnBackdropChanged));

    public FrameworkElement? BackdropSource
    {
        get => (FrameworkElement?)GetValue(BackdropSourceProperty);
        set => SetValue(BackdropSourceProperty, value);
    }
    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }
    public double BackdropOpacity
    {
        get => (double)GetValue(BackdropOpacityProperty);
        set => SetValue(BackdropOpacityProperty, value);
    }

    private Border? _backdrop;
    private Grid? _clip;
    private Rect _lastViewbox;
    private Size _lastSize;
    private Size _lastSourceSize;
    private CornerRadius _lastRadius;
    private DpiScale _lastDpi;
    private bool _dirty = true;

    public GlassPanel()
    {
        LayoutUpdated += (_, _) => UpdateBackdrop();
        Loaded += (_, _) => RefreshBackdrop();
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _backdrop = GetTemplateChild("PART_Backdrop") as Border;
        _clip = GetTemplateChild("PART_Clip") as Grid;
        _dirty = true;
        UpdateBackdrop();
    }

    private static void OnBackdropChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) =>
        ((GlassPanel)sender).RefreshBackdrop();

    public void RefreshBackdrop()
    {
        _dirty = true;
        UpdateBackdrop();
        // Resolve a theme changed during layout once, without a repeating timer.
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(UpdateBackdrop));
    }

    private void UpdateBackdrop()
    {
        if (_clip is null || ActualWidth <= 0 || ActualHeight <= 0) return;
        var size = new Size(ActualWidth, ActualHeight);
        if (size != _lastSize || CornerRadius != _lastRadius)
        {
            _clip.Clip = new RectangleGeometry(new Rect(size), CornerRadius.TopLeft, CornerRadius.TopLeft);
            _lastSize = size;
            _lastRadius = CornerRadius;
        }
        if (_backdrop is null || BackdropSource is null || BackdropSource.ActualWidth <= 0) return;
        try
        {
            if (BackdropOpacity <= 0)
            {
                _backdrop.Background = Brushes.Transparent;
                _dirty = true;
                return;
            }
            var origin = TransformToVisual(BackdropSource).Transform(new Point());
            var box = new Rect(origin, size);
            var sourceSize = new Size(BackdropSource.ActualWidth, BackdropSource.ActualHeight);
            var dpi = VisualTreeHelper.GetDpi(this);
            if (_dirty || box != _lastViewbox || sourceSize != _lastSourceSize || dpi.DpiScaleX != _lastDpi.DpiScaleX || dpi.DpiScaleY != _lastDpi.DpiScaleY)
            {
                const double padding = 18;
                var sampledBox = box;
                sampledBox.Inflate(padding, padding);
                var sample = new VisualBrush(BackdropSource)
                {
                    ViewboxUnits = BrushMappingMode.Absolute,
                    Viewbox = sampledBox,
                    Stretch = Stretch.Fill
                };
                var drawing = new DrawingVisual
                {
                    Effect = new BlurEffect { Radius = padding, KernelType = KernelType.Gaussian, RenderingBias = RenderingBias.Performance }
                };
                using (var context = drawing.RenderOpen())
                    context.DrawRectangle(sample, null, new Rect(-padding, -padding, size.Width + padding * 2, size.Height + padding * 2));
                var bitmap = new RenderTargetBitmap(
                    Math.Max(1, (int)Math.Ceiling(size.Width * dpi.DpiScaleX)),
                    Math.Max(1, (int)Math.Ceiling(size.Height * dpi.DpiScaleY)),
                    dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
                bitmap.Render(drawing);
                bitmap.Freeze();
                var texture = new ImageBrush(bitmap) { Stretch = Stretch.Fill };
                texture.Freeze();
                _backdrop.Background = texture;
                _lastViewbox = box;
                _lastSourceSize = sourceSize;
                _lastDpi = dpi;
                _dirty = false;
            }
        }
        catch (InvalidOperationException) { } // The two visuals are not attached yet.
    }
}
