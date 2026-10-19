using System.Globalization;
using System.Text;

namespace Athermalize.Cli;

/// <summary>
/// A plain SVG chart: lines against a shared x axis, or horizontal bars. Charts stack into one file with
/// <see cref="Document"/>. No scripts and no fonts beyond the viewer's sans-serif, so a browser or a document opens it.
/// </summary>
internal sealed class SvgChart
{
    public const string Blue = "#1f5fa8", Red = "#c0392b", Green = "#2e8b57", Orange = "#d97b16", Purple = "#7d3c98", Gray = "#7f7f7f";
    public static readonly string[] Palette = { Blue, Red, Green, Orange, Purple, Gray };

    /// <param name="Label">Its name in the legend.</param>
    /// <param name="Points">Its points, in x order.</param>
    /// <param name="Dashed">Drawn dashed.</param>
    /// <param name="Markers">A dot at each point.</param>
    public sealed record Line(string Label, IReadOnlyList<(double X, double Y)> Points, string Color, bool Dashed = false, bool Markers = false);

    /// <summary>A shaded region between two curves over the same x.</summary>
    public sealed record Band(string Label, IReadOnlyList<(double X, double Low, double High)> Points, string Color);

    /// <summary>A horizontal line across the plot.</summary>
    public sealed record Level(string Label, double Y, string Color);

    public string Title { get; init; } = "";
    public string XLabel { get; init; } = "";
    public string YLabel { get; init; } = "";
    public List<Line> Lines { get; } = new();
    public List<Band> Bands { get; } = new();
    public List<Level> Levels { get; } = new();

    /// <summary>Horizontal bars, label and value; when there are any, the chart is a bar chart.</summary>
    public List<(string Label, double Value, string Color)> Bars { get; } = new();

    /// <summary>The y axis's lowest and highest values, when they shouldn't follow the data.</summary>
    public double? YMin { get; init; }
    public double? YMax { get; init; }

    private const double Width = 760, LineHeight = 380, BarRow = 26, BarHeader = 70, BarFooter = 50;

    private double Height => Bars.Count > 0 ? BarHeader + BarRow * Bars.Count + BarFooter : LineHeight + LegendRow * LegendRows;

    /// <summary>The charts, one under the other, as an SVG document.</summary>
    public static string Document(params SvgChart[] charts)
    {
        double h = charts.Sum(c => c.Height);
        var s = new StringBuilder();
        s.Append(F($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{Width}\" height=\"{h}\" viewBox=\"0 0 {Width} {h}\" font-family=\"sans-serif\" font-size=\"12\">\n"));
        s.Append("<rect width=\"100%\" height=\"100%\" fill=\"white\"/>\n");
        double y = 0;
        foreach (var c in charts)
        {
            s.Append(F($"<g transform=\"translate(0 {y})\">\n"));
            if (c.Bars.Count > 0) c.DrawBars(s); else c.DrawLines(s);
            s.Append("</g>\n");
            y += c.Height;
        }
        s.Append("</svg>\n");
        return s.ToString();
    }

    private void DrawLines(StringBuilder s)
    {
        const double left = PlotLeft, right = PlotRight, top = 44;
        double bottom = 70 + LegendRow * LegendRows;
        double h = Height;
        var xs = Lines.SelectMany(l => l.Points.Select(p => p.X)).Concat(Bands.SelectMany(b => b.Points.Select(p => p.X))).ToList();
        var ys = Lines.SelectMany(l => l.Points.Select(p => p.Y))
            .Concat(Bands.SelectMany(b => b.Points.SelectMany(p => new[] { p.Low, p.High })))
            .Concat(Levels.Select(l => l.Y)).Where(double.IsFinite).ToList();
        if (xs.Count == 0 || ys.Count == 0) return;
        var (x0, x1, xStep) = Axis(xs.Min(), xs.Max(), tight: true);
        var (y0, y1, yStep) = Axis(YMin ?? ys.Min(), YMax ?? ys.Max(), tight: false);
        double X(double v) => left + (v - x0) / (x1 - x0) * (Width - left - right);
        double Y(double v) => h - bottom - (Math.Clamp(v, y0, y1) - y0) / (y1 - y0) * (h - top - bottom);

        s.Append(F($"<text x=\"{Width / 2}\" y=\"24\" text-anchor=\"middle\" font-size=\"14\">{Escape(Title)}</text>\n"));
        for (double v = y0; v <= y1 + yStep * 1e-6; v += yStep)
        {
            s.Append(F($"<line x1=\"{left}\" y1=\"{Y(v):0.0}\" x2=\"{Width - right}\" y2=\"{Y(v):0.0}\" stroke=\"#e4e4e4\"/>\n"));
            s.Append(F($"<text x=\"{left - 8}\" y=\"{Y(v):0.0}\" text-anchor=\"end\" dominant-baseline=\"middle\">{Number(v, yStep)}</text>\n"));
        }
        for (double v = Math.Ceiling(x0 / xStep - 1e-9) * xStep; v <= x1 + xStep * 1e-6; v += xStep)
        {
            s.Append(F($"<line x1=\"{X(v):0.0}\" y1=\"{top}\" x2=\"{X(v):0.0}\" y2=\"{h - bottom}\" stroke=\"#e4e4e4\"/>\n"));
            s.Append(F($"<text x=\"{X(v):0.0}\" y=\"{h - bottom + 18}\" text-anchor=\"middle\">{Number(v, xStep)}</text>\n"));
        }
        if (y0 < 0 && y1 > 0)
            s.Append(F($"<line x1=\"{left}\" y1=\"{Y(0):0.0}\" x2=\"{Width - right}\" y2=\"{Y(0):0.0}\" stroke=\"#9a9a9a\"/>\n"));
        s.Append(F($"<rect x=\"{left}\" y=\"{top}\" width=\"{Width - left - right}\" height=\"{h - top - bottom}\" fill=\"none\" stroke=\"black\"/>\n"));
        s.Append(F($"<text x=\"{(left + Width - right) / 2}\" y=\"{h - bottom + 36}\" text-anchor=\"middle\">{Escape(XLabel)}</text>\n"));
        s.Append(F($"<text x=\"20\" y=\"{(top + h - bottom) / 2}\" text-anchor=\"middle\" transform=\"rotate(-90 20 {(top + h - bottom) / 2})\">{Escape(YLabel)}</text>\n"));

        foreach (var b in Bands)
        {
            var path = b.Points.Select(p => F($"{X(p.X):0.0},{Y(p.High):0.0}"))
                .Concat(b.Points.Reverse().Select(p => F($"{X(p.X):0.0},{Y(p.Low):0.0}")));
            s.Append($"<polygon points=\"{string.Join(" ", path)}\" fill=\"{b.Color}\" fill-opacity=\"0.15\" stroke=\"none\"/>\n");
        }
        foreach (var l in Levels)
            s.Append(F($"<line x1=\"{left}\" y1=\"{Y(l.Y):0.0}\" x2=\"{Width - right}\" y2=\"{Y(l.Y):0.0}\" stroke=\"{l.Color}\" stroke-dasharray=\"2 3\"/>\n"));
        foreach (var l in Lines)
        {
            var pts = l.Points.Where(p => double.IsFinite(p.Y)).Select(p => F($"{X(p.X):0.0},{Y(p.Y):0.0}"));
            s.Append($"<polyline points=\"{string.Join(" ", pts)}\" fill=\"none\" stroke=\"{l.Color}\" stroke-width=\"2\"{(l.Dashed ? " stroke-dasharray=\"6 4\"" : "")}/>\n");
            if (l.Markers)
                foreach (var p in l.Points.Where(p => double.IsFinite(p.Y)))
                    s.Append(F($"<circle cx=\"{X(p.X):0.0}\" cy=\"{Y(p.Y):0.0}\" r=\"3\" fill=\"{l.Color}\"><title>{Number(p.X, 0.01)}, {Number(p.Y, Math.Abs(yStep) / 100)}</title></circle>\n"));
        }

        // The legend, in rows under the plot, so it never covers a curve.
        var entries = Legend();
        var (columns, columnWidth) = LegendLayout();
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            double lx = left + columnWidth * (i % columns) - 8, ey = h - bottom + 58 + LegendRow * (i / columns);
            if (e.Kind == 0)
                s.Append(F($"<rect x=\"{lx + 8}\" y=\"{ey - 6}\" width=\"24\" height=\"10\" fill=\"{e.Color}\" fill-opacity=\"0.15\"/>\n"));
            else
                s.Append(F($"<line x1=\"{lx + 8}\" y1=\"{ey - 1}\" x2=\"{lx + 32}\" y2=\"{ey - 1}\" stroke=\"{e.Color}\" stroke-width=\"{(e.Kind == 1 ? 2 : 1)}\"{(e.Dashed ? " stroke-dasharray=\"6 4\"" : e.Kind == 2 ? " stroke-dasharray=\"2 3\"" : "")}/>\n"));
            s.Append(F($"<text x=\"{lx + 38}\" y=\"{ey}\" dominant-baseline=\"middle\">{Escape(e.Label)}</text>\n"));
        }
    }

    private const double LegendRow = 20, PlotLeft = 80, PlotRight = 30;

    /// <summary>The legend's entries: kind 0 a band, 1 a line, 2 a level.</summary>
    private List<(string Label, string Color, int Kind, bool Dashed)> Legend() =>
        Bands.Select(b => (b.Label, b.Color, Kind: 0, Dashed: false))
            .Concat(Lines.Select(l => (l.Label, l.Color, Kind: 1, l.Dashed)))
            .Concat(Levels.Select(l => (l.Label, l.Color, Kind: 2, Dashed: false))).Where(e => e.Label != "").ToList();

    private (int Columns, double ColumnWidth) LegendLayout()
    {
        var entries = Legend();
        if (entries.Count == 0) return (1, 0);
        double w = entries.Max(e => e.Label.Length) * 6.6 + 56;
        return (Math.Max(1, (int)((Width - PlotLeft - PlotRight) / w)), w);
    }

    private int LegendRows => Legend().Count is var n && n > 0 ? (n + LegendLayout().Columns - 1) / LegendLayout().Columns : 0;

    private void DrawBars(StringBuilder s)
    {
        const double left = 290, right = 70;
        double top = BarHeader - 16, h = Height, bottom = BarFooter;
        var values = Bars.Select(b => b.Value).Append(0.0).ToList();
        double span = values.Max() - values.Min();
        var (x0, x1, step) = Axis(values.Min() - 0.12 * span, values.Max() + 0.12 * span, tight: false);
        double X(double v) => left + (v - x0) / (x1 - x0) * (Width - left - right);

        s.Append(F($"<text x=\"{Width / 2}\" y=\"24\" text-anchor=\"middle\" font-size=\"14\">{Escape(Title)}</text>\n"));
        for (double v = x0; v <= x1 + step * 1e-6; v += step)
        {
            s.Append(F($"<line x1=\"{X(v):0.0}\" y1=\"{top}\" x2=\"{X(v):0.0}\" y2=\"{h - bottom}\" stroke=\"#e4e4e4\"/>\n"));
            s.Append(F($"<text x=\"{X(v):0.0}\" y=\"{h - bottom + 18}\" text-anchor=\"middle\">{Number(v, step)}</text>\n"));
        }
        for (int i = 0; i < Bars.Count; i++)
        {
            var (label, value, color) = Bars[i];
            double y = top + BarRow * i + 4, a = X(Math.Min(0, value)), b = X(Math.Max(0, value));
            s.Append(F($"<text x=\"{left - 8}\" y=\"{y + 9}\" text-anchor=\"end\" dominant-baseline=\"middle\">{Escape(label)}</text>\n"));
            s.Append(F($"<rect x=\"{a:0.0}\" y=\"{y}\" width=\"{Math.Max(b - a, 0.5):0.0}\" height=\"18\" fill=\"{color}\"/>\n"));
            double tx = value < 0 ? a - 4 : b + 4;
            s.Append(F($"<text x=\"{tx:0.0}\" y=\"{y + 9}\" text-anchor=\"{(value < 0 ? "end" : "start")}\" dominant-baseline=\"middle\" font-size=\"11\">{Number(value, Math.Abs(step) / 100)}</text>\n"));
        }
        s.Append(F($"<line x1=\"{X(0):0.0}\" y1=\"{top}\" x2=\"{X(0):0.0}\" y2=\"{h - bottom}\" stroke=\"black\"/>\n"));
        s.Append(F($"<text x=\"{(left + Width - right) / 2}\" y=\"{h - 14}\" text-anchor=\"middle\">{Escape(XLabel)}</text>\n"));
    }

    /// <summary>An axis from <paramref name="min"/> to <paramref name="max"/> on steps of 1, 2 or 5 × 10ⁿ; a tight axis ends at the data.</summary>
    private static (double Low, double High, double Step) Axis(double min, double max, bool tight)
    {
        if (max - min < 1e-12 * Math.Max(1.0, Math.Abs(max))) { min -= 1.0; max += 1.0; }
        double raw = (max - min) / 6.0, p = Math.Pow(10, Math.Floor(Math.Log10(raw))), f = raw / p;
        double step = (f < 1.5 ? 1 : f < 3 ? 2 : f < 7 ? 5 : 10) * p;
        if (tight) return (min, max, step);
        return (Math.Floor(min / step + 1e-9) * step, Math.Ceiling(max / step - 1e-9) * step, step);
    }

    private static string Number(double v, double step)
    {
        int decimals = Math.Clamp((int)Math.Ceiling(-Math.Log10(Math.Abs(step)) - 1e-9), 0, 6);
        if (Math.Abs(v) < step * 1e-6) v = 0.0;
        return v.ToString("F" + decimals, CultureInfo.InvariantCulture).Replace("-", "−");
    }

    private static string F(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);

    private static string Escape(string text) => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
