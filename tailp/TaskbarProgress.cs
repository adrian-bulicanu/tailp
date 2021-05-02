// This is an open source non-commercial project. Dear PVS-Studio, please check it.
// PVS-Studio Static Code Analyzer for C, C++ and C#: http://www.viva64.com

using System;
using System.Runtime.InteropServices;

// ReSharper disable SuspiciousTypeConversion.Global

namespace tailp
{
    // thanks to https://stackoverflow.com/a/24187171
    [Flags]
    public enum TaskbarStates
    {
#pragma warning disable S2346 // Flags enumerations zero-value members should be named "None"
#pragma warning disable CA1008 // Enums should have zero value
        NoProgress = 0,
#pragma warning restore CA1008 // Enums should have zero value
#pragma warning restore S2346 // Flags enumerations zero-value members should be named "None"
        Indeterminate = 0x1,
        Normal = 0x2,
        Error = 0x4,
        // ReSharper disable once UnusedMember.Global
        Paused = 0x8
    }

    internal static class NativeMethods
    {
        [ComImport()]
        [Guid("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface ITaskbarList3
        {
            // ITaskbarList
            [PreserveSig]
            void HrInit();

            [PreserveSig]
            void AddTab(IntPtr hwnd);

            [PreserveSig]
            void DeleteTab(IntPtr hwnd);

            [PreserveSig]
            void ActivateTab(IntPtr hwnd);

            [PreserveSig]
            void SetActiveAlt(IntPtr hwnd);

            // ITaskbarList2
            [PreserveSig]
            void MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool fFullscreen);

            // ITaskbarList3
            [PreserveSig]
            void SetProgressValue(IntPtr hwnd, UInt64 ullCompleted, UInt64 ullTotal);

            [PreserveSig]
            void SetProgressState(IntPtr hwnd, TaskbarStates state);
        }

        [ComImport()]
        [Guid("56fdf344-fd6d-11d0-958a-006097c9a090")]
        [ClassInterface(ClassInterfaceType.None)]
        internal class TaskbarInstance
        {
        }

        [DllImport("kernel32.dll")]
#pragma warning disable CA5392 // Use DefaultDllImportSearchPaths attribute for P/Invokes
        internal static extern IntPtr GetConsoleWindow();
#pragma warning restore CA5392 // Use DefaultDllImportSearchPaths attribute for P/Invokes
    }

    public static class TaskbarProgress
    {
        private static readonly NativeMethods.ITaskbarList3 TaskbarInstance =
            (NativeMethods.ITaskbarList3)new NativeMethods.TaskbarInstance();

        private static readonly bool TaskbarSupported = Environment.OSVersion.Version >= new Version(6, 1);
        private static IntPtr _consoleHandle = IntPtr.Zero;

        private static IntPtr GetConsoleHandle()
        {
            if (_consoleHandle == IntPtr.Zero)
            {
                _consoleHandle = NativeMethods.GetConsoleWindow();
            }

            return _consoleHandle;
        }

        public static void SetState(TaskbarStates taskbarState)
        {
            if (TaskbarSupported) TaskbarInstance.SetProgressState(GetConsoleHandle(), taskbarState);
        }

        public static void SetValue(double progressValue, double progressMax)
        {
            if (TaskbarSupported) TaskbarInstance.SetProgressValue(GetConsoleHandle(), (ulong)progressValue, (ulong)progressMax);
        }
    }
}