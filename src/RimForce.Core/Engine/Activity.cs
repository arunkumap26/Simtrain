using RimForce.Core.Safety;

namespace RimForce.Core.Engine;

/// <summary>Everything an activity (a drill session, a device test, free ride) sees each force-loop tick.</summary>
public readonly record struct ActivityTick(
    double Time,
    double Dt,
    double AngleDeg,
    double VelocityDps,
    double AccelDps2,
    /// <summary>Torque the motor is outputting after the governor (Nm, + = clockwise).</summary>
    double MotorTorqueNm,
    /// <summary>Estimated torque the user puts into the wheel (Nm, + = clockwise).</summary>
    double UserTorqueNm,
    double Envelope,
    bool Armed,
    /// <summary>Master intensity multiplier (0.1 - 1.5), the RimForce equivalent of a game's FFB strength slider.</summary>
    double Intensity);

public readonly record struct ActivityStep(TorqueRequest Request, double GuideTargetDeg)
{
    public static readonly ActivityStep Idle = new(TorqueRequest.None, 0);
}

/// <summary>Something that runs on the force loop and wants torque: a training session, a device test, ...</summary>
public interface IActivity
{
    string Kind { get; }
    bool IsFinished { get; }
    ActivityStep Step(in ActivityTick tick);
    /// <summary>Called when the governor trips or the user stops. The activity should pause/abort its current set.</summary>
    void OnSafetyStop(string reason);
    /// <summary>Plain DTO for the UI (serialised to JSON). Called at UI rate, on the loop thread.</summary>
    object Snapshot();
    /// <summary>Events for the UI (cues, set start/end, results). Drained by the engine.</summary>
    IReadOnlyList<ActivityEvent> DrainEvents();
}

public sealed record ActivityEvent(string Kind, object? Data = null);
