using Microsoft.Maui.Graphics;

namespace ArtifactX.Maui.Controls;

/// <summary>
/// Custom-drawn vector ArtifactX Hexagon + X mark (matching AppIconMark.png).
/// When animated on the Loading screen, it progressively traces the outer hexagon
/// with dual laser tips, slices the cyan ghost-offset X-arms across the core,
/// sweeps a holographic scanline across the emblem, and locks into the final mark.
/// </summary>
public sealed class AnimatedLogoView : GraphicsView, IDrawable
{
    public static readonly BindableProperty ProgressProperty =
        BindableProperty.Create(
            nameof(Progress),
            typeof(double),
            typeof(AnimatedLogoView),
            1.0,
            propertyChanged: (bindable, _, _) => ((AnimatedLogoView)bindable).Invalidate());

    public static readonly BindableProperty PrimaryColorProperty =
        BindableProperty.Create(
            nameof(PrimaryColor),
            typeof(Color),
            typeof(AnimatedLogoView),
            Color.FromArgb("#FF9F2E"),
            propertyChanged: (bindable, _, _) => ((AnimatedLogoView)bindable).Invalidate());

    public static readonly BindableProperty CyanGhostColorProperty =
        BindableProperty.Create(
            nameof(CyanGhostColor),
            typeof(Color),
            typeof(AnimatedLogoView),
            Color.FromArgb("#39D0FF"),
            propertyChanged: (bindable, _, _) => ((AnimatedLogoView)bindable).Invalidate());

    /// <summary>Animation progress from 0.0 (blank) to 1.0 (fully drawn mark).</summary>
    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public Color PrimaryColor
    {
        get => (Color)GetValue(PrimaryColorProperty);
        set => SetValue(PrimaryColorProperty, value);
    }

    public Color CyanGhostColor
    {
        get => (Color)GetValue(CyanGhostColorProperty);
        set => SetValue(CyanGhostColorProperty, value);
    }

    public AnimatedLogoView()
    {
        Drawable = this;
        BackgroundColor = Colors.Transparent;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        float size = Math.Min(dirtyRect.Width, dirtyRect.Height);
        if (size <= 4) return;

        float cx = dirtyRect.Center.X;
        float cy = dirtyRect.Center.Y;
        float r = size * 0.43f;
        float strokeWidth = Math.Max(2.0f, size * 0.078f);

        double p = Math.Clamp(Progress, 0.0, 1.0);

        // 6 vertices of our point-top hexagon:
        // V0: Top, V1: Top-Right, V2: Bottom-Right, V3: Bottom, V4: Bottom-Left, V5: Top-Left
        PointF[] hex = new PointF[6];
        for (int i = 0; i < 6; i++)
        {
            double angleRad = (i * 60.0 - 90.0) * Math.PI / 180.0;
            hex[i] = new PointF(
                cx + (float)(r * Math.Cos(angleRad)),
                cy + (float)(r * Math.Sin(angleRad)));
        }

        canvas.SaveState();
        canvas.Antialias = true;

        // Stage 1 (0.00 -> 0.45): Dual-beam trace of the outer Hexagon from V0 (Top) down both sides to V3 (Bottom)
        double hexProgress = Math.Clamp(p / 0.45, 0.0, 1.0);
        if (hexProgress >= 0.995)
        {
            // Draw as a single closed polygon so all 6 miter corners are razor-sharp
            var closedHex = new PathF();
            closedHex.MoveTo(hex[0]);
            for (int i = 1; i < 6; i++)
                closedHex.LineTo(hex[i]);
            closedHex.Close();

            canvas.StrokeColor = PrimaryColor;
            canvas.StrokeSize = strokeWidth;
            canvas.StrokeLineJoin = LineJoin.Miter;
            canvas.MiterLimit = 4f;
            canvas.DrawPath(closedHex);
        }
        else if (hexProgress > 0)
        {
            PointF? rightTip = DrawPolylineProgress(canvas, [hex[0], hex[1], hex[2], hex[3]], (float)hexProgress, PrimaryColor, strokeWidth);
            PointF? leftTip = DrawPolylineProgress(canvas, [hex[0], hex[5], hex[4], hex[3]], (float)hexProgress, PrimaryColor, strokeWidth);

            DrawLaserSpark(canvas, rightTip, CyanGhostColor, strokeWidth * 0.85f);
            DrawLaserSpark(canvas, leftTip, CyanGhostColor, strokeWidth * 0.85f);
        }

        // Stage 2 (0.36 -> 0.78): Flat-ended geometric X silhouette + cyan ghost inner edges (matching AppIconMark.png)
        double xProgress = Math.Clamp((p - 0.36) / 0.42, 0.0, 1.0);
        if (xProgress > 0)
        {
            float halfW = r * 0.48f;
            float yTop = cy - r * 0.43f;
            float yBot = cy + r * 0.43f;
            float armW = r * 0.25f;
            float ghost = Math.Max(1.2f, size * 0.024f);

            // Top-Left to Bottom-Right parallelogram bar (with flat horizontal top & bottom caps)
            float slash1 = (float)Math.Clamp(xProgress / 0.55, 0.0, 1.0);
            if (slash1 > 0)
            {
                PointF tlOuter = new(cx - halfW, yTop);
                PointF tlInner = new(cx - halfW + armW, yTop);
                PointF brInner = Lerp(tlOuter, new(cx + halfW - armW, yBot), slash1);
                PointF brOuter = Lerp(tlInner, new(cx + halfW, yBot), slash1);

                // Cyan ghost offset (only on full-size mark >= 36px, per AppIcon.ico design rule)
                if (size >= 36f)
                {
                    FillQuad(canvas,
                        new(tlOuter.X + ghost, tlOuter.Y),
                        new(tlInner.X + ghost, tlInner.Y),
                        new(brOuter.X + ghost, brOuter.Y),
                        new(brInner.X + ghost, brInner.Y),
                        CyanGhostColor);
                }

                FillQuad(canvas, tlOuter, tlInner, brOuter, brInner, PrimaryColor);

                if (slash1 < 0.99f)
                    DrawLaserSpark(canvas, Lerp(brInner, brOuter, 0.5f), CyanGhostColor, strokeWidth * 0.7f);
            }

            // Top-Right to Bottom-Left parallelogram bar (with flat horizontal top & bottom caps)
            float slash2 = (float)Math.Clamp((xProgress - 0.35) / 0.65, 0.0, 1.0);
            if (slash2 > 0)
            {
                PointF trInner = new(cx + halfW - armW, yTop);
                PointF trOuter = new(cx + halfW, yTop);
                PointF blOuter = Lerp(trInner, new(cx - halfW, yBot), slash2);
                PointF blInner = Lerp(trOuter, new(cx - halfW + armW, yBot), slash2);

                if (size >= 36f)
                {
                    FillQuad(canvas,
                        new(trInner.X - ghost, trInner.Y),
                        new(trOuter.X - ghost, trOuter.Y),
                        new(blInner.X - ghost, blInner.Y),
                        new(blOuter.X - ghost, blOuter.Y),
                        CyanGhostColor);
                }

                FillQuad(canvas, trInner, trOuter, blInner, blOuter, PrimaryColor);

                if (slash2 < 0.99f)
                    DrawLaserSpark(canvas, Lerp(blOuter, blInner, 0.5f), CyanGhostColor, strokeWidth * 0.7f);
            }
        }

        // Stage 3 (0.68 -> 0.94): Horizontal holographic scanline sweep across the hexagon
        if (p >= 0.68 && p <= 0.95)
        {
            float scanT = (float)((p - 0.68) / 0.27);
            float scanY = (cy - r) + (2 * r * scanT);
            float alpha = (float)Math.Sin(scanT * Math.PI) * 0.85f;
            canvas.StrokeColor = CyanGhostColor.WithAlpha(alpha);
            canvas.StrokeSize = Math.Max(1.5f, size * 0.018f);
            canvas.DrawLine(cx - r * 0.82f, scanY, cx + r * 0.82f, scanY);
        }

        canvas.RestoreState();
    }

    private static void FillQuad(ICanvas canvas, PointF p0, PointF p1, PointF p2, PointF p3, Color color)
    {
        var path = new PathF();
        path.MoveTo(p0);
        path.LineTo(p1);
        path.LineTo(p2);
        path.LineTo(p3);
        path.Close();
        canvas.FillColor = color;
        canvas.FillPath(path);
    }

    private static PointF? DrawPolylineProgress(
        ICanvas canvas,
        PointF[] pts,
        float progress,
        Color color,
        float strokeWidth)
    {
        if (pts.Length < 2 || progress <= 0) return null;

        float totalLen = 0f;
        float[] segLens = new float[pts.Length - 1];
        for (int i = 0; i < pts.Length - 1; i++)
        {
            segLens[i] = Distance(pts[i], pts[i + 1]);
            totalLen += segLens[i];
        }

        float targetLen = totalLen * Math.Clamp(progress, 0f, 1f);
        float walked = 0f;

        var path = new PathF();
        path.MoveTo(pts[0]);
        PointF tip = pts[0];

        for (int i = 0; i < segLens.Length; i++)
        {
            float seg = segLens[i];
            if (walked + seg <= targetLen)
            {
                path.LineTo(pts[i + 1]);
                tip = pts[i + 1];
                walked += seg;
            }
            else
            {
                float remain = targetLen - walked;
                float t = seg > 0 ? remain / seg : 0f;
                tip = Lerp(pts[i], pts[i + 1], t);
                path.LineTo(tip);
                break;
            }
        }

        canvas.StrokeColor = color;
        canvas.StrokeSize = strokeWidth;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeLineJoin = LineJoin.Miter;
        canvas.DrawPath(path);
        return tip;
    }

    private static void DrawLaserSpark(ICanvas canvas, PointF? pt, Color sparkColor, float radius)
    {
        if (pt is not { } p) return;
        canvas.FillColor = sparkColor.WithAlpha(0.38f);
        canvas.FillCircle(p.X, p.Y, radius * 1.9f);
        canvas.FillColor = Colors.White;
        canvas.FillCircle(p.X, p.Y, radius * 0.75f);
    }

    private static float Distance(PointF a, PointF b)
    {
        float dx = b.X - a.X;
        float dy = b.Y - a.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    private static PointF Lerp(PointF a, PointF b, float t) =>
        new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
}
