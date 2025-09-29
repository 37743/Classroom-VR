/// <summary>
/// PiperNative.cs
/// Unity C# bindings for Piper TTS native plugin
/// DLLImport definitions TODO: replace with .SO for Android
/// Specifically modified for Classroom VR from original source: https://github.com/Macoron/piper.unity/tree/master
/// Following the license: GPL v3
/// </summary>

using System;
using System.Runtime.InteropServices;

namespace Piper.Native
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct PiperProcessedSentenceNative
    {
        public long* phonemesIds;
        public UIntPtr length;
    }


    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct PiperProcessedTextNative
    {
        public PiperProcessedSentenceNative* sentences;
        public UIntPtr sentencesCount;
    }

    public static unsafe class PiperNative
    {
        private const string LibraryName = "piper_phonemize";

        [DllImport(LibraryName)]
        public static extern int init_piper(string dataPath);

        [DllImport(LibraryName)]
        public static extern int process_text(string text, string voice);

        [DllImport(LibraryName)]
        public static extern PiperProcessedTextNative get_processed_text();

        [DllImport(LibraryName)]
        public static extern void free_piper();

    }
}

