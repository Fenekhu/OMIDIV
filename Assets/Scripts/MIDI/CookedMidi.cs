using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// The midi information for one note.
/// </summary>
public class MidiNote {
    public ulong startTick;
    public ulong lengthTicks;
    public ulong startMicro;
    public ulong lengthMicros;
    public byte pitch;
    public byte velocity;
    public byte channel;

    public ulong endTick { get { return startTick + lengthTicks; } }
    public ulong endMicro { get { return startMicro + lengthMicros; } }
}

/// <summary>
/// Represents all the relevant information in a midi track.
/// </summary>
public class Track {
    public List<MidiNote> notes = new List<MidiNote>();
    public string name = "";
    public (byte lower, byte upper) pitchRange = (byte.MaxValue, byte.MinValue);
}

/// <summary>
/// Represents all the relevant information in a midi file in ways that are easier to work with than in a <see cref="RawMidi"/>.
/// </summary>
public class CookedMidi {

    /// <summary>
    /// Stores the tempo changes within the midi.
    /// </summary>
    public class TempoMap_ {
        private Dictionary<long, uint> tempMap = new Dictionary<long, uint>(); // <time micros, tempo micros>.
        private SortedList<long, uint> _map = new SortedList<long, uint>(); // <time micros, tempo micros>
        private List<long> keyList = new List<long>();

        public void Clear() {
            tempMap.Clear();
            _map.Clear();
            keyList.Clear();
        }

        /// <remarks>Changes will not be applied until <see cref="UpdateChanges"/> is called.</remarks>
        public void Set(long timeMicros, uint tempoMicros) {
            tempMap[timeMicros] = tempoMicros;
        }

        public int Count => _map.Count;

        /// <summary>
        /// Applies changes made with <see cref="Set"/>.
        /// </summary>
        public void UpdateChanges() {
            double realTime = Time.realtimeSinceStartupAsDouble;
            Debug.Log($"Starting UpdateChanges");
            _map = new SortedList<long, uint>(tempMap);
            Debug.Log($"UpdateChanges _map created (took {Time.realtimeSinceStartupAsDouble - realTime}s)");
            realTime = Time.realtimeSinceStartupAsDouble;
            keyList = new List<long>(_map.Keys);
            Debug.Log($"UpdateChanges keyList created (took {Time.realtimeSinceStartupAsDouble - realTime}s)");
        }

        /// <summary>
        /// Returns the tempo at a given time in microseconds, as well as the time where it is set.
        /// </summary>
        public (long timeMicros, uint tempoMicros) this[long timeMicros] {
            get { // this could probably be done nicely with linq
                return LTE(timeMicros) ?? (0, _map[0]);
            }
        }
        
        /// <returns>The nth tempo change's time and tempo.</returns>
        public (long timeMicros, uint tempoMicros) GetAtIndex(int index) {
            try {
                var kvp = _map.ElementAt(index);
                return (kvp.Key, kvp.Value);
            } catch (ArgumentOutOfRangeException e) {
                Debug.LogErrorFormat("Index out of bounds: {0:d} (size: {1:d})", index, _map.Count);
                throw e;
            }
        }

        /// <returns>
        /// The index of the next tempo change after (not including) <paramref name="timeMicros"/>, or null if there is no next tempo change.
        /// </returns>
        public int? GTIndex(long timeMicros) {
            int res = keyList.BinarySearch(timeMicros);
            if (res >= 0) return res == keyList.Count-1 ? null : res+1; // if timeMicros is a tempo change, return the next one, or null if there is no next one.
            res = ~res; // res is now the index of the next tempo change, or Count if there is no next.
            return res == keyList.Count ? null : res;
            // either way, null is returned if there is no next tempo (either timeMicros was the last tempo change or it was after the last tempo change)
        }

        /// <returns>
        /// The next tempo change after (not including) <paramref name="timeMicros"/>, or null if there is no next tempo change.
        /// </returns>
        public (long timeMicros, uint tempoMicros)? GT(long timeMicros) {
            int? index = GTIndex(timeMicros);
            if (!index.HasValue) return null;
            return GetAtIndex(index.Value);
        }

        /// <returns>
        /// The index of the tempo change at or after <paramref name="timeMicros"/>, or null if there is no next tempo change.
        /// </returns>
        public int? GTEIndex(long timeMicros) {
            int res = keyList.BinarySearch(timeMicros);
            if (res >= 0) return res; // this is the "E" part: if timeMicros is a tempo change, return it.
            res = ~res; // res is now the index of the next tempo change, or Count if there is no next.
            return res == keyList.Count ? null : res; 
        }

        /// <returns>
        /// The tempo change at or after <paramref name="timeMicros"/>, or null if there is no next tempo change.
        /// </returns>
        public (long timeMicros, uint tempoMicros)? GTE(long timeMicros) {
            int? index = GTEIndex(timeMicros);
            if (!index.HasValue) return null;
            return GetAtIndex(index.Value);
        }

        /// <returns>
        /// The index of the previous tempo change before (not including) <paramref name="timeMicros"/>, or null if there is no previous tempo change.
        /// </returns>
        public int? LTIndex(long timeMicros) {
            int res = keyList.BinarySearch(timeMicros);
            if (res >= 0) return res == 0? null : res-1; // if timeMicros is a tempo change, return the previous one, or null if there is no previous one.
            res = (~res) - 1; // res is now the index of the previous tempo change.
                              // Note: if res was -1, then timeMicros was before any tempo changes and there is no previous one.
                              // ~(-1) = 0, 0 - 1 = -1. If it was -1, it will remain -1.
            return res < 0 ? null : res; // if res was -1, there was no previous tempo change.
        }

        /// <returns>
        /// The previous tempo change before (not including) <paramref name="timeMicros"/>, or null if there is no previous tempo change.
        /// </returns>
        public (long timeMicros, uint tempoMicros)? LT(long timeMicros) {
            int? index = LTIndex(timeMicros);
            if (!index.HasValue) return null;
            return GetAtIndex(index.Value);
        }

        /// <returns>
        /// The index of the tempo change at or before <paramref name="timeMicros"/>, or null if there is no previous tempo change.
        /// </returns>
        public int? LTEIndex(long timeMicros) {
            int res = keyList.BinarySearch(timeMicros);
            if (res >= 0) return res; // this is the "E" part: if timeMicros is a tempo change, return it.
            res = (~res) - 1; // res is now the index of the previous tempo change.
                              // Note: if res was -1, then timeMicros was before any tempo changes and there is no previous one.
                              // ~(-1) = 0, 0 - 1 = -1. If it was -1, it will remain -1.
            return res < 0? null : res; // if res was -1, there was no previous tempo change.
        }

        /// <returns>
        /// The tempo change at or before <paramref name="timeMicros"/>, or null if there is no previous tempo change.
        /// </returns>
        public (long timeMicros, uint tempoMicros)? LTE(long timeMicros) {
            int? index = LTEIndex(timeMicros);
            if (!index.HasValue) return null;
            return GetAtIndex(index.Value);
        }

        public TempoMap_() {
            tempMap[0] = 500000;
            _map[0] = 500000;
            keyList.Add(0);
        }
    }

    public HeaderChunk Header { get; private set; } = new HeaderChunk();
    public List<Track> Tracks { get; private set; } = new List<Track>();
    public TempoMap_ TempoMap { get; private set; } = new TempoMap_();
    public byte HighestNote { get; private set; }
    public byte LowestNote { get; private set; }
    public byte NoteRange { get { return (byte)(HighestNote - LowestNote); } }

    public CookedMidi() {}

    public CookedMidi(RawMidi raw) {
        Cook(raw);
    }

    /// <summary>
    /// Processes a <see cref="RawMidi"/>.
    /// </summary>
    /// <remarks>
    /// If you need to extract other information from the midi to use in a visualization, do it by modifying this function.
    /// </remarks>
    public void Cook(RawMidi raw) {
        Debug.Log("Starting Cook");
        Header = raw.header;
        Tracks.Clear();

        CookTempoMap(raw);

        Debug.Log("Cook reading raw midi");
        double realTime = Time.realtimeSinceStartupAsDouble;

        // One dictionary for each channel.
        // When processing the raw midi, when a note-on message is hit, maps the pitch to the in-progress note object.
        // Later, when the note-off message is hit, the length of that note can be calculated,
        // and the note is moved out of this dictionary into the list of notes in the track.
        // If another note-on is hit on the same pitch before the note-off, according to the midi specification,
        // the active note will immediately end and a new one will start.
        Dictionary<uint, MidiNote>[] activeNotes = new Dictionary<uint, MidiNote>[16];
        for (int i = 0; i < activeNotes.Length; i++) activeNotes[i] = new Dictionary<uint, MidiNote>();

        foreach (TrackChunk chunk in raw.tracks) {
            Tracks.Add(new Track());
            Track track = Tracks.Last();
            uint currentTick = 0;
            long currentTime = 0;

            foreach (MTrkEvent @event in chunk.events) {
                currentTick += @event.delta;
                // TODO: SMPTE
                currentTime += @event.delta * TempoMap[currentTime].tempoMicros / Header.ticksPerQuarter;

                byte eventType = @event.EventType;
                if (eventType == 0xFF && @event is MetaEvent) {
                    MetaEvent me = (MetaEvent) @event;
                    if (me.type == (byte)EMidiMetaEvent.TrackName) {
                        track.name = System.Text.Encoding.UTF8.GetString(me.data);
                    }
                } else {
                    if (@event is MidiNoteEvent) {
                        MidiNoteEvent noteEvent = (MidiNoteEvent) @event;
                        Dictionary<uint, MidiNote> chActive = activeNotes[noteEvent.Channel];
                        switch (noteEvent.Status) {
                        case EMidiEventStatus.NoteOn: {
                            if (noteEvent.Vel == 0) goto case EMidiEventStatus.NoteOff; // according to the midi specification, note-on with velocity 0 is actually a note-off.
                            track.pitchRange.lower = Math.Min(track.pitchRange.lower, noteEvent.Key);
                            track.pitchRange.upper = Math.Max(track.pitchRange.upper, noteEvent.Key);
                            if (chActive.TryGetValue(noteEvent.Key, out MidiNote note)) { // if we're starting a new note but a note is already playing on this pitch and channel.
                                note.lengthTicks = currentTick - note.startTick;
                                note.lengthMicros = (ulong)currentTime - note.startMicro;
                                if (note.lengthTicks != 0)
                                    track.notes.Add(note);
                            }
                            MidiNote note1 = new MidiNote();
                            note1.startTick = currentTick;
                            note1.lengthTicks = 0;
                            note1.startMicro = (ulong)currentTime;
                            note1.lengthMicros = 0;
                            note1.pitch = noteEvent.Key;
                            note1.velocity = noteEvent.Vel;
                            note1.channel = (byte)noteEvent.Channel;
                            chActive[noteEvent.Key] = note1;
                            break;
                        }
                        case EMidiEventStatus.NoteOff: {
                            if (chActive.TryGetValue(noteEvent.Key,out MidiNote note)) {
                                note.lengthTicks = currentTick - note.startTick;
                                note.lengthMicros = (ulong)currentTime - note.startMicro;
                                if (note.lengthTicks != 0) // I've found midis with 0-length notes for some reason. here is the best place to deal with them.
                                    track.notes.Add(note);
                                chActive.Remove(noteEvent.Key);
                            }
                            break;
                        }
                        }
                    }
                }
            }

            if (track.name.Length == 0) track.name = string.Format("Track {0:d}", Tracks.Count);
            LowestNote = Math.Min(track.pitchRange.lower, LowestNote);
            HighestNote = Math.Max(track.pitchRange.upper, HighestNote);
#if DEBUG
            for (int i = 0; i < activeNotes.Count(); i++) {
                var chActive = activeNotes[i];
                if (chActive.Count > 0) {
                    Debug.LogWarningFormat("Channel {0:d} ended with {1:d} active notes:", i, chActive.Count);
                    foreach (var kvp in chActive) {
                        Debug.LogWarningFormat("pitch: {0:d}  startTick: {:d}", kvp.Key, kvp.Value);
                    }
                }
            }
#endif

            track.notes.Sort((n1, n2) => n1.startTick.CompareTo(n2.startTick));
        }

        Debug.Log($"Cook finished reading raw midi (took {Time.realtimeSinceStartupAsDouble - realTime}s)");
    }

    private void CookTempoMap(RawMidi raw) {
        double realTime = Time.realtimeSinceStartupAsDouble;
        Debug.Log("Starting CookTempoMap");
        TempoMap.Clear();
        TempoMap.Set(0, 500000);

        // Since tempo events can technically be in any track,
        // we can't convert ticks to time until we're sure we have every tempo change.
        // We need to iterate through all the tempo changes in order later, the the storage must be in order.
        // SortedList has faster insertion than SortedDictionary if elements are provided in order.
        // Tempo changes are usually in the first midi track, so they will probably be provided in order.
        SortedList<long, uint> tempoMapByTick = new SortedList<long, uint>(); // <tick, tempo micros>

        foreach (TrackChunk chunk in raw.tracks) {
            long currentTick = 0;
            foreach (MTrkEvent @event in chunk.events) {
                currentTick += @event.delta;

                byte eventType = @event.EventType;
                if (eventType == 0xff && @event is MetaEvent) {
                    MetaEvent me = (MetaEvent) @event;
                    if (me.type == (byte)EMidiMetaEvent.Tempo) {
                        // the delta will be calculated later, after we have scanned all the changes.
                        tempoMapByTick[currentTick] = MidiUtil.TempoMicros(me.data);
                    }
                }
            }
        }

        Debug.Log($"CookTempoMap converting ticks to micros (took {Time.realtimeSinceStartupAsDouble - realTime}s)");
        realTime = Time.realtimeSinceStartupAsDouble;

        long lastTempoTick = 0;
        long currentTime = 0;
        uint currentTempo = 500000;
        if (Header.fmt == EMidiDivisionFormat.TPQN) {
            foreach (var kvp in tempoMapByTick) {
                long delta = kvp.Key - lastTempoTick;
                currentTime += currentTempo * delta / Header.ticksPerQuarter;
                currentTempo = kvp.Value;
                TempoMap.Set(currentTime, currentTempo);
            }
        } else { // SMPTE
            foreach (var kvp in tempoMapByTick) {
                long delta = kvp.Key - lastTempoTick;
                currentTime += 1_000_000 * delta / (-Header.smpte * Header.ticksPerFrame);
                currentTempo = kvp.Value;
                TempoMap.Set(currentTime, currentTempo);
            }
        }

        TempoMap.UpdateChanges();

        Debug.Log($"CookTempoMap finished (took {Time.realtimeSinceStartupAsDouble - realTime}s)");
    }
}
