using ImGuiNET;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// The base class for a component that draws a visualization.
/// </summary>
public abstract class VisualsComponent : OmidivComponent {

    public static readonly SortedList<int, Color> TrackColors = new SortedList<int, Color>();

    /// Populates <see cref="TrackColors"/>
    static VisualsComponent() {
        for (int i = 0; i < 24; i++) {
            TrackColors[i] = Color.HSVToRGB(i / 24f, 1, 0.5f);
        }
    }



    /// <summary>
    /// Whether changes that require recreating the visualization should apply automatically (true) or manually with F6 (false).
    /// </summary>
    /// <remarks>
    /// Note to subclasses:<br/>
    /// Doesn't make changes you make automatically apply. Instead, chack the value of this for whether or not you should apply the changes when they happen.
    /// </remarks>
    public bool AutoApplyChanges { get; set; } = true;

    /// <summary>
    /// Create your visualization here. Happens every time visuals reload after a call to <see cref="ClearVisuals"/>.
    /// </summary>
    protected abstract void CreateVisuals();

    /// <summary>Clear all created visuals here.</summary>
    /// <remarks>May be called before visuals have been created, so don't assume the visuals will be there.</remarks>
    protected abstract void ClearVisuals();

    /// <summary>
    /// Move your visualization forward or backward (in case midi delay is adjusted).
    /// </summary>
    /// <param name="ticks">Will usually be 1 except if the midi uses SMPTE time format, or the midi delay is adjusted.</param>
    protected abstract void MovePlay(decimal ticks, decimal microseconds);

    protected override void OnEnable() {
        base.OnEnable();
        MidiManager.OnMidiDelayChanged += OnMidiDelayChanged;
    }

    protected override void OnDisable() {
        base.OnDisable();
        MidiManager.OnMidiDelayChanged -= OnMidiDelayChanged;
    }

    /// <remarks>
    /// Clears visuals by default.<br/>
    /// Please call <c>base.OnDestroy()</c> if overriding this, unless you really know what you're doing.
    /// </remarks>
    protected override void OnDestroy() {
        base.OnDestroy();
        ClearVisuals();
    }

    /// <remarks>Please call <c>base.ReadConfig()</c> if overriding this unless you know what you're doing.</remarks>
    protected override void ReadConfig() {
        Config.TryGet<bool>(ConfigTag+".autoApply", val => AutoApplyChanges = val);
    }

    /// <remarks>Please call <c>base.WriteConfig()</c> if overriding this unless you know what you're doing.</remarks>
    protected override void WriteConfig() {
        Config.Set(ConfigTag+".autoApply", AutoApplyChanges);
    }

    private void OnMidiDelayChanged(long deltaMicros, decimal tickDelta) {
        MovePlay(tickDelta, deltaMicros);
    }

    /// <remarks>Please call <c>base.Update()</c> if overriding this unless you know what you're doing.</remarks>
    protected virtual void Update() {
        if (!IsPlaying) return;

        MovePlay(MidiManager.TicksPerFrame, (decimal)FrameDeltaTime * 1e6m);
    }

    /// <summary>Clears and creates the visuals.</summary>
    /// <remarks>Please call <c>base.LoadVisuals()</c> if overriding this.</remarks>
    protected override void LoadVisuals() {
        ClearVisuals();
        CreateVisuals();
    }

    /// <remarks>Please call <c>base.DrawGUI()</c> if overriding this.</remarks>
    protected override void DrawGUI() {
        if (ImGui.Begin("Misc Controls")) {
            bool _autoReload = AutoApplyChanges;
            if (ImGui.Checkbox("Auto-apply certain changes", ref _autoReload)) AutoApplyChanges = _autoReload;
        }
        ImGui.End();
    }

    protected override void Restart() {
        decimal micros = MidiManager.MidiDelay * -1000m;
        MovePlay(MidiManager.MicrosToTicks(0, micros), micros);
    }
}