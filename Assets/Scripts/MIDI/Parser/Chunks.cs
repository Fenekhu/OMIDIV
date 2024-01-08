using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Debug = UnityEngine.Debug;

public class Chunk {
    public uint type = 0;
    public uint length = 0;

    public Chunk() { }
    public Chunk(EMidiChunkType type, uint length) {
        this.type = (uint)type;
        this.length = length;
    }
    public Chunk(int type, uint length) {
        this.type = (uint)type;
        this.length = length;
    }
    public Chunk(uint type, uint length) {
        this.type = type;
        this.length = length;
    }
    public Chunk(byte[] type, uint length) {
        this.type = BitConverter.ToUInt32(type, 0);
        this.length = length;
    }
}

public class HeaderChunk : Chunk {
    public ushort format = 0;
    public ushort ntrks = 0;

    public ushort divisionU = 0;
    public int ticksPerQuarter {
        get { return divisionU & 0x7FFF; }
        set { divisionU = (ushort)(value & 0x7FFF); }
    }
    public EMidiDivisionFormat fmt {
        get { return (EMidiDivisionFormat)((divisionU & 0x8000) >> 15); }
        set { if (value == EMidiDivisionFormat.TPQN) { divisionU &= 0x7FFF; } else if (value == EMidiDivisionFormat.SMPTE) { divisionU |= 0x8000; } }
    }
    public byte ticksPerFrame {
        get { return (byte)(divisionU >> 8); }
        set { divisionU = (ushort)((divisionU & 0x00FF) + (value << 8)); }
    }

    public sbyte smpte {
        get { return (sbyte)(divisionU & 0x00FF); }
        set { divisionU = (ushort)((divisionU & 0xFF00) + value); }
    }

    public HeaderChunk() : base(EMidiChunkType.Header, 6) { }
    public HeaderChunk(FileStream file) : this() {
        if (!MidiUtil.ReadU32(file, out length)) return;
        long pos = file.Position;
        if (!MidiUtil.ReadU16(file, out format)) return;
        if (!MidiUtil.ReadU16(file, out ntrks)) return;
        if (!MidiUtil.ReadU16(file, out divisionU)) return;
        file.Seek(pos + length, SeekOrigin.Begin);

        if (format >= (ushort)EMidiFormat.Two) {
            Debug.LogFormat("Unsupported MIDI format: {0:d}", format);
        }
    }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
    public void DebugPrint(StreamWriter sw) {
        string msg = string.Format("{0:s}  len:{1:d}  format:{2:d}  ntrks:{3:d}  ", System.Text.Encoding.UTF8.GetString(BitConverter.GetBytes(type)), length, format, ntrks);
        if (fmt == EMidiDivisionFormat.TPQN) {
            msg += string.Format("ticks/qtr:{0:d}", ticksPerQuarter);
        } else if (fmt == EMidiDivisionFormat.SMPTE) {
            msg += string.Format("fps:{0:d}  ticks/frame:{1:d}", -smpte, ticksPerFrame);
        }

        if (sw is null) {
            Debug.Log(msg);
        } else {
            sw.WriteLine(msg);
        }
    }
#endif
}

public class TrackChunk : Chunk {
    public List<MTrkEvent> events = new List<MTrkEvent>();

    public TrackChunk() : base(EMidiChunkType.Track, 0) { }
    public TrackChunk(FileStream file) : this() {
        if (!MidiUtil.ReadU32(file, out length)) return;
        long pos = file.Position;
        bool eot = false;
        while (file.Good() && !eot) {
            MTrkEvent @event = MTrkEvent.Create(file);
            if (@event is null) {
                Debug.Log("@event was null");
                continue;
            }
            if (@event is MetaEvent) {
                MetaEvent me = (MetaEvent) @event;
                eot = (me.ff == 0xFF && me.type == (byte)EMidiMetaEvent.EOT);
            }
            events.Add(@event);
        }
        file.Seek(pos + length, SeekOrigin.Begin);
    }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
    public void DebugPrint(StreamWriter sw) {
        string msg = string.Format("{0:s}  len:{1:d}", System.Text.Encoding.UTF8.GetString(BitConverter.GetBytes(type)), length);
        if (sw is null) {
            Debug.Log(msg);
        } else {
            sw.WriteLine(msg);
        }

        foreach (MTrkEvent @event in events) {
            @event.DebugPrint(sw);
        }
    }
#endif
}
