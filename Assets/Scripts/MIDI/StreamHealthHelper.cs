using System;
using System.IO;

public static class StreamHealthHelper {

    public static bool EOF(this FileStream stream) {
        return stream.Position >= stream.Length;
    }

    public static bool Good(this FileStream stream) {
        return stream.Position < stream.Length;
    }
    public static bool EOF(this BinaryReader reader) {
        return reader.BaseStream.Position >= reader.BaseStream.Length;
    }

    public static bool Good(this BinaryReader reader) {
        return reader.BaseStream.Position < reader.BaseStream.Length;
    }
}
