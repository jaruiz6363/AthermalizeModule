using System.Text.Json;

namespace Athermalize.Core.Materials;

/// <summary>
/// A material's length over temperature: its relative change of length and its expansion coefficient at each
/// temperature. A length at one temperature grows to another by the ratio of (1 + strain) at the two, so a part's
/// growth is the integral of its coefficient over the change rather than a single coefficient times the change.
///
/// <para>The coefficient is the one sources tabulate, (1/L_ref)·dL/dT for a fixed reference length; it and the
/// change of length per unit length differ from the instantaneous ones by the strain, under 0.5 % for any material
/// here.</para>
/// </summary>
public abstract class ExpansionCurve
{
    /// <summary>The relative change of length from the curve's reference length at <paramref name="temperatureC"/>.</summary>
    public abstract double Strain(double temperatureC);

    /// <summary>The expansion coefficient at <paramref name="temperatureC"/>, 1/K.</summary>
    public abstract double Alpha(double temperatureC);

    /// <summary>The temperatures, °C, the data cover; outside them the curve is extrapolated.</summary>
    public abstract (double Min, double Max) RangeC { get; }

    /// <summary>Where the data come from.</summary>
    public string Source { get; init; } = "";

    public bool InRange(double temperatureC) => temperatureC >= RangeC.Min - 1e-9 && temperatureC <= RangeC.Max + 1e-9;

    /// <summary>The factor a length at <paramref name="fromC"/> grows by at <paramref name="toC"/>.</summary>
    public virtual double Scale(double fromC, double toC) => (1.0 + Strain(toC)) / (1.0 + Strain(fromC));

    /// <summary>The mean coefficient from <paramref name="fromC"/> to <paramref name="toC"/>, 1/K.</summary>
    public double MeanAlpha(double fromC, double toC) =>
        toC == fromC ? Alpha(fromC) : (Scale(fromC, toC) - 1.0) / (toC - fromC);

    protected const double Kelvin = 273.15;

    /// <summary>
    /// Reads a curve in the format of MeasuredMaterials.json: a "table" of "temperaturesK" or "temperaturesC" with
    /// "alpha" and, optionally, "strain", each multiplied by its "alphaScale" or "strainScale" as printed; or a
    /// "polynomial" of "pieces" ({"fromK", "originK", "coefficients"}) over "rangeK", multiplied by "scale".
    /// </summary>
    internal static ExpansionCurve Read(JsonElement e)
    {
        static double[] Row(JsonElement e, double scale = 1.0) => e.EnumerateArray().Select(v => v.GetDouble() * scale).ToArray();
        static double Scale(JsonElement e, string name) => e.TryGetProperty(name, out var s) ? s.GetDouble() : 1.0;

        string source = e.TryGetProperty("source", out var src) ? src.GetString()! : "";
        switch (e.GetProperty("kind").GetString())
        {
            case "table":
            {
                double[] t = e.TryGetProperty("temperaturesK", out var tk) ? Row(tk)
                    : Row(e.GetProperty("temperaturesC")).Select(c => c + Kelvin).ToArray();
                double[]? strain = e.TryGetProperty("strain", out var st) ? Row(st, Scale(e, "strainScale")) : null;
                return new TabulatedExpansion(t, Row(e.GetProperty("alpha"), Scale(e, "alphaScale")), strain) { Source = source };
            }
            case "polynomial":
            {
                double scale = Scale(e, "scale");
                var pieces = e.GetProperty("pieces").EnumerateArray().Select(p => new PolynomialExpansion.Piece(
                    p.GetProperty("fromK").GetDouble(), p.TryGetProperty("originK", out var o) ? o.GetDouble() : 0.0,
                    Row(p.GetProperty("coefficients"), scale)));
                var r = e.GetProperty("rangeK");
                return new PolynomialExpansion(pieces, (r[0].GetDouble(), r[1].GetDouble())) { Source = source };
            }
            default:
                throw new InvalidOperationException($"Unknown expansion curve \"{e.GetProperty("kind").GetString()}\".");
        }
    }
}

/// <summary>One coefficient at every temperature: a length grows by 1 + α·ΔT.</summary>
public sealed class ConstantExpansion : ExpansionCurve
{
    public ConstantExpansion(double alpha) => Coefficient = alpha;

    public double Coefficient { get; }

    public override double Strain(double temperatureC) => Coefficient * (temperatureC - 20.0);
    public override double Alpha(double temperatureC) => Coefficient;
    public override (double Min, double Max) RangeC => (double.NegativeInfinity, double.PositiveInfinity);
    public override double Scale(double fromC, double toC) => 1.0 + Coefficient * (toC - fromC);
}

/// <summary>
/// A table over temperature, as the sources print it: the coefficient at each temperature, and the change of length
/// when the source gives it.
/// <list type="bullet">
/// <item>With the change of length, the curve is the cubic through each pair of neighboring points with the
/// tabulated changes and coefficients (Hermite interpolation), so it reproduces both columns.</item>
/// <item>With the coefficient alone, the coefficient is linear between points and the change of length its
/// integral.</item>
/// </list>
/// Above the table the coefficient is held at its last value. Below it the coefficient falls in proportion to the
/// absolute temperature, reaching zero at 0 K, as every solid's does.
/// </summary>
public sealed class TabulatedExpansion : ExpansionCurve
{
    private readonly double[] _t, _alpha, _strain;

    /// <param name="temperaturesK">The table's temperatures, K, rising.</param>
    /// <param name="alpha">The coefficient at each, 1/K.</param>
    /// <param name="strain">The relative change of length at each, from any reference; null when the source gives none.</param>
    public TabulatedExpansion(double[] temperaturesK, double[] alpha, double[]? strain = null)
    {
        if (temperaturesK.Length < 2 || alpha.Length != temperaturesK.Length || strain != null && strain.Length != temperaturesK.Length)
            throw new ArgumentException("A table needs two or more temperatures, and a value of each column at each.");
        for (int i = 1; i < temperaturesK.Length; i++)
            if (temperaturesK[i] <= temperaturesK[i - 1]) throw new ArgumentException("The temperatures must rise.");
        _t = temperaturesK;
        _alpha = alpha;
        HasStrain = strain != null;
        if (strain != null) _strain = strain;
        else
        {
            _strain = new double[_t.Length];
            for (int i = 1; i < _t.Length; i++)
                _strain[i] = _strain[i - 1] + 0.5 * (_alpha[i] + _alpha[i - 1]) * (_t[i] - _t[i - 1]);
        }
    }

    /// <summary>True when the table gives the change of length as well as the coefficient.</summary>
    public bool HasStrain { get; }

    public override (double Min, double Max) RangeC => (_t[0] - Kelvin, _t[^1] - Kelvin);

    public override double Strain(double temperatureC)
    {
        double t = temperatureC + Kelvin;
        if (t >= _t[^1]) return _strain[^1] + _alpha[^1] * (t - _t[^1]);
        if (t <= _t[0])
        {
            double t0 = Math.Max(t, 0.0);
            return _strain[0] - _alpha[0] * (_t[0] * _t[0] - t0 * t0) / (2.0 * _t[0]);
        }
        int i = Segment(t);
        double h = _t[i + 1] - _t[i], u = (t - _t[i]) / h;
        if (!HasStrain)
            return _strain[i] + h * (_alpha[i] * u + 0.5 * (_alpha[i + 1] - _alpha[i]) * u * u);
        double u2 = u * u, u3 = u2 * u;
        return (2 * u3 - 3 * u2 + 1) * _strain[i] + (u3 - 2 * u2 + u) * h * _alpha[i]
             + (-2 * u3 + 3 * u2) * _strain[i + 1] + (u3 - u2) * h * _alpha[i + 1];
    }

    public override double Alpha(double temperatureC)
    {
        double t = temperatureC + Kelvin;
        if (t >= _t[^1]) return _alpha[^1];
        if (t <= _t[0]) return _alpha[0] * Math.Max(t, 0.0) / _t[0];
        int i = Segment(t);
        double h = _t[i + 1] - _t[i], u = (t - _t[i]) / h;
        if (!HasStrain) return _alpha[i] + (_alpha[i + 1] - _alpha[i]) * u;
        double u2 = u * u;
        return ((6 * u2 - 6 * u) * _strain[i] + (3 * u2 - 4 * u + 1) * h * _alpha[i]
              + (-6 * u2 + 6 * u) * _strain[i + 1] + (3 * u2 - 2 * u) * h * _alpha[i + 1]) / h;
    }

    private int Segment(double t)
    {
        int i = Array.BinarySearch(_t, t);
        if (i < 0) i = ~i - 1;
        return Math.Clamp(i, 0, _t.Length - 2);
    }
}

/// <summary>
/// The change of length as polynomials in the temperature, as fits print it: each piece holds over a range of
/// temperatures and is a polynomial in (T − its origin), T in kelvin. Outside the pieces the coefficient is held at
/// the end's value.
/// </summary>
public sealed class PolynomialExpansion : ExpansionCurve
{
    /// <summary>One piece: from <paramref name="FromK"/> up to the next piece, a polynomial in (T − <paramref name="OriginK"/>), constant term first.</summary>
    public sealed record Piece(double FromK, double OriginK, double[] Coefficients);

    private readonly Piece[] _pieces;
    private readonly double _minK, _maxK;

    /// <param name="pieces">The pieces, by rising <see cref="Piece.FromK"/>.</param>
    /// <param name="rangeK">The temperatures, K, the fit holds over.</param>
    public PolynomialExpansion(IEnumerable<Piece> pieces, (double Min, double Max) rangeK)
    {
        _pieces = pieces.OrderBy(p => p.FromK).ToArray();
        if (_pieces.Length == 0) throw new ArgumentException("A fit needs at least one piece.");
        (_minK, _maxK) = rangeK;
    }

    public override (double Min, double Max) RangeC => (_minK - Kelvin, _maxK - Kelvin);

    public override double Strain(double temperatureC)
    {
        double t = temperatureC + Kelvin;
        if (t > _maxK) return Value(_maxK) + Slope(_maxK) * (t - _maxK);
        if (t < _minK) return Value(_minK) + Slope(_minK) * (t - _minK);
        return Value(t);
    }

    public override double Alpha(double temperatureC) => Slope(Math.Clamp(temperatureC + Kelvin, _minK, _maxK));

    private Piece At(double t)
    {
        var p = _pieces[0];
        foreach (var q in _pieces) if (t >= q.FromK) p = q;
        return p;
    }

    private double Value(double t)
    {
        var p = At(t);
        double x = t - p.OriginK, y = 0.0;
        for (int j = p.Coefficients.Length - 1; j >= 0; j--) y = y * x + p.Coefficients[j];
        return y;
    }

    private double Slope(double t)
    {
        var p = At(t);
        double x = t - p.OriginK, y = 0.0;
        for (int j = p.Coefficients.Length - 1; j >= 1; j--) y = y * x + j * p.Coefficients[j];
        return y;
    }
}
