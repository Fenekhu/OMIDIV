using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

#if UNITY_EDITOR || UNITY_STANDALONE_WIN
public static class WindowHelper {
    private delegate bool EnumWindowsDelegate(IntPtr hWnd, int lParam);
    private static int s_procId = 0;
    private static IntPtr s_hWnd = IntPtr.Zero;
    //private static string s_winTitle = "";

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsDelegate lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", EntryPoint = "GetWindowText")]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpWindowText, int nMaxCount);

    [DllImport("user32.dll", EntryPoint = "GetClientRect")]
    private static extern bool ext_GetClientRect(IntPtr hWnd, ref ext_Rect lpRect);

    [DllImport("user32.dll", EntryPoint = "ClientToScreen")]
    private static extern bool ext_ClientToScreen(IntPtr hWnd, ref ext_LPPoint lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct ext_Rect {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ext_LPPoint {
        public int x;
        public int y;
        public ext_LPPoint(int x, int y) {
            this.x = x; this.y = y;
        }
    }


    [AOT.MonoPInvokeCallback(typeof(EnumWindowsDelegate))]
    private static bool EnumWindowsCallback(IntPtr hWnd, int lParam) {
        GetWindowThreadProcessId(hWnd, out uint windowProcId);

        if (windowProcId != s_procId)
            return true;

        s_hWnd = hWnd;
        return false;
    }

    private static void Refresh() {
        s_procId = Process.GetCurrentProcess().Id;
        IntPtr s_hWnd = IntPtr.Zero;
        //s_winTitle = "";

        EnumWindows(EnumWindowsCallback, IntPtr.Zero);
    }

    public static IntPtr GetWindowHandle() {
        Refresh();
        return s_hWnd;
    }

    public static Rect GetClientRect(bool newhWnd = true) {
        ext_Rect rect = new ext_Rect();
        ext_GetClientRect(newhWnd ? GetWindowHandle() : s_hWnd, ref rect);
        Vector2Int topLeft = ClientToScreen(0, 0, false);

        return new Rect(topLeft.x, topLeft.y, rect.right, rect.bottom);
    }

    public static Vector2Int ClientToScreen(int x, int y, bool newhWnd = true) {
        ext_LPPoint p = new ext_LPPoint(x, y);
        ext_ClientToScreen(newhWnd? GetWindowHandle() : s_hWnd, ref p);
        return new Vector2Int(p.x, p.y);
    }

}
#endif