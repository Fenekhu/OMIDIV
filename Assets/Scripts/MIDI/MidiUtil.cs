using System;
using System.Buffers.Binary;
using System.IO;
using Debug = UnityEngine.Debug;

#region Enums
public enum EMidiChunkType {
    Header = 1684558925, // 'MThd' in big endian ('dhTM')
    Track = 1802654797 // 'MTrk' in big endian ('krTM')
}

public enum EMidiFormat {
    Zero,
    One,
    Two
}

public enum EMidiDivisionFormat {
    TPQN, // Ticks per quarter note
    SMPTE // idk what it stands for because i dont support it
}

public enum EMidiSMPTEFPS {
    TwentyFour = -24,
    TwentyFive = -25,
    ThirtyDrop = -29, // 29.97
    Thirty = -30
}

public enum EMidiEventStatus {
    NoteOff = 8,
    NoteOn = 9,
    PolyKeyPressure = 0xA,
    CtrlChange = 0xB,
    ChannelMode = 0xB,
    PrgmChange = 0xC,
    ChannelPressure = 0xD,
    PitchWheel = 0xE,
    System = 0xF
}

public enum EMidiEventSysCh {
    Exclusive = 0,
    SongPos = 2,
    SongSelect = 3,
    Tune = 6,
    ExclusiveEnd = 7,
    Timing = 8,
    Start = 0xA,
    Resume = 0xB,
    Stop = 0xC,
    Sensing = 0xE,
    Reset = 0xF
}

public enum EMidiEventCtrl {
    BankSelect_1 = 0,
    ModWheel_1,
    Breath_1,
    Foot_1,
    GlideTime_1 = 5,
    DataEntry_1,
    ChannelVolume_1,
    Balance_1,
    Pan_1 = 10,
    Expression_1,
    Effect1_1,
    Effect2_1,
    General1_1 = 16,
    General2_1,
    General3_1,
    General4_1,
    BankSelect_2 = 32,
    ModWheel_2,
    Breath_2,
    Foot_2,
    GlideTime_2 = 37,
    DataEntry_2,
    ChannelVolume_2,
    Balance_2,
    Pan_2 = 42,
    Expression_2,
    Effect1_2,
    Effect2_2,
    General1_2 = 48,
    General2_2,
    General3_2,
    General4_2,
    DamperSwitch = 64,
    GlideSwitch,
    SustainSwitch,
    SoftSwitch,
    LegatoFootSwitch,
    Hold2,
    Sound1,
    Sound2,
    Sound3,
    Sound4,
    Sound5,
    Sound6,
    Sound7,
    Sound8,
    Sound9,
    Sound10,
    General5,
    General6,
    General7,
    General8,
    Glide,
    Effect1_Depth = 91,
    Effect2_Depth,
    Effect3_Depth,
    Effect4_Depth,
    Effect5_Depth,

    DataEntryInc,
    DataEntryDec,
    NRPN_LSB,
    NRPN_MSB,
    RPN_LSB,
    RPN_MSB,

    AllSoundOff = 120,
    ResetAll,
    LocalSwitch,
    AllNotesOff,
    OmniOff,
    OmniOn,
    PolySwitch,
    PolyOn
}

public enum EMidiMetaEvent {
    SequenceNumber,
    Text,
    Copyright,
    TrackName,
    InstrName,
    Lyric,
    Marker,
    Cue,
    Channel = 0x20,
    EOT = 0x2F,
    Tempo = 0x51,
    SMPTEOffset = 0x54,
    TimeSig = 0x58,
    KeySig = 0x59,
    SeqSpec = 0x7F,
}
#endregion

public static class MidiUtil {

    /// <summary>
    /// Big-Endian Variable-Length Value to uint
    /// </summary>
    /// <param name="bytes">An array of bytes that contains a variable length value./param>
    /// <param name="offset">The offset into the array that the value begins at. Will be updated based on the number of bytes read.</param>
    public static uint BEVLVToUint(byte[] bytes, ref int offset) {
        uint ret = 0;
        while ((bytes[offset] & 0b1000_0000) != 0) {
            ret |= bytes[offset] & (uint)0b0111_1111;
            ret <<= 7;
            offset++;
        }
        ret |= bytes[offset++];
        return ret;
    }

    /// <summary>
    /// Big-Endian Variable-Length Value to uint
    /// </summary>
    /// <param name="bytes">An array of bytes that contains a variable length value./param>
    /// <param name="offset">The offset into the array that the value begins at.</param>
    public static uint BEVLVToUint(byte[] bytes, int offset = 0) {
        return BEVLVToUint(bytes, ref offset);
    }

    /// <summary>
    /// uint to Big-Endian Variable-Length Value
    /// </summary>
    public static byte[] UintToBEVLV(uint num) {
        if (num >= 0x0FFF_FFFF) throw new ArgumentOutOfRangeException(nameof(num), num, "UintToBEVLV must be less than 0x0FFFFFFF (268,435,455)");

        uint val = 0; // VLV in little endian
        int count = 1;

        val |= num & 0b0111_1111;
        while ((num >>= 7) > 0) {
            val <<= 8;
            val |= 0b1000_0000 | (num & 0b0111_1111);
            count++;
        }

        byte[] bytes = new byte[count]; // big endian VLV

        // read LE VLV into BE VLV
        for (int i = 0; i < count; i++) {
            bytes[i] = (byte)val;
            val >>= 8;
        }

        return bytes;
    }

    /// <summary>
    /// Used when interpreting tempo events.
    /// </summary>
    public static uint TempoMicros(byte[] data) {
        if (data.Length < 3) throw new ArgumentException("data must be at least 3 bytes long", nameof(data));

        byte[] ret = {0, 0, 0, 0 };
        ret[2] = data[0];
        ret[1] = data[1];
        ret[0] = data[2];
        return BitConverter.ToUInt32(ret, 0);
    }

    public static uint TempoMicros(double bpm) {
        return (uint)(6e7d / bpm);
    }

    public static double TempoBPM(uint micros) {
        return 6e7d / micros;
    }

    public static double TempoBPM(byte[] data) {
        return TempoBPM(TempoMicros(data));
    }

    #region File IO Helpers
    public static void HandleReadError(Exception ex) {
        Debug.LogException(ex);
#if DEVELOPEMENT_BUILD
        throw ex;
#endif
    }
    public static bool ReadU8(FileStream file, out byte val) {
        BinaryReader br = new BinaryReader(file);
        try {
            val = br.ReadByte();
            return true;
        } catch (Exception ex) when (ex is EndOfStreamException || ex is IOException) {
            HandleReadError(ex);
            val = 0;
            return false;
        }
    }
    public static bool ReadI16(FileStream file, out short val) {
        BinaryReader br = new BinaryReader(file);
        try {
            val = BinaryPrimitives.ReverseEndianness(br.ReadInt16());
            return true;
        } catch (Exception ex) when (ex is EndOfStreamException || ex is IOException) {
            HandleReadError(ex);
            val = 0;
            return false;
        }
    }
    public static bool ReadU16(FileStream file, out ushort val) {
        BinaryReader br = new BinaryReader(file);
        try {
            val = BinaryPrimitives.ReverseEndianness(br.ReadUInt16());
            return true;
        } catch (Exception ex) when (ex is EndOfStreamException || ex is IOException) {
            HandleReadError(ex);
            val = 0;
            return false;
        }
    }
    public static bool ReadI32(FileStream file, out int val) {
        BinaryReader br = new BinaryReader(file);
        try {
            val = BinaryPrimitives.ReverseEndianness(br.ReadInt32());
            return true;
        } catch (Exception ex) when (ex is EndOfStreamException || ex is IOException) {
            HandleReadError(ex);
            val = 0;
            return false;
        }
    }
    public static bool ReadU32(FileStream file, out uint val) {
        BinaryReader br = new BinaryReader(file);
        try {
            val = BinaryPrimitives.ReverseEndianness(br.ReadUInt32());
            return true;
        } catch (Exception ex) when (ex is EndOfStreamException || ex is IOException) {
            HandleReadError(ex);
            val = 0;
            return false;
        }
    }
    public static bool ReadBEVLV(FileStream file, out uint val) {
        try {
            byte[] bytes = {0, 0, 0, 0};
            int i = 0;
            while (file.Read(bytes, i, 1) == 1 && 
                (bytes[i] & 0x80) != 0) 
                i++;
            val = BEVLVToUint(bytes);
            return true;
        } catch (IOException ex) {
            HandleReadError(ex);
            val = 0;
            return false;
        }
    }
    #endregion


}
