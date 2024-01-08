using System.Collections;
using System.Collections.Generic;
using System.IO;
using Debug = UnityEngine.Debug;

public class RawMidi {
    public HeaderChunk header;
    public List<TrackChunk> tracks = new List<TrackChunk>();

    public RawMidi() {}

    public RawMidi(FileInfo path) {
        Open(path);
    }

    public void Open(FileInfo path) {
        header = new HeaderChunk();
        tracks.Clear();
        FileStream file = new FileStream(path.FullName, FileMode.Open, FileAccess.Read);
        if (!file.Good() || !file.CanRead) {
            Debug.LogError("Could not open file: " + path.FullName);
            return;
        }

        bool readHeader = false;
        bool fail = false;
        uint chunk = 0;
        using (BinaryReader br = new BinaryReader(file)) {
            while(file.Good() && !fail) {
                chunk = br.ReadUInt32();
                if (file.EOF()) break;
                switch((EMidiChunkType)chunk) {
                case EMidiChunkType.Header:
                    if (readHeader) {
                        Debug.LogWarning("Multiple header chunks encountered");
                        break;
                    }
                    header = new HeaderChunk(file);
                    readHeader = true;
                    break;
                case EMidiChunkType.Track:
                    tracks.Add(new TrackChunk(file));
                    break;
                default:
                    Debug.LogError("Unknown chunk type: " + chunk);
                    fail = true;
                    break;
                }
            }
        }
    }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
    public void DebugPrint(StreamWriter sw) {
        string msg = "--------------------- MIDI debug dump ---------------------";
        if (sw is null) {
            Debug.Log(msg);
        } else {
            sw.WriteLine(msg);
        }
        header.DebugPrint(sw);
        foreach (TrackChunk track in tracks) track.DebugPrint(sw);
        msg = "-----------------------------------------------------------";
        if (sw is null) {
            Debug.Log(msg);
        } else {
            sw.WriteLine(msg);
        }
    }
#endif
}
