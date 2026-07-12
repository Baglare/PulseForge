using System;
using System.Runtime.InteropServices;

namespace PulseForge.Runtime.Unity.Audio
{
    internal static class WindowsAudioFilePicker
    {
        private const int MaximumPathLength = 32768;

        public static bool TryPickAudioFile(out string selectedPath, out string errorMessage)
        {
            selectedPath = string.Empty;
            errorMessage = string.Empty;

#if UNITY_EDITOR
            selectedPath = UnityEditor.EditorUtility.OpenFilePanelWithFilters(
                "Choose a song for PulseForge",
                string.Empty,
                new[]
                {
                    "Supported audio",
                    "wav,mp3,m4a,aac,flac,ogg,opus,wma,aif,aiff",
                    "All files",
                    "*"
                });
            return !string.IsNullOrWhiteSpace(selectedPath);
#elif UNITY_STANDALONE_WIN
            IntPtr fileBuffer = IntPtr.Zero;
            IntPtr filterBuffer = IntPtr.Zero;
            IntPtr titleBuffer = IntPtr.Zero;

            try
            {
                int fileBufferBytes = MaximumPathLength * sizeof(char);
                fileBuffer = Marshal.AllocHGlobal(fileBufferBytes);
                Marshal.Copy(new byte[fileBufferBytes], 0, fileBuffer, fileBufferBytes);
                filterBuffer = Marshal.StringToHGlobalUni(BuildFilter());
                titleBuffer = Marshal.StringToHGlobalUni("Choose a song for PulseForge");

                OpenFileName dialog = new OpenFileName
                {
                    structSize = Marshal.SizeOf(typeof(OpenFileName)),
                    filter = filterBuffer,
                    file = fileBuffer,
                    maxFile = MaximumPathLength,
                    title = titleBuffer,
                    flags = OpenFileNameFlags.Explorer
                        | OpenFileNameFlags.FileMustExist
                        | OpenFileNameFlags.PathMustExist
                        | OpenFileNameFlags.NoChangeDirectory
                };

                if (GetOpenFileName(ref dialog))
                {
                    selectedPath = Marshal.PtrToStringUni(fileBuffer) ?? string.Empty;
                    return !string.IsNullOrWhiteSpace(selectedPath);
                }

                int dialogError = CommDlgExtendedError();
                if (dialogError != 0)
                {
                    errorMessage = "Windows file picker failed (code " + dialogError + ").";
                }

                return false;
            }
            finally
            {
                FreeHGlobal(fileBuffer);
                FreeHGlobal(filterBuffer);
                FreeHGlobal(titleBuffer);
            }
#else
            errorMessage = "Custom song selection is currently available in Windows builds only.";
            return false;
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private static string BuildFilter()
        {
            return "Supported audio\0*.wav;*.mp3;*.m4a;*.aac;*.flac;*.ogg;*.opus;*.wma;*.aif;*.aiff\0"
                + "All files\0*.*\0\0";
        }

        private static void FreeHGlobal(IntPtr pointer)
        {
            if (pointer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(pointer);
            }
        }

        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetOpenFileNameW", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetOpenFileName(ref OpenFileName openFileName);

        [DllImport("comdlg32.dll")]
        private static extern int CommDlgExtendedError();

        [StructLayout(LayoutKind.Sequential)]
        private struct OpenFileName
        {
            public int structSize;
            public IntPtr dialogOwner;
            public IntPtr instance;

            public IntPtr filter;
            public IntPtr customFilter;

            public int maxCustomFilter;
            public int filterIndex;
            public IntPtr file;
            public int maxFile;
            public IntPtr fileTitle;
            public int maxFileTitle;
            public IntPtr initialDirectory;
            public IntPtr title;

            public OpenFileNameFlags flags;
            public short fileOffset;
            public short fileExtension;

            public IntPtr defaultExtension;
            public IntPtr customData;
            public IntPtr hook;
            public IntPtr templateName;
            public IntPtr reserved;
            public int reservedValue;
            public int flagsExtended;
        }

        [Flags]
        private enum OpenFileNameFlags
        {
            NoChangeDirectory = 0x00000008,
            PathMustExist = 0x00000800,
            FileMustExist = 0x00001000,
            Explorer = 0x00080000
        }
#endif
    }
}
