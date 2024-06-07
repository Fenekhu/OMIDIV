using System;
using System.Buffers.Binary;
using System.IO;
using Debug = UnityEngine.Debug;

/// <summary>
/// A base class for events in a track.
/// </summary>
public abstract class MTrkEvent {
    private static byte lastMsg;

    public uint delta { get; set; }

    public abstract byte EventType { get; }

    public static MTrkEvent Create(FileStream file) {
        if (!MidiUtil.ReadBEVLV(file, out uint delta)) return null;
        if (!MidiUtil.ReadU8(file, out byte mtype)) return null;

        if (mtype == 0xFF) {// MIDI_EVENT_STATUS_SYSTEM << 4 + MIDI_EVENT_SYSCH_RESET; NOTE: in a MIDI file, this is interpreted as a meta event, but from a port its interpreted as a reset.
            MetaEvent @event = new MetaEvent(file);
            @event.delta = delta;
            return @event;
        } else if (mtype == 0xF0 || mtype == 0xF7) { // MIDI_EVENT_STATUS_SYSTEM << 4 + MIDI_EVENT_SYSCH_(SYSTEM_EXCLUSIVE or EXCLUSIVE_END)
            SysexEvent @event = new SysexEvent(mtype, file);
            @event.delta = delta;
            return @event;
        } else {
            MidiEvent.Message msg;
            msg.v = mtype;

            if ((msg.status & 0x08) == 0) {
                msg.v = lastMsg;
                file.Seek(-1, SeekOrigin.Current);
            }
            lastMsg = msg.v;

            switch ((EMidiEventStatus)msg.status) {
            case EMidiEventStatus.NoteOff:
            case EMidiEventStatus.NoteOn:
            case EMidiEventStatus.PolyKeyPressure: {
                MidiNoteEvent @event = new MidiNoteEvent();
                @event.delta = delta;
                @event.msg = msg;
                if (!MidiUtil.ReadU8(file, out byte data1)) return null;
                if (!MidiUtil.ReadU8(file, out byte data2)) return null;
                @event.data1 = data1;
                @event.data2 = data2;
                return @event;
            }
            case EMidiEventStatus.CtrlChange: {
                MidiControlEvent @event = new MidiControlEvent();
                @event.delta = delta;
                @event.msg = msg;
                if (!MidiUtil.ReadU8(file, out byte data1)) return null;
                if (!MidiUtil.ReadU8(file, out byte data2)) return null;
                @event.data1 = data1;
                @event.data2 = data2;
                return @event;
            }
            case EMidiEventStatus.PrgmChange: {
                MidiProgramChangeEvent @event = new MidiProgramChangeEvent();
                @event.delta = delta;
                @event.msg = msg;
                if (!MidiUtil.ReadU8(file, out byte data1)) return null;
                @event.data1 = data1;
                return @event;
            }
            case EMidiEventStatus.ChannelPressure: {
                MidiChannelPressureEvent @event = new MidiChannelPressureEvent();
                @event.delta = delta;
                @event.msg = msg;
                if (!MidiUtil.ReadU8(file, out byte data1)) return null;
                @event.data1 = data1;
                return @event;
            }
            case EMidiEventStatus.PitchWheel: {
                MidiPitchwheelEvent @event = new MidiPitchwheelEvent();
                @event.delta = delta;
                @event.msg = msg;
                if (!MidiUtil.ReadU8(file, out byte data1)) return null;
                if (!MidiUtil.ReadU8(file, out byte data2)) return null;
                @event.data1 = data1;
                @event.data2 = data2;
                return @event;
            }
            case EMidiEventStatus.System: {
                switch((EMidiEventSysCh)msg.channel) {
                case EMidiEventSysCh.SongPos: {
                    MidiSongPositionEvent event_ = new MidiSongPositionEvent();
                    event_.delta = delta;
                    event_.msg = msg;
                    if (!MidiUtil.ReadU8(file, out byte data1)) return null;
                    if (!MidiUtil.ReadU8(file, out byte data2)) return null;
                    event_.data1 = data1;
                    event_.data2 = data2;
                    return event_;
                }
                case EMidiEventSysCh.SongSelect: {
                    MidiSongSelectEvent event_ = new MidiSongSelectEvent();
                    event_.delta = delta;
                    event_.msg = msg;
                    if (!MidiUtil.ReadU8(file, out byte data1)) return null;
                    event_.data1 = data1;
                    return event_;
                }
                case EMidiEventSysCh.Tune:
                case EMidiEventSysCh.Timing:
                case EMidiEventSysCh.Start:
                case EMidiEventSysCh.Resume:
                case EMidiEventSysCh.Stop:
                case EMidiEventSysCh.Sensing:
                    MidiEvent @event = new MidiEvent();
                    @event.delta = delta;
                    @event.msg = msg;
                    return @event;
                }
                break;
            }
            }

            Debug.LogFormat("unknown midi message: {0:d}, ({0:x})", msg.v);
            return null;
        }

    }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
    public abstract void DebugPrint(StreamWriter sw);
#endif

}

public class SysexEvent : MTrkEvent {
    public byte f = 0;
    public uint length = 0;
    public byte[] data;

    public override byte EventType { get { return f; } }

    public SysexEvent(byte firstByte, FileStream file) {
        f = firstByte;
        if (!MidiUtil.ReadBEVLV(file, out length)) return;

        long pos = file.Position;
        data = new byte[length];
        try {
            file.Read(data, 0, (int)length);
        } catch (Exception ex) when (ex is EndOfStreamException || ex is IOException) {
            MidiUtil.HandleReadError(ex);
            return;
        }
        file.Seek(pos + length, SeekOrigin.Begin);
    }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
    public override void DebugPrint(StreamWriter sw) {
        string msg = string.Format("        {0,-6:d}  sysex  type:{1:x}  len:{2:d}    data: ", delta, f, length);
        if (data is null) {
            msg += " null";
        } else for (int i = 0; i < length; i++) msg += string.Format(" {0:x}", data[i]);
        if (sw is null) {
            Debug.Log(msg);
        } else {
            sw.WriteLine(msg);
        }
    }
#endif
}

public class MetaEvent : MTrkEvent {
    public byte ff = 0;
    public byte type = 0;
    public uint length = 0;
    public byte[] data;

    public MetaEvent(FileStream file) {
        ff = 0xFF;
        data = new byte[0];
        if (!MidiUtil.ReadU8(file, out type)) return;
        if (!MidiUtil.ReadBEVLV(file, out length)) return;

        long pos = file.Position;
        data = new byte[length];
        if (length != 0) {
            try {
                file.Read(data, 0, (int)length);
            } catch (Exception ex) when (ex is EndOfStreamException || ex is IOException) {
                MidiUtil.HandleReadError(ex);
                return;
            }
            file.Seek(pos + length, SeekOrigin.Begin);
        }
    }

    public override byte EventType { get { return ff; } }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
    public override void DebugPrint(StreamWriter sw) {
        string msg = string.Format("        {0,-6:d}  meta  type:{1:x}  len:{2:d}  ", delta, type, length);
        
        switch ((EMidiMetaEvent)type) {
        case EMidiMetaEvent.SequenceNumber:
            msg += "sqn#:" + BinaryPrimitives.ReadInt16BigEndian(data);
            break;
        case EMidiMetaEvent.Text:
            msg += "text:\"" + System.Text.Encoding.UTF8.GetString(data) + "\"";
            break;
        case EMidiMetaEvent.Copyright:
            msg += "cpyr:\"" + System.Text.Encoding.UTF8.GetString(data) + "\"";
            break;
        case EMidiMetaEvent.TrackName:
            msg += "track:\"" + System.Text.Encoding.UTF8.GetString(data) + "\"";
            break;
        case EMidiMetaEvent.InstrName:
            msg += "instr:\"" + System.Text.Encoding.UTF8.GetString(data) + "\"";
            break;
        case EMidiMetaEvent.Lyric:
            msg += "lyric:\"" + System.Text.Encoding.UTF8.GetString(data) + "\"";
            break;
        case EMidiMetaEvent.Marker:
            msg += "mark:\"" + System.Text.Encoding.UTF8.GetString(data) + "\"";
            break;
        case EMidiMetaEvent.Cue:
            msg += "cue:\"" + System.Text.Encoding.UTF8.GetString(data) + "\"";
            break;
        case EMidiMetaEvent.Channel:
            msg += "chn:" + data[0];
            break;
        case EMidiMetaEvent.EOT:
            msg += "end";
            break;
        case EMidiMetaEvent.Tempo: {
            uint micros = MidiUtil.TempoMicros(data);
            double tempo = MidiUtil.TempoBPM(micros);

            msg += string.Format("tempo:{0:d} ({1:g}bpm)", micros, tempo);
            break;
        }
        case EMidiMetaEvent.SMPTEOffset:
            msg += string.Format("offset: {0:d}h {1:d}m {2:d}s {3:d}fr {4:d}ff", data[0], data[1], data[2], data[3], data[4]);
            break;
        case EMidiMetaEvent.TimeSig:
            msg += string.Format("tsig: {0:d}/{1:d} metr:{2:d} 32nds:{3:d}", data[0], 1 << data[1], data[2], data[3]);
            break;
        case EMidiMetaEvent.KeySig:
            msg += ((sbyte)data[0]).ToString() + (data[1] != 0 ? "m" : "M");
            break;
        case EMidiMetaEvent.SeqSpec:
            msg += "custom: ";
            for (int i = 0; i < data.Length; i++) msg += string.Format(" {0:x}", data[i]);
            break;
        }

        if (sw is null) {
            Debug.Log(msg);
        } else {
            sw.WriteLine(msg);
        }
    }
#endif
}