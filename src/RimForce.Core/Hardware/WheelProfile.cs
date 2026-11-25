namespace RimForce.Core.Hardware;

/// <summary>
/// Describes the physical wheel setup so percentages can be turned into real torque and force.
/// All drill loads are computed in Nm; the device layer receives percent of DirectInput full scale.
///
///   max torque at DI 100%  = BaseMaxTorqueNm x SimProStrengthPct/100
///   rim tangential force   = torque / rim radius
/// </summary>
public sealed record WheelProfile
{
    /// <summary>Peak torque of the wheelbase at 100% (Alpha EVO Sport 9, Alpha EVO 12, Alpha EVO Pro 18).</summary>
    public double BaseMaxTorqueNm { get; init; } = 12.0;

    /// <summary>SimPro Manager overall strength for the profile used while training (0-100).</summary>
    public double SimProStrengthPct { get; init; } = 100.0;

    /// <summary>Rim diameter in millimetres (outer diameter, where your hands are).</summary>
    public double RimDiameterMm { get; init; } = 300.0;

    /// <summary>Total rotation configured in SimPro (lock to lock), e.g. 900.</summary>
    public double RotationRangeDeg { get; init; } = 900.0;

    /// <summary>Estimated moment of inertia of rotor + rim (kg m^2). Used for the user-torque estimate and the simulator.</summary>
    public double InertiaKgM2 { get; init; } = 0.045;

    /// <summary>Friction coefficient between hand and rim (leather/alcantara ~0.6-0.9). Used for the grip-force estimate.</summary>
    public double HandRimFriction { get; init; } = 0.7;

    /// <summary>Flip the sign of every force command. Determined by the direction check in Device Test.</summary>
    public bool InvertForce { get; init; }

    /// <summary>Flip the sign of the wheel angle reading.</summary>
    public bool InvertAngle { get; init; }

    public double RimRadiusM => RimDiameterMm / 2000.0;
    public double LockDeg => RotationRangeDeg / 2.0;
    public double MaxTorqueNm => BaseMaxTorqueNm * Math.Clamp(SimProStrengthPct, 0, 100) / 100.0;

    public double NmToPercent(double nm) => MaxTorqueNm <= 0 ? 0 : nm / MaxTorqueNm * 100.0;
    public double PercentToNm(double pct) => pct / 100.0 * MaxTorqueNm;

    /// <summary>Tangential force at the rim (N) for a torque.</summary>
    public double RimForceN(double nm) => RimRadiusM <= 0 ? 0 : nm / RimRadiusM;

    public static WheelProfile AlphaEvo12 => new();
}

public static class Units
{
    public const double G = 9.80665;
    public const double DegToRad = Math.PI / 180.0;
    public const double RadToDeg = 180.0 / Math.PI;
    public static double NToKgf(double n) => n / G;
    public static double NToLbf(double n) => n / 4.4482216;
}
