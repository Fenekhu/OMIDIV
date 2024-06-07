using System;

public abstract class RecorderController : OmidivComponent {

    public event Action OnRecordingBegin;
    public event Action OnBeforeFrame;
    public event Action OnAfterFrame;
    public event Action OnRecordingEnd;

    public bool RecordingEnabled { get; set; } = false;

    public enum Status {
        Standby, Recording, Processing, ProcessingBackground
    }

    public abstract Status GetStatus();
    public abstract double GetFramerate();

    public void StartIfEnabled() {
        if (RecordingEnabled) StartRecording();
    }
    public void StopIfEnabled() {
        if (RecordingEnabled) StopRecording(); 
    }

    public abstract void StartRecording();
    public abstract void StopRecording();

    // events cant be called directly from subclasses
    protected void FireOnRecordingBegin() {
        OnRecordingBegin?.Invoke();
    }
    protected void FireOnBeforeFrame() {
        OnBeforeFrame?.Invoke();
    }
    protected void FireOnAfterFrame() {
        OnAfterFrame?.Invoke();
    }
    protected void FireOnRecordingEnd() {
        OnRecordingEnd?.Invoke();
    }

    protected override void OnPlayStart() {
        StartIfEnabled();
    }
    protected override void OnPlayStop() {
        StopIfEnabled();
    }

}
