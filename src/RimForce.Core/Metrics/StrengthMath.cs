using RimForce.Core.Hardware;

namespace RimForce.Core.Metrics;

/// <summary>
/// Reference isometric strength (MVC) used to turn wheel torque into "% of a typical person".
/// Sources and derivations: docs/research/03-torque-biomechanics.md §1.2, §3.
/// </summary>
public sealed record StrengthNorms(
    string Name,
    double ShoulderFlexionNm,   // arm at ~90°, lifting
    double WristRadialDevNm,    // thumb-up power grip
    double GripKgf,             // dynamometer
    double PronationNm,
    double SupinationNm,
    double TwoHandSteeringNm,   // seated, 300 mm rim (scaled from FSAE data)
    double BodyMassKg)
{
    public static readonly StrengthNorms Men = new("Men", 77, 10.3, 46, 8.1, 8.0, 47, 80);
    public static readonly StrengthNorms Women = new("Women", 40, 6.2, 28, 4.7, 4.9, 26, 65);
    public static StrengthNorms For(string? key) => key == "women" ? Women : Men;
}

/// <summary>Instantaneous strength numbers shown in the cockpit. All "Pct" values are % of the reference MVC.</summary>
public sealed record LiveStrength(
    double TorqueNm,
    double RimForceN,
    double RimForceKgf,
    double PerHandKgf,
    double GripKgf,
    double GripPct,
    double WristNm,
    double WristPct,
    double ShoulderNm,
    double ShoulderPct,
    double ForearmTwistPct,
    double SystemPct,
    double PercentOfMax,
    double FrontRaiseKg,
    double PowerW,
    string LimitingLink,
    double LimitingPct,
    double EnduranceS,
    double BudgetUsedPct,
    int Hands)
{
    public static readonly LiveStrength Zero = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "shoulder", 0, double.PositiveInfinity, 0, 2);
}

/// <summary>
/// Turns wheel torque + motion into body-relevant numbers (estimates; assumptions documented in
/// docs/research/03-torque-biomechanics.md §6):
///   rim force F = |τ| / r, per hand F_i = F / hands (50/50 split)
///   hands rotate with the wheel: vertical part of F_i = F_i |cos θ|
///   shoulder M = F_vert · 0.45 m + arm weight (4.9% BM at 0.2 m)
///   wrist M = F_i · 0.075 m (radial deviation, thumb-up grip)
///   grip ≈ 1.4 · F_i / (2 μ)
///   front-raise equivalent = F_vert/g · 0.45/0.6
///   endurance (Rohmert): ET[min] = -1.5 + 2.1/f - 0.6/f² + 0.1/f³, 0.15 ≤ f ≤ 1
/// </summary>
public static class StrengthMath
{
    public const double ShoulderReachM = 0.45;
    public const double StraightArmM = 0.6;
    public const double WristLeverM = 0.075;
    public const double ArmMassFrac = 0.049;
    public const double ArmComM = 0.2;

    public static LiveStrength Compute(WheelProfile p, double userTorqueNm, double velocityDps, int hands, double? maxTorqueNm,
        double angleDeg = 0, StrengthNorms? norms = null, double? bodyMassKg = null, bool wristTechnique = false, double budgetUsedPct = 0)
    {
        var n = norms ?? StrengthNorms.Men;
        hands = Math.Clamp(hands, 1, 2);
        double t = Math.Abs(userTorqueNm);
        double rimN = p.RimForceN(t);
        double perHandN = rimN / hands;
        double vertN = perHandN * Math.Abs(Math.Cos(angleDeg * Units.DegToRad));
        double bm = bodyMassKg ?? n.BodyMassKg;

        double gripN = 1.4 * perHandN / (2 * Math.Max(0.2, p.HandRimFriction));
        double gripKgf = Units.NToKgf(gripN);
        double gripPct = gripKgf / n.GripKgf * 100;

        double wristNm = perHandN * WristLeverM;
        double wristPct = wristNm / n.WristRadialDevNm * 100;

        double armWeightNm = ArmMassFrac * bm * Units.G * ArmComM;
        double shoulderNm = vertN * ShoulderReachM + armWeightNm;
        double shoulderPct = shoulderNm / n.ShoulderFlexionNm * 100;

        // "If you held it purely by twisting": both forearms share it (pronation one side, supination the other).
        double twistCap = hands == 2 ? n.PronationNm + n.SupinationNm : Math.Max(n.PronationNm, n.SupinationNm);
        double twistPct = t / twistCap * 100;

        double systemPct = t / (hands == 2 ? n.TwoHandSteeringNm : n.TwoHandSteeringNm * 0.47) * 100;
        double pctOfMax = maxTorqueNm is > 0.1 ? t / maxTorqueNm.Value * 100.0 : 0;

        double frontRaise = Units.NToKgf(vertN) * ShoulderReachM / StraightArmM;
        double power = userTorqueNm * velocityDps * Units.DegToRad;

        // Weakest link: what limits a static hold in this technique.
        (string link, double pct) limiting = wristTechnique
            ? ("forearm twist", twistPct)
            : new[] { ("shoulder", shoulderPct), ("wrist", wristPct), ("grip", gripPct) }.MaxBy(x => x.Item2);

        double et = EnduranceSeconds(limiting.pct / 100.0);

        return new LiveStrength(
            Math.Round(t, 3), Math.Round(rimN, 1), Math.Round(Units.NToKgf(rimN), 2), Math.Round(Units.NToKgf(perHandN), 2),
            Math.Round(gripKgf, 2), Math.Round(gripPct, 1), Math.Round(wristNm, 2), Math.Round(wristPct, 1),
            Math.Round(shoulderNm, 1), Math.Round(shoulderPct, 1), Math.Round(twistPct, 1), Math.Round(systemPct, 1),
            Math.Round(pctOfMax, 1), Math.Round(frontRaise, 2), Math.Round(power, 1),
            limiting.link, Math.Round(limiting.pct, 1), et, Math.Round(budgetUsedPct, 1), hands);
    }

    /// <summary>Rohmert endurance time (s) for a fraction of MVC. Below 15% we floor at ET(0.15) (Rohmert is optimistic there).</summary>
    public static double EnduranceSeconds(double f)
    {
        if (f <= 0.01) return double.PositiveInfinity;
        f = Math.Clamp(f, 0.15, 1.0);
        double minutes = -1.5 + 2.1 / f - 0.6 / (f * f) + 0.1 / (f * f * f);
        return Math.Max(1, minutes * 60);
    }

    /// <summary>Inverse of Rohmert: what fraction of MVC can be held for this many seconds (0.15..1).</summary>
    public static double FractionForEndurance(double seconds)
    {
        if (seconds <= EnduranceSeconds(1.0)) return 1.0;
        if (seconds >= EnduranceSeconds(0.15)) return 0.15;
        double lo = 0.15, hi = 1.0;
        for (int i = 0; i < 60; i++)
        {
            double mid = (lo + hi) / 2;
            if (EnduranceSeconds(mid) > seconds) lo = mid; else hi = mid;
        }
        return (lo + hi) / 2;
    }
}
