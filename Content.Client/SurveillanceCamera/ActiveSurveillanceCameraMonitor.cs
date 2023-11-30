namespace Content.Client.SurveillanceCamera;

[RegisterComponent]
public sealed class ActiveSurveillanceCameraMonitorVisualsComponent : Component
{
    public float TimeLeft = 2.5f;

    public Action? OnFinish;
}
