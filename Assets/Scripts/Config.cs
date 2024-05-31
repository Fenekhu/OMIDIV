using SFB;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

public static class Config {
    public static event Action AfterLoading;
    public static event Action BeforeSaving;

    private static Dictionary<string, byte[]> cfgMap = new Dictionary<string, byte[]>(); // <setting id, data>
    private static string currPath;

    public static T? Get<T>(string id) where T : unmanaged {
        bool result = cfgMap.TryGetValue(id, out byte[] data);
        if (!result) return null;
        unsafe {
            fixed (byte* bptr = &data[0]) {
                return *(T*)bptr;
            }
        }
    }
    public static void TryGet<T>(string id, ref T val) where T : unmanaged {
        val = Get<T>(id) ?? val;
    }
    public static int Get<T>(string id, List<T> dest, int maxCount = -1) where T : unmanaged {
        bool result = cfgMap.TryGetValue(id, out byte[] data);
        if (!result || maxCount == 0 || dest is null) return 0;
        maxCount = maxCount < 0 ? int.MaxValue / Marshal.SizeOf<T>() : maxCount;
        int count = Math.Min(maxCount, data.Length / Marshal.SizeOf<T>());
        unsafe {
            for (int i = 0; i < count; i++) {
                fixed (byte* bptr = &data[Marshal.SizeOf<T>() * i]) {
                    dest.Add(*(T*)bptr);
                }
            }
        }
        return count;
    }
    public static bool Get(string id, out string str) {
        bool result = cfgMap.TryGetValue(id, out byte[] data);
        if (!result) { str = string.Empty; return false; }
        str = System.Text.Encoding.UTF8.GetString(data);
        return true;
    }
    public static void TryGet(string id, ref string str) {
        Get(id, out string res);
        if (res.Length != 0) str = res;
    }

    public static void Set<T>(string id, T val) where T : unmanaged {
        byte[] data = new byte[Marshal.SizeOf<T>()];
        unsafe {
            fixed (byte* ptr = &data[0]) {
                *(T*)ptr = val;
            }
        }
        cfgMap[id] = data;
    }
    public static void Set<T>(string id, T[] val, int count = -1) where T : unmanaged {
        count = count < 0 ? val.Length : count;
        byte[] data = new byte[Marshal.SizeOf<T>() * count];
        unsafe {
            for (int i = 0; i < count; i++) {
                fixed (byte* bptr = &data[Marshal.SizeOf<T>() * i]) fixed(T* tptr = &val[i]) {
                    *(T*)bptr = *tptr;
                }
            }
        }
        cfgMap[id] = data;
    }
    public static void Set(string id, string val) {
        cfgMap[id] = Encoding.UTF8.GetBytes(val);
    }

    public static void Clear() {
        cfgMap.Clear();
    }

    public static void Open() {
        StandaloneFileBrowser.OpenFilePanelAsync("Open File", "", "omvcfg", false, (string[] res) => {
            if (res.Length > 0) {
                currPath = res[0];
                Read(new FileInfo(currPath));
            }
        });
    }

    public static void SaveAs() {
        StandaloneFileBrowser.SaveFilePanelAsync("Save Config", "", "", "omvcfg", (string res) => {
            if (res.Length > 0) {
                currPath = res;
                Save();
            }
        });
    }

    public static void Save() {
        if (currPath.Length == 0) { SaveAs(); return; }
        Write(new FileInfo(currPath));
    }

    public static void Read(FileInfo path) {
        using (BinaryReader br = new BinaryReader(File.OpenRead(path.FullName), Encoding.UTF8)) {
            byte hLength = br.ReadByte();
            long pos = br.BaseStream.Position;
            byte version = br.ReadByte();
            br.BaseStream.Seek(pos + hLength, SeekOrigin.Begin);

            if (version == 1 || version == 2) {
                while(br.BaseStream.Position < br.BaseStream.Length) {
                    string id = "";
                    if (version == 1) {
                        List<byte> bytes = new List<byte>();
                        byte b = br.ReadByte();
                        while (b != 0) { bytes.Add(b); b = br.ReadByte(); }
                        id = Encoding.UTF8.GetString(bytes.ToArray());
                    } else if (version == 2) {
                        id = br.ReadString();
                    }
                    uint dataSize = br.ReadUInt32();
                    byte[] data = new byte[dataSize];
                    br.Read(data);
                    cfgMap[id] = data;
                }
            }
        }

        AfterLoading?.Invoke();
    }

    public static void Write(FileInfo path) {
        BeforeSaving?.Invoke();

        using (BinaryWriter bw = new BinaryWriter(File.OpenWrite(path.FullName), Encoding.UTF8)) {
            bw.Write((byte)1); // header size
            bw.Write((byte)2); // config version

            foreach (var kvp in cfgMap) {
                bw.Write(kvp.Key);
                uint dataSize = (uint)kvp.Value.Length;
                bw.Write(dataSize);
                bw.Write(kvp.Value);
            }
        }
    }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
    public static void DebugPrint() {
        foreach (var kvp in cfgMap) {
            Debug.LogFormat("{0:s}:  {1:s}", kvp.Key, string.Join(" ", kvp.Value));
        }
    }
#endif

}
