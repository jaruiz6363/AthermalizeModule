namespace Athermalize.Core.Air;

/// <summary>Which equation gives the refractive index of air.</summary>
public enum AirModel
{
    /// <summary>
    /// SCHOTT TIE-19, eqs. (8) and (9): Edlén's 1953 dispersion of dry standard air at 15 °C, scaled to the
    /// temperature and pressure as an ideal gas. It has no humidity, CO₂ or compressibility term. Glass makers
    /// fit their thermal coefficients with it, so it is the default.
    /// </summary>
    Tie19,

    /// <summary>
    /// Ciddor (1996), with humidity, CO₂ and compressibility. Valid from −40 to +100 °C, 80 to 120 kPa,
    /// 0 to 100 % relative humidity and about 0.3 to 1.7 µm.
    /// </summary>
    Ciddor1996,
}

/// <summary>The refractive index of air.</summary>
public static class AirIndex
{
    /// <summary>The index of air in <paramref name="env"/> at <paramref name="lambdaUm"/> micrometers.</summary>
    public static double Of(AirModel model, double lambdaUm, AirConditions env) => model switch
    {
        AirModel.Tie19 => Tie19(lambdaUm, env.TemperatureC, env.Atmospheres),
        AirModel.Ciddor1996 => Ciddor1996(lambdaUm, env.TemperatureC, env.PressurePa, env.RelativeHumidity, env.Co2Ppm),
        _ => throw new ArgumentOutOfRangeException(nameof(model)),
    };

    /// <summary>
    /// SCHOTT TIE-19 eqs. (8) and (9):
    /// n = 1 + (n₁₅ − 1)·P/(1 + 3.4785×10⁻³ (T − 15)), with n₁₅ Edlén's (1953) standard dry air at 15 °C,
    /// n₁₅ − 1 = 10⁻⁸ [6432.8 + 2 949 810 λ²/(146 λ² − 1) + 25 540 λ²/(41 λ² − 1)].
    /// </summary>
    /// <param name="lambdaUm">Wavelength in µm.</param>
    /// <param name="temperatureC">Temperature in °C.</param>
    /// <param name="atmospheres">Pressure in standard atmospheres (101 325 Pa).</param>
    public static double Tie19(double lambdaUm, double temperatureC, double atmospheres)
    {
        double l2 = lambdaUm * lambdaUm;
        double n15 = 1e-8 * (6432.8 + 2949810.0 * l2 / (146.0 * l2 - 1.0) + 25540.0 * l2 / (41.0 * l2 - 1.0));
        return 1.0 + n15 * atmospheres / (1.0 + 3.4785e-3 * (temperatureC - 15.0));
    }

    /// <summary>
    /// Ciddor, "Refractive index of air: new equations for the visible and near infrared," Appl. Opt. 35,
    /// 1566-1573 (1996), with the saturation vapor pressure and enhancement factor as NIST's Engineering
    /// Metrology Toolbox states them (IAPWS over water at 0 °C and above, over ice below).
    /// </summary>
    /// <param name="lambdaVacuumUm">Vacuum wavelength in µm.</param>
    /// <param name="temperatureC">Temperature in °C.</param>
    /// <param name="pressurePa">Absolute pressure in Pa.</param>
    /// <param name="relativeHumidity">Relative humidity, 0 to 1.</param>
    /// <param name="co2Ppm">CO₂ mole fraction in µmol/mol.</param>
    public static double Ciddor1996(double lambdaVacuumUm, double temperatureC, double pressurePa,
        double relativeHumidity = 0.0, double co2Ppm = 450.0)
    {
        if (pressurePa <= 0.0) return 1.0;

        double t = temperatureC, T = t + 273.15, p = pressurePa;
        double s2 = 1.0 / (lambdaVacuumUm * lambdaVacuumUm);

        // Standard dry air (15 °C, 101 325 Pa), then corrected to the CO₂ content.
        double nas = 1e-8 * (5792105.0 / (238.0185 - s2) + 167917.0 / (57.362 - s2));
        double naxs = nas * (1.0 + 0.534e-6 * (co2Ppm - 450.0));

        // Standard water vapor (20 °C, 1333 Pa).
        double nws = 1e-8 * 1.022 * (295.235 + 2.6422 * s2 - 0.032380 * s2 * s2 + 0.004028 * s2 * s2 * s2);

        double xw = relativeHumidity > 0.0
            ? EnhancementFactor(t, p) * relativeHumidity * SaturationVaporPressure(T) / p
            : 0.0;

        const double R = 8.314510, Mw = 0.018015;
        double Ma = 1e-3 * (28.9635 + 12.011e-6 * (co2Ppm - 400.0));

        double rhoAxs = 101325.0 * Ma / (Compressibility(101325.0, 288.15, 0.0) * R * 288.15);
        double rhoWs = 1333.0 * Mw / (Compressibility(1333.0, 293.15, 1.0) * R * 293.15);

        double Z = Compressibility(p, T, xw);
        double rhoA = p * Ma * (1.0 - xw) / (Z * R * T);
        double rhoW = p * Mw * xw / (Z * R * T);

        return 1.0 + rhoA / rhoAxs * naxs + rhoW / rhoWs * nws;
    }

    /// <summary>The compressibility of moist air (BIPM 1981/91), Ciddor's eq. (12).</summary>
    private static double Compressibility(double p, double T, double xw)
    {
        double t = T - 273.15, pt = p / T;
        const double a0 = 1.58123e-6, a1 = -2.9331e-8, a2 = 1.1043e-10;
        const double b0 = 5.707e-6, b1 = -2.051e-8;
        const double c0 = 1.9898e-4, c1 = -2.376e-6;
        const double d = 1.83e-11, e = -0.765e-8;
        return 1.0 - pt * (a0 + a1 * t + a2 * t * t + (b0 + b1 * t) * xw + (c0 + c1 * t) * xw * xw)
                   + pt * pt * (d + e * xw * xw);
    }

    /// <summary>The enhancement factor of water vapor in air, f = 1.00062 + 3.14×10⁻⁸ p + 5.6×10⁻⁷ t².</summary>
    private static double EnhancementFactor(double t, double p) => 1.00062 + 3.14e-8 * p + 5.6e-7 * t * t;

    /// <summary>
    /// The saturation vapor pressure in Pa: IAPWS (1997) over water at 0 °C and above, IAPWS (2008) over ice below.
    /// </summary>
    internal static double SaturationVaporPressure(double T)
    {
        if (T >= 273.15)
        {
            const double K1 = 1.16705214528e3, K2 = -7.24213167032e5, K3 = -1.70738469401e1,
                K4 = 1.20208247025e4, K5 = -3.23255503223e6, K6 = 1.49151086135e1, K7 = -4.82326573616e3,
                K8 = 4.05113405421e5, K9 = -2.38555575678e-1, K10 = 6.50175348448e2;
            double omega = T + K9 / (T - K10);
            double A = omega * omega + K1 * omega + K2;
            double B = K3 * omega * omega + K4 * omega + K5;
            double C = K6 * omega * omega + K7 * omega + K8;
            double X = -B + Math.Sqrt(B * B - 4.0 * A * C);
            return 1e6 * Math.Pow(2.0 * C / X, 4);
        }

        double theta = T / 273.16;
        double Y = -13.928169 * (1.0 - Math.Pow(theta, -1.5)) + 34.7078238 * (1.0 - Math.Pow(theta, -1.25));
        return 611.657 * Math.Exp(Y);
    }
}
