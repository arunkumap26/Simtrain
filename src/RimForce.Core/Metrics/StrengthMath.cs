using RimForce.Core.Hardware;

namespace RimForce.Core.Metrics;

/// <summary>Instantaneous strength numbers shown in the cockpit.</summary>
public sealed record LiveStrength(
    double TorqueNm,
    double RimForceN,
    double RimForceKgf,
    double PerHandKgf,
    double GripSqueezeKgf,
    double PowerW,
    double ShoulderEquivKg,
    double ForearmShareNm,
    double PercentOfMax,
    int Hands)
{
    public static readonly LiveStrength Zero = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 2);
}

/// <summary>
/// Turns wheel torque + motion into body-relevant numbers. Assumptions are documented in
/// docs/research/03-torque-biomechanics.md and shown in the UI as estimates.
/// </summary>
public static class StrengthMath
{
    /// <summary>Typical forearm length lever used for the "front-raise equivalent" (shoulder to hand ~0.55-0.65 m).</summary>
    public const double ArmLeverM = 0.6;

    public static LiveStrength Compute(WheelProfile p, double userTorqueNm, double velocityDps, int hands, double? maxTorqueNm)
    {
        hands = Math.Clamp(hands, 1, 2);
        double t = Math.Abs(userTorqueNm);
        double rimN = p.RimForceN(t);
        double perHandN = rimN / hands;
        double kgf = Units.NToKgf(rimN);
        double perHandKgf = Units.NToKgf(perHandN);
        // To transmit a tangential force F through skin friction mu you must squeeze with at least F/mu.
        double grip = perHandKgf / Math.Max(0.2, p.HandRimFriction);
        double power = userTorqueNm * velocityDps * Units.DegToRad;
        // At 3/9 o'clock the tangential force is vertical: the shoulder sees roughly the same moment as
        // holding a dumbbell of perHandKgf at the hand. Shown as "front-raise equivalent".
        double shoulderEquiv = perHandKgf;
        // Each hand also has to twist: share of wheel torque per forearm.
        double forearmShare = t / hands;
        double pct = maxTorqueNm is > 0.1 ? t / maxTorqueNm.Value * 100.0 : 0;
        return new LiveStrength(t, rimN, kgf, perHandKgf, grip, power, shoulderEquiv, forearmShare, pct, hands);
    }
}
