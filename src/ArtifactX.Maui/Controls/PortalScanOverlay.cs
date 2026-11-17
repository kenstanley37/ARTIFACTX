using ArtifactX.PortalScanning;

namespace ArtifactX.Maui.Controls;

/// <summary>Draws and animates camera framing brackets; never samples or modifies camera pixels.</summary>
public sealed class PortalScanOverlay : GraphicsView, IDrawable
{
    private static readonly RectF GuideRect = new((float)PortalScanTarget.Guide.X, (float)PortalScanTarget.Guide.Y,
        (float)PortalScanTarget.Guide.Width, (float)PortalScanTarget.Guide.Height);
    private RectF _rendered = GuideRect;
    private const string TrackAnimation = "PortalTargetTracking";

    public static readonly BindableProperty TargetProperty = BindableProperty.Create(nameof(Target), typeof(PortalScanTarget),
        typeof(PortalScanOverlay), null, propertyChanged: OnTargetChanged);
    public static readonly BindableProperty IsLockedProperty = BindableProperty.Create(nameof(IsLocked), typeof(bool),
        typeof(PortalScanOverlay), false, propertyChanged: Redraw);
    public static readonly BindableProperty GuideColorProperty = BindableProperty.Create(nameof(GuideColor), typeof(Color),
        typeof(PortalScanOverlay), Colors.White, propertyChanged: Redraw);
    public static readonly BindableProperty TrackingColorProperty = BindableProperty.Create(nameof(TrackingColor), typeof(Color),
        typeof(PortalScanOverlay), Colors.White, propertyChanged: Redraw);
    public static readonly BindableProperty LockColorProperty = BindableProperty.Create(nameof(LockColor), typeof(Color),
        typeof(PortalScanOverlay), Colors.White, propertyChanged: Redraw);

    public PortalScanTarget? Target { get => (PortalScanTarget?)GetValue(TargetProperty); set => SetValue(TargetProperty, value); }
    public bool IsLocked { get => (bool)GetValue(IsLockedProperty); set => SetValue(IsLockedProperty, value); }
    public Color GuideColor { get => (Color)GetValue(GuideColorProperty); set => SetValue(GuideColorProperty, value); }
    public Color TrackingColor { get => (Color)GetValue(TrackingColorProperty); set => SetValue(TrackingColorProperty, value); }
    public Color LockColor { get => (Color)GetValue(LockColorProperty); set => SetValue(LockColorProperty, value); }

    public PortalScanOverlay()
    {
        Drawable = this;
        InputTransparent = true;
    }

    private static void Redraw(BindableObject bindable, object oldValue, object newValue) => ((PortalScanOverlay)bindable).Invalidate();

    private static void OnTargetChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var view = (PortalScanOverlay)bindable;
        var from = view._rendered;
        var to = newValue is PortalScanTarget target
            ? new RectF((float)target.X, (float)target.Y, (float)target.Width, (float)target.Height)
            : GuideRect;
        view.AbortAnimation(TrackAnimation);
        view.Animate(TrackAnimation, amount =>
        {
            float t = (float)amount;
            view._rendered = new(from.X + (to.X - from.X) * t, from.Y + (to.Y - from.Y) * t,
                from.Width + (to.Width - from.Width) * t, from.Height + (to.Height - from.Height) * t);
            view.Invalidate();
        }, length: 160, easing: Easing.CubicOut);
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        float left = Math.Clamp(_rendered.X * dirtyRect.Width, 4, Math.Max(4, dirtyRect.Width - 4));
        float top = Math.Clamp(_rendered.Y * dirtyRect.Height, 4, Math.Max(4, dirtyRect.Height - 4));
        float right = Math.Clamp((_rendered.X + _rendered.Width) * dirtyRect.Width, left, Math.Max(left, dirtyRect.Width - 4));
        float bottom = Math.Clamp((_rendered.Y + _rendered.Height) * dirtyRect.Height, top, Math.Max(top, dirtyRect.Height - 4));
        float corner = Math.Min(28, Math.Min(right - left, bottom - top) / 3);
        var brackets = new PathF();
        brackets.MoveTo(left, top + corner); brackets.LineTo(left, top); brackets.LineTo(left + corner, top);
        brackets.MoveTo(right - corner, top); brackets.LineTo(right, top); brackets.LineTo(right, top + corner);
        brackets.MoveTo(right, bottom - corner); brackets.LineTo(right, bottom); brackets.LineTo(right - corner, bottom);
        brackets.MoveTo(left + corner, bottom); brackets.LineTo(left, bottom); brackets.LineTo(left, bottom - corner);
        canvas.StrokeSize = 7;
        canvas.StrokeColor = Colors.Black.WithAlpha(0.8f);
        canvas.DrawPath(brackets);
        canvas.StrokeSize = 3;
        canvas.StrokeColor = IsLocked ? LockColor : Target is not null ? TrackingColor : GuideColor;
        canvas.DrawPath(brackets);
    }
}
