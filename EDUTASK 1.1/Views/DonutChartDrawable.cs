using EDUTASK_1._1.Helpers;

namespace EDUTASK_1._1.Views;

public sealed class DonutChartDrawable : IDrawable
{
    // The chart fills, not the status foregrounds. See the CHART block in
    // Colors.xaml for why a wedge and a badge cannot share a colour.
    private static readonly Color CompletedColor = AppColors.ChartCompleted;
    private static readonly Color PendingColor = AppColors.ChartOngoing;
    private static readonly Color OverdueColor = AppColors.ChartOverdue;
    private static readonly Color EmptyColor = AppColors.ChartEmpty;

    public int Completed { get; set; }
    public int Pending { get; set; }
    public int Overdue { get; set; }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        const float thickness = 14;
        float radius = (Math.Min(dirtyRect.Width, dirtyRect.Height) - thickness) / 2;
        float centerX = dirtyRect.Center.X;
        float centerY = dirtyRect.Center.Y;
        var bounds = new RectF(centerX - radius, centerY - radius, radius * 2, radius * 2);

        canvas.StrokeSize = thickness;
        canvas.StrokeLineCap = LineCap.Butt;

        int total = Completed + Pending + Overdue;
        if (total <= 0)
        {
            canvas.StrokeColor = EmptyColor;
            canvas.DrawEllipse(bounds);
            return;
        }

        float cursor = 90; // 12 o'clock
        DrawSegment(canvas, bounds, ref cursor, Completed, total, CompletedColor);
        DrawSegment(canvas, bounds, ref cursor, Pending, total, PendingColor);
        DrawSegment(canvas, bounds, ref cursor, Overdue, total, OverdueColor);
    }

    private static void DrawSegment(ICanvas canvas, RectF bounds, ref float cursor, int value, int total, Color color)
    {
        if (value <= 0)
            return;

        float sweep = 360f * value / total;
        canvas.StrokeColor = color;
        canvas.DrawArc(bounds.X, bounds.Y, bounds.Width, bounds.Height, cursor, cursor - sweep, true, false);
        cursor -= sweep;
    }
}
