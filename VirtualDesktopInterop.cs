using System;
using System.Runtime.InteropServices;

namespace TaskbarWorkspaceWidget
{
    internal static class VirtualDesktopInterop
    {
        // Switch to desktop by index
        [DllImport("VirtualDesktopAccessor.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void GoToDesktopNumber(int number);

        // Get current desktop number (0-based index)
        [DllImport("VirtualDesktopAccessor.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetCurrentDesktopNumber();

        // Get total number of desktops
        [DllImport("VirtualDesktopAccessor.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetDesktopCount();

        [DllImport("VirtualDesktopAccessor.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void PinWindow(IntPtr hwnd);
    }
}
