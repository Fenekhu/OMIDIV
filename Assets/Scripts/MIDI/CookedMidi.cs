using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Debug = UnityEngine.Debug;

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

public class Track {
    public List<MidiNote> notes = new List<MidiNote>();
    public string name = "";
    public (byte lower, byte upper) pitchRange = (byte.MaxValue, byte.MinValue);
}

public class CookedMidi {
    public class TempoMap_ {
        private Dictionary<long, uint> tempMap = new Dictionary<long, uint>();
        private SortedDictionary<long, uint> _map = new SortedDictionary<long, uint>(); // <time micros, tempo micros>
        private List<long> keyList = new List<long>();

        public void Clear() {
            tempMap.Clear();
            _map.Clear();
            keyList.Clear();
        }
        public void Set(long timeMicros, uint tempoMicros, bool autoUpdate = true) {
            tempMap[timeMicros] = tempoMicros;
            if (autoUpdate) UpdateChanges();
        }
        public int Count => _map.Count;
        public void UpdateChanges() {
            double realTime = Time.realtimeSinceStartupAsDouble;
            Debug.Log($"Starting UpdateChanges");
            _map = new SortedDictionary<long, uint>(tempMap);
            Debug.Log($"UpdateChanges _map created (took {Time.realtimeSinceStartupAsDouble - realTime}s)");
            realTime = Time.realtimeSinceStartupAsDouble;
            keyList = new List<long>(_map.Keys);
            Debug.Log($"UpdateChanges keyList created (took {Time.realtimeSinceStartupAsDouble - realTime}s)");
        }

        public (long timeMicros, uint tempoMicros) this[long timeMicros] {
            get { // this could probably be done nicely with linq
                return LTE(timeMicros) ?? (0, _map[0]);
            }
        }
        
        public (long timeMicros, uint tempoMicros) GetAtIndex(int index) {
            try {
                var kvp = _map.ElementAt(index);
                return (kvp.Key, kvp.Value);
            } catch (ArgumentOutOfRangeException e) {
                Debug.LogErrorFormat("Index out of bounds: {0:d} (size: {1:d})", index, _map.Count);
                throw e;
            }
        }
        public int? GTIndex(long timeMicros) {
            int res = keyList.BinarySearch(timeMicros);
            if (res >= 0) return res == keyList.Count-1 ? null : res+1;
            res = ~res;
            return res == keyList.Count ? null : res;
        }
        public (long timeMicros, uint tempoMicros)? GT(long timeMicros) {
            int? index = GTIndex(timeMicros);
            if (!index.HasValue) return null;
            return GetAtIndex(index.Value);
        }
        public int? GTEIndex(long timeMicros) {
            int res = keyList.BinarySearch(timeMicros);
            if (res >= 0) return res;
            res = ~res;
            return res == keyList.Count ? null : res;
        }
        public (long timeMicros, uint tempoMicros)? GTE(long timeMicros) {
            int? index = GTEIndex(timeMicros);
            if (!index.HasValue) return null;
            return GetAtIndex(index.Value);
        }
        public int? LTIndex(long timeMicros) {
            int res = keyList.BinarySearch(timeMicros);
            if (res >= 0) return res == 0? null : res-1;
            res = ~res;
            return res-1;
        }
        public (long timeMicros, uint tempoMicros)? LT(long timeMicros) {
            int? index = LTIndex(timeMicros);
            if (!index.HasValue) return null;
            return GetAtIndex(index.Value);
        }
        public int? LTEIndex(long timeMicros) {
            int res = keyList.BinarySearch(timeMicros);
            if (res >= 0) return res == 0 ? null : res;
            res = ~res;
            return res-1;
        }
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

    public void Cook(RawMidi raw) {
        Debug.Log("Starting Cook");
        Header = raw.header;
        Tracks.Clear();

        CookTempoMap(raw);

        Debug.Log("Cook reading raw midi");
        double realTime = Time.realtimeSinceStartupAsDouble;

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
                            if (noteEvent.Vel == 0) goto case EMidiEventStatus.NoteOff;
                            track.pitchRange.lower = Math.Min(track.pitchRange.lower, noteEvent.Key);
                            track.pitchRange.upper = Math.Max(track.pitchRange.upper, noteEvent.Key);
                            if (chActive.TryGetValue(noteEvent.Key, out MidiNote note)) {
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
        TempoMap.Set(0, 500000, false);

        Dictionary<long, (long delta, uint tempoMicros)> tempoMapByTick = new Dictionary<long, (long, uint)>(); // <tick, (delta, tempo micros)>

        foreach (TrackChunk chunk in raw.tracks) {
            long currentTick = 0;
            long lastTempoTick = 0;
            foreach (MTrkEvent @event in chunk.events) {
                currentTick += @event.delta;

                byte eventType = @event.EventType;
                if (eventType == 0xff && @event is MetaEvent) {
                    MetaEvent me = (MetaEvent) @event;
                    if (me.type == (byte)EMidiMetaEvent.Tempo) {
                        tempoMapByTick[currentTick] = (currentTick - lastTempoTick, MidiUtil.TempoMicros(me.data));
                        lastTempoTick = currentTick;
                    }
                }
            }
        }

        Debug.Log($"CookTempoMap converting ticks to micros (took {Time.realtimeSinceStartupAsDouble - realTime}s)");
        realTime = Time.realtimeSinceStartupAsDouble;

        long currentTime = 0;
        uint currentTempo = 500000;
        if (Header.fmt == EMidiDivisionFormat.TPQN) {
            foreach (var kvp in tempoMapByTick) {
                currentTime += currentTempo * kvp.Value.delta / Header.ticksPerQuarter;
                currentTempo = kvp.Value.tempoMicros;
                TempoMap.Set(currentTime, currentTempo, false);
            }
        } else { // SMPTE
            foreach (var kvp in tempoMapByTick) {
                currentTime += 1_000_000 * kvp.Value.delta / (-Header.smpte * Header.ticksPerFrame);
                currentTempo = kvp.Value.tempoMicros;
                TempoMap.Set(currentTime, currentTempo, false);
            }
        }

        TempoMap.UpdateChanges();

        Debug.Log($"CookTempoMap finished (took {Time.realtimeSinceStartupAsDouble - realTime}s)");
    }
}
