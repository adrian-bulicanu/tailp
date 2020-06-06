﻿// This is an open source non-commercial project. Dear PVS-Studio, please check it.
// PVS-Studio Static Code Analyzer for C, C++ and C#: http://www.viva64.com
using System;
using System.Runtime.InteropServices;
// ReSharper disable SuspiciousTypeConversion.Global

namespace TailP
{
    // thanks to https://stackoverflow.com/a/24187171
    [Flags]
    public enum TaskbarStates
    {
#pragma warning disable S2346 // Flags enumerations zero-value members should be named "None"
        NoProgress = 0,
#pragma warning restore S2346 // Flags enumerations zero-value members should be named "None"
        Indeterminate = 0x1,
        Normal = 0x2,
        Error = 0x4,
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
        internal static extern IntPtr GetConsoleWindow();
    }

    public static class TaskbarProgress
    {
        private static readonly NativeMethods.ITaskbarList3 _taskbarInstance =
            (NativeMethods.ITaskbarList3)new NativeMethods.TaskbarInstance();

        private static readonly bool _taskbarSupported = Environment.OSVersion.Version >= new Version(6, 1);
        private static IntPtr _consoleHandle = IntPtr.Zero;

        private static IntPtr GetConsoleHandle()
        {
            if (_consoleHandle == IntPtr.Zero)
            {
                _consoleHandle = NativeMethods.GetConsoleWindow();
            }

            return _consoleHandle;
        }

        public static void SetState(IntPtr windowHandle, TaskbarStates taskbarState)
        {
            if (_taskbarSupported) _taskbarInstance.SetProgressState(windowHandle, taskbarState);
        }

        public static void SetValue(IntPtr windowHandle, double progressValue, double progressMax)
        {
            if (_taskbarSupported) _taskbarInstance.SetProgressValue(windowHandle, (ulong)progressValue, (ulong)progressMax);
        }

        public static void SetState(TaskbarStates taskbarState)
        {
            if (_taskbarSupported) _taskbarInstance.SetProgressState(GetConsoleHandle(), taskbarState);
        }

        public static void SetValue(double progressValue, double progressMax)
        {
            if (_taskbarSupported) _taskbarInstance.SetProgressValue(GetConsoleHandle(), (ulong)progressValue, (ulong)progressMax);
        }
    }
}