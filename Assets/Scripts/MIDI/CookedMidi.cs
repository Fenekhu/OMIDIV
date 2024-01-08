using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    public string name;
    public (byte lower, byte upper) pitchRange = (byte.MaxValue, byte.MinValue);
}

public class CookedMidi {
    public class TempoMap_ {
        public SortedDictionary<long, uint> _map; // <time micros, tempo micros>
        public (long timeMicros, uint tempoMicros) this[long timeMicros] {
            get { // this could probably be done nicely with linq
                return LTE(timeMicros) ?? (0, _map[0]);
            }
        }
        
        public (long timeMicros, uint tempoMicros) GetAtIndex(int index) {
            var kvp = _map.ElementAt(index);
            return (kvp.Key, kvp.Value);
        }
        public int? GTIndex(long timeMicros) {
            for (int i = 0; i < _map.Count; i++) {
                var kvp = _map.ElementAt(i);
                if (kvp.Key > timeMicros) return i;
            }
            return null;
        }
        public (long timeMicros, uint tempoMicros)? GT(long timeMicros) {
            for (int i = 0; i < _map.Count; i++) {
                var kvp = _map.ElementAt(i);
                if (kvp.Key > timeMicros) return (kvp.Key, kvp.Value);
            }
            return null;
        }
        public int? GTEIndex(long timeMicros) {
            for (int i = 0; i < _map.Count; i++) {
                var kvp = _map.ElementAt(i);
                if (kvp.Key >= timeMicros) return i;
            }
            return null;
        }
        public (long timeMicros, uint tempoMicros)? GTE(long timeMicros) {
            for (int i = 0; i < _map.Count; i++) {
                var kvp = _map.ElementAt(i);
                if (kvp.Key >= timeMicros) return (kvp.Key, kvp.Value);
            }
            return null;
        }
        public int? LTIndex(long timeMicros) {
            for (int i = 0; i < _map.Count; i++) {
                var kvp = _map.ElementAt(i);
                if (kvp.Key < timeMicros) return i;
            }
            return null;
        }
        public (long timeMicros, uint tempoMicros)? LT(long timeMicros) {
            for (int i = 0; i < _map.Count; i++) {
                var kvp = _map.ElementAt(i);
                if (kvp.Key < timeMicros) return (kvp.Key, kvp.Value);
            }
            return null;
        }
        public int? LTEIndex(long timeMicros) {
            for (int i = 0; i < _map.Count; i++) {
                var kvp = _map.ElementAt(i);
                if (kvp.Key <= timeMicros) return i;
            }
            return null;
        }
        public (long timeMicros, uint tempoMicros)? LTE(long timeMicros) {
            for (int i = 0; i < _map.Count; i++) {
                var kvp = _map.ElementAt(i);
                if (kvp.Key <= timeMicros) return (kvp.Key, kvp.Value);
            }
            return null;
        }

        public TempoMap_() {
            _map = new SortedDictionary<long, uint>();
            _map[0] = 500000;
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
        Header = raw.header;
        Tracks.Clear();

        CookTempoMap(raw);

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
    }

    private void CookTempoMap(RawMidi raw) {
        TempoMap._map.Clear();
        TempoMap._map[0] = 500000;

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

        long currentTime = 0;
        uint currentTempo = 500000;
        if (Header.fmt == EMidiDivisionFormat.TPQN) {
            foreach (var kvp in tempoMapByTick) {
                currentTime += currentTempo * kvp.Value.delta / Header.ticksPerQuarter;
                currentTempo = kvp.Value.tempoMicros;
                TempoMap._map[currentTime] = currentTempo;
            }
        } else { // SMPTE
            foreach (var kvp in tempoMapByTick) {
                currentTime += 1_000_000 * kvp.Value.delta / (-Header.smpte * Header.ticksPerFrame);
                currentTempo = kvp.Value.tempoMicros;
                TempoMap._map[currentTime] = currentTempo;
            }
        }
    }
}
