using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

public class NativeProbe : MonoBehaviour
{
#if UNITY_EDITOR_WIN
    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern IntPtr LoadLibrary(string lpFileName);

    void Start()
    {
        // Adjust this to where YOUR file actually is; I recommend x86_64:
        var full = Path.Combine(Application.dataPath, "Plugins", "x86_64", "piper_phonemize.dll");

        if (!File.Exists(full))
        {
            Debug.LogError("Not found: " + full);
            return;
        }

        // (Optional) augment PATH so sibling deps are found
        var dir = Path.GetDirectoryName(full);
        var oldPath = Environment.GetEnvironmentVariable("PATH");
        if (oldPath == null || !oldPath.Contains(dir))
            Environment.SetEnvironmentVariable("PATH", dir + ";" + oldPath);

        var h = LoadLibrary(full);
        if (h == IntPtr.Zero)
        {
            int err = Marshal.GetLastWin32Error();
            Debug.LogError($"LoadLibrary({full}) failed. Win32={err} ({new Win32Exception(err).Message})");
        }
        else
        {
            Debug.Log("LoadLibrary OK: " + full);
        }
    }
#endif
}