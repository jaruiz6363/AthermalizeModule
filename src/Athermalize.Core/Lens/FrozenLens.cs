using System.Globalization;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.Models;
using Athermalize.Core.Air;
using Athermalize.Core.Materials;

namespace Athermalize.Core.Lens;

/// <summary>
/// One material of a <see cref="FrozenLens"/>: its index relative to the air at the lens's conditions, sampled
/// across the band, under a name of its own.
/// </summary>
/// <param name="Name">The frozen glass's name, the material's with the conditions: N-BK7_M40C_VAC.</param>
/// <param name="Source">The material it was frozen from, and the conditions, in words.</param>
/// <param name="Points">Wavelength in the lens's air (µm) and the relative index there; the lens's own wavelengths among them.</param>
public sealed record FrozenMaterial(string Name, string Source, IReadOnlyList<(double Um, double N)> Points)
{
    /// <summary>
    /// A catalog glass through the points: the Schott formula when it fits within 10⁻⁷, else whichever of it and
    /// the Extended 2 formula, which adds λ⁴ and λ⁶ for the infrared, fits better. Both are linear in n², fitted by
    /// least squares. The glass is relative to its air and has no thermal data.
    /// </summary>
    public (GlassData Glass, double MaxError) Fit()
    {
        var schott = Fit(DispersionFormula.Schott, l2 => new[] { 1.0, l2, 1 / l2, 1 / (l2 * l2), 1 / (l2 * l2 * l2), 1 / (l2 * l2 * l2 * l2) });
        if (schott.MaxError <= 1e-7) return schott;
        var extended = Fit(DispersionFormula.Extended2, l2 =>
            new[] { 1.0, l2, 1 / l2, 1 / (l2 * l2), 1 / (l2 * l2 * l2), 1 / (l2 * l2 * l2 * l2), l2 * l2, l2 * l2 * l2 });
        return extended.MaxError < schott.MaxError ? extended : schott;
    }

    private (GlassData Glass, double MaxError) Fit(DispersionFormula formula, Func<double, double[]> basis)
    {
        var rows = Points.Select(p => basis(p.Um * p.Um)).ToArray();
        var c = LeastSquares(rows, Points.Select(p => p.N * p.N).ToArray());
        var g = new GlassData
        {
            Name = Name, Formula = formula, Coefficients = c, Comment = Source,
            LambdaMin = Points.Min(p => p.Um), LambdaMax = Points.Max(p => p.Um),
        };
        return (g, Points.Max(p => Math.Abs(g.IndexAt(p.Um) - p.N)));
    }

    /// <summary>Least squares by Householder QR, the columns scaled to unit length, since the basis spans many orders of magnitude.</summary>
    private static double[] LeastSquares(double[][] rows, double[] y)
    {
        int m = rows.Length, n = rows[0].Length;
        var a = new double[m, n];
        var b = (double[])y.Clone();
        var scale = new double[n];
        for (int j = 0; j < n; j++)
        {
            scale[j] = Math.Sqrt(rows.Sum(r => r[j] * r[j]));
            for (int i = 0; i < m; i++) a[i, j] = rows[i][j] / scale[j];
        }
        for (int k = 0; k < n; k++)
        {
            double norm = 0.0;
            for (int i = k; i < m; i++) norm += a[i, k] * a[i, k];
            norm = Math.Sqrt(norm);
            if (a[k, k] > 0) norm = -norm;
            var v = new double[m];
            for (int i = k; i < m; i++) v[i] = a[i, k];
            v[k] -= norm;
            double vv = 0.0;
            for (int i = k; i < m; i++) vv += v[i] * v[i];
            if (vv == 0.0) continue;
            for (int j = k; j < n; j++)
            {
                double s = 0.0;
                for (int i = k; i < m; i++) s += v[i] * a[i, j];
                s = 2.0 * s / vv;
                for (int i = k; i < m; i++) a[i, j] -= s * v[i];
            }
            double sb = 0.0;
            for (int i = k; i < m; i++) sb += v[i] * b[i];
            sb = 2.0 * sb / vv;
            for (int i = k; i < m; i++) b[i] -= sb * v[i];
        }
        var x = new double[n];
        for (int j = n - 1; j >= 0; j--)
        {
            double s = b[j];
            for (int k = j + 1; k < n; k++) s -= a[j, k] * x[k];
            x[j] = s / a[j, j];
        }
        for (int j = 0; j < n; j++) x[j] /= scale[j];
        return x;
    }
}

/// <summary>
/// A lens at a temperature and in an air, made into a lens any program traces correctly at its default conditions.
///
/// <para>A ray trace needs only the indices relative to the air around the lens and the wavelengths measured in
/// that air: dividing every index in Snell's law by the air's leaves the rays unchanged, and the optical path in
/// waves is the same either way. So the lens at the temperature, its geometry grown, with each material replaced by
/// a glass whose index is the material's relative index at those conditions and whose wavelengths are those in that
/// air, traces exactly as the module's lens. The frozen glasses carry no thermal data, so a program that changes
/// indices with temperature leaves them alone at its reference conditions.</para>
/// </summary>
public sealed class FrozenLens
{
    private FrozenLens(OpticalSystem system, IReadOnlyList<FrozenMaterial> materials, ThermalLens lens)
    {
        System = system;
        Materials = materials;
        Lens = lens;
    }

    /// <summary>The lens to write: the lens at the temperature, its materials renamed to the frozen glasses.</summary>
    public OpticalSystem System { get; }

    /// <summary>The frozen glasses, one for each material of the lens.</summary>
    public IReadOnlyList<FrozenMaterial> Materials { get; }

    /// <summary>The lens at the temperature it was frozen from.</summary>
    public ThermalLens Lens { get; }

    /// <summary>
    /// The lens of <paramref name="model"/> at <paramref name="air"/>'s temperature and pressure, frozen. Each material is
    /// sampled at <paramref name="points"/> wavelengths evenly from 10 % below the lens's shortest wavelength to 10 %
    /// above its longest, and at the lens's own wavelengths.
    /// </summary>
    public static FrozenLens Of(ThermalModel model, AirConditions air, int points = 41)
    {
        var lens = model.At(air);
        var system = lens.System;
        var options = model.Options.Index ?? IndexOptions.Default;
        var own = Enumerable.Range(0, system.Wavelengths.Count).Select(w => model.WavelengthIn(w, air)).ToList();
        double lo = own.Min() * 0.9, hi = own.Max() * 1.1;
        var sample = Enumerable.Range(0, points).Select(k => lo + (hi - lo) * k / (points - 1))
            .Concat(own).Distinct().OrderBy(x => x).ToList();

        string tag = Tag(air);
        var byName = new Dictionary<string, FrozenMaterial>(StringComparer.OrdinalIgnoreCase);
        for (int i = 1; i < system.Surfaces.Count - 1; i++)
        {
            var m = model.MaterialAfter(i);
            if (m == null || system.Surfaces[i].IsMirror) continue;
            if (!byName.TryGetValue(m.Name, out var frozen))
            {
                var pts = sample.Select(um => (um, m.Index(um, air, air.TemperatureC, options).Relative)).ToList();
                frozen = new FrozenMaterial(Clean(m.Glass) + "_" + tag,
                    FormattableString.Invariant($"{m.Name} at {air.TemperatureC:0.##} °C in {Describe(air)}, relative to that air; frozen by atherm"),
                    pts);
                byName[m.Name] = frozen;
            }
            system.Surfaces[i].Material = frozen.Name;
        }
        system.Title = FormattableString.Invariant(
            $"{(string.IsNullOrWhiteSpace(model.Nominal.Title) ? "Lens" : model.Nominal.Title.Trim())} at {air.TemperatureC:0.##} C in {Describe(air)}, frozen");
        system.GlassCatalogs.Clear();
        return new FrozenLens(system, byName.Values.ToList(), lens);
    }

    /// <summary>The conditions in a glass name: M40C_VAC for −40 °C in vacuum, 20C_0P5ATM for 20 °C at 0.5 atm.</summary>
    public static string Tag(AirConditions air)
    {
        string N(double v) => v.ToString("0.##", CultureInfo.InvariantCulture).Replace("-", "M").Replace(".", "P");
        return N(air.TemperatureC) + "C_" + (air.IsVacuum ? "VAC" : N(air.Atmospheres) + "ATM");
    }

    private static string Describe(AirConditions air) =>
        air.IsVacuum ? "vacuum" : FormattableString.Invariant($"air at {air.Atmospheres:0.####} atm");

    /// <summary>A glass name as a catalog line takes it: no spaces.</summary>
    private static string Clean(string name) => new(name.Select(c => char.IsWhiteSpace(c) ? '_' : char.ToUpperInvariant(c)).ToArray());
}
