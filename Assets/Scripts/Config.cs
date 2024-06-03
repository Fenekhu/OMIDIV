using SFB;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

/// <summary>
/// Handles settings that are saved and loaded.
/// </summary>
public static class Config {
    /// <summary>
    /// Called after config has been loaded. Read config values here.
    /// </summary>
    /// <remarks>Subscribed to by <see cref="OmidivComponent.ReadConfig"/></remarks>
    public static event Action AfterLoading;

    /// <summary>
    /// Called before config has been saved. Write config values here.
    /// </summary>
    /// <remarks>Subscribed to by <see cref="OmidivComponent.WriteConfig"/></remarks>
    public static event Action BeforeSaving;

    private static Dictionary<string, byte[]> cfgMap = new Dictionary<string, byte[]>(); // <setting id, data>
    private static string currPath;

    /// <summary>
    /// Gets a value by <paramref name="id"/> and returns it as type <typeparamref name="T"/>, or <c>null</c> if not found.
    /// </summary>
    public static T? Get<T>(string id) where T : unmanaged {
        bool result = cfgMap.TryGetValue(id, out byte[] data);
        if (!result) return null;
        unsafe {
            fixed (byte* bptr = &data[0]) {
                return *(T*)bptr;
            }
        }
    }

    /// <summary>
    /// Sets <paramref name="val"/> if <paramref name="id"/> was found.
    /// </summary>
    /// <returns>Whether a value with the given <paramref name="id"/> was found.</returns>
    public static bool TryGet<T>(string id, ref T val) where T : unmanaged {
        T? res = Get<T>(id);
        val = res.GetValueOrDefault(val);
        return res.HasValue;
    }

    /// <summary>
    /// Passes the value to <paramref name="setter"/> if <paramref name="id"/> is found.
    /// </summary>
    /// <returns>Whether a value with the given <paramref name="id"/> was found.</returns>
    /// <remarks>Useful to set properties, which can't be <c>ref</c>'ed</remarks>
    public static bool TryGet<T>(string id, Action<T> setter) where T : unmanaged {
        T? result = Get<T>(id);
        if (result.HasValue) setter(result.Value);
        return result.HasValue;
    }

    /// <summary>
    /// Reads potentially multiple values of type <typeparamref name="T"/> into <paramref name="dest"/>
    /// </summary>
    /// <returns>The number of values read.</returns>
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

    /// <summary>
    /// Get a saved string by <paramref name="id"/>. <paramref name="str"/> will be <see cref="string.Empty"/> if not found.
    /// </summary>
    /// <returns>Whether a value with the given <paramref name="id"/> was found.</returns>
    public static bool Get(string id, out string str) {
        bool result = cfgMap.TryGetValue(id, out byte[] data);
        if (!result) { str = string.Empty; return false; }
        str = Encoding.UTF8.GetString(data);
        return true;
    }

    /// <summary>
    /// Get a saved string by <paramref name="id"/>. <paramref name="str"/> will not be written to if not found.
    /// </summary>
    /// <returns>Whether a value with the given <paramref name="id"/> was found.</returns>
    public static bool TryGet(string id, ref string str) {
        bool ret = Get(id, out string res);
        if (res.Length != 0) str = res;
        return ret;
    }

    /// <summary>
    /// Passes the value to <paramref name="setter"/> if <paramref name="id"/> is found.
    /// </summary>
    /// <returns>Whether a value with the given <paramref name="id"/> was found.</returns>
    /// <remarks>Useful to set properties, which can't be <c>ref</c>'ed</remarks>
    public static bool TryGet(string id, Action<string> setter) {
        bool ret = Get(id, out string str);
        if (ret) setter(str);
        return ret;
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

    /// <summary>
    /// Opens a file browser and reads a config file.
    /// </summary>
    public static void Open() {
        StandaloneFileBrowser.OpenFilePanelAsync("Open File", "", "omvcfg", false, (string[] res) => {
            if (res.Length > 0) {
                currPath = res[0];
                Read(new FileInfo(currPath));
            }
        });
    }

    /// <summary>
    /// Opens a file browser and saves a config file.
    /// </summary>
    public static void SaveAs() {
        StandaloneFileBrowser.SaveFilePanelAsync("Save Config", "", "", "omvcfg", (string res) => {
            if (res.Length > 0) {
                currPath = res;
                Save();
            }
        });
    }

    /// <summary>
    /// Saves a config file. If one was not opened, calls <see cref="SaveAs"/>.
    /// </summary>
    public static void Save() {
        if (currPath.Length == 0) { SaveAs(); return; }
        Write(new FileInfo(currPath));
    }

    /// <summary>
    /// Reads config from a given file.
    /// </summary>
    /// <param name="path">The path of the file to read.</param>
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

    /// <summary>
    /// Writes config to a file.
    /// </summary>
    /// <param name="path">The path of the file to write to.</param>
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
