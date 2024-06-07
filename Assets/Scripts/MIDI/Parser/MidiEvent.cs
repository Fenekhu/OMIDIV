using System.IO;
using Debug = UnityEngine.Debug;

/// <summary>
/// A class representing realtime events on a specific channel.
/// </summary>
public class MidiEvent : MTrkEvent {
    
    public struct Message {
        public byte v;
        public int status {
            get { return (v & 0xf0) >> 4; }
        }
        public int channel {
            get { return v & 0x0f; }
        }
    }

    public override byte EventType { get { return msg.v; } }
    public Message msg;
    public EMidiEventStatus Status { get { return (EMidiEventStatus)msg.status; } }
    public int Channel { get { return msg.channel; } }

    public MidiEvent() { msg.v = 0; }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
    public override void DebugPrint(StreamWriter sw) {
        string msg = string.Format("        {0,-6:d}  midi  ", delta);

        if (Status == EMidiEventStatus.System) {
            switch ((EMidiEventSysCh)Channel) {
            case EMidiEventSysCh.Tune:
                msg += "tune request";
                break;
            case EMidiEventSysCh.Timing:
                msg += "timing clock";
                break;
            case EMidiEventSysCh.Start:
                msg += "seq start";
                break;
            case EMidiEventSysCh.Resume:
                msg += "seq resume";
                break;
            case EMidiEventSysCh.Stop:
                msg += "seq stop";
                break;
            case EMidiEventSysCh.Sensing:
                msg += "active sensing";
                break;
            default:
                msg += "unknown dataless system event";
                break;
            }
        } else {
            msg += "unknown dataless midi event";
        }

        if (sw is null) {
            Debug.Log(msg);
        } else {
            sw.WriteLine(msg);
        }
    }
#endif
}

public class MidiEventOneByte : MidiEvent {
    public byte data1 = 0;

#if DEVELOPMENT_BUILD || UNITY_EDITOR
    public override void DebugPrint(StreamWriter sw) {
        string msg = string.Format("        {0,-6:d}  midi  ", delta);

        switch(Status) {
        case EMidiEventStatus.PrgmChange:
            msg += string.Format("ch:{0:d}  prgm:{1:d}", Channel, data1);
            break;
        case EMidiEventStatus.ChannelPressure:
            msg += string.Format("ch:{0:d}  ch pressure:{1:d}", Channel, data1);
            break;
        case EMidiEventStatus.System:
            if (Channel == (int)EMidiEventSysCh.SongSelect) {
                msg += string.Format("song sel:{0:d}", data1);
            } else {
                msg += string.Format("unknown 1byte system event  data:{0:x}", data1);
            }
            break;
        default:
            msg += string.Format("unknown 1byte midi event  data:{0:x}", data1);
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

public class MidiEventTwoBytes : MidiEvent {
    public byte data1 = 0;
    public byte data2 = 0;

#if DEVELOPMENT_BUILD || UNITY_EDITOR
    public override void DebugPrint(StreamWriter sw) {
        string msg = string.Format("        {0,-6:d}  midi  ", delta);

        switch(Status) {
        case EMidiEventStatus.NoteOff:
            msg += string.Format("ch:{0:d}  note off  key:{1:d}  vel:{2:d}", Channel, data1, data2);
            break;
        case EMidiEventStatus.NoteOn:
            msg += string.Format("ch:{0:d}  note on  key:{1:d}  vel:{2:d}", Channel, data1, data2);
            break;
        case EMidiEventStatus.PolyKeyPressure:
            msg += string.Format("ch:{0:d}  pressure  key:{1:d}  vel:{2:d}", Channel, data1, data2);
            break;
        case EMidiEventStatus.PitchWheel:
            msg += string.Format("ch:{0:d}  pitchwheel:{1:d}", Channel, (data2 << 7) + data1);
            break;
        case EMidiEventStatus.System:
            if (Channel == (int)EMidiEventSysCh.SongPos) {
                msg += string.Format("song pos:{0:d}", (data2 << 7) + data1);
            } else {
                msg += string.Format("unknown 2byte system event  data:{0:x} {1:x}", data1, data2);
            }
            break;
        default:
            msg += string.Format("unknown 2byte midi event  data:{0:x} {1:x}", data1, data2);
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

public class MidiNoteEvent : MidiEventTwoBytes {
    public byte Key { get { return data1; } set { data1 = value; } }
    public byte Vel { get { return data2; } set { data2 = value; } }
}

public class MidiControlEvent : MidiEventTwoBytes {
    public byte Control { get { return data1; } set { data1 = value; } }
    public byte Value { get { return data2; } set { data2 = value; } }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
    public override void DebugPrint(StreamWriter sw) {
        string msg = string.Format("        {0,-6:d}  midi  ", delta);
        msg += string.Format("ch:{0:d}  ctrl change  ctrl:{0:d}  val:{1:d}", Channel, data1, data2);
        if (sw is null) {
            Debug.Log(msg);
        } else {
            sw.WriteLine(msg);
        }
    }
#endif
}

public class MidiProgramChangeEvent : MidiEventOneByte {
    public byte Patch { get { return data1; } set { data1 = value; } }
}

public class MidiChannelPressureEvent : MidiEventOneByte {
    public byte Vel { get { return data1; } set { data1 = value; } }
}

public class MidiPitchwheelEvent : MidiEventTwoBytes {
    public int Pitch { get { return (data2 << 7) + data1; } }
}

public class MidiSongPositionEvent : MidiEventTwoBytes {
    public int Pos { get { return (data2 << 7) + data1; } }
}

public class MidiSongSelectEvent : MidiEventOneByte {
    public byte Song { get { return data1; } set { data1 = value; } }
}
