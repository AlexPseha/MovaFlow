using System.Runtime.InteropServices;

namespace MovaFlow.Security;

public static class MemorySanitizer
{
    [DllImport("kernel32.dll", EntryPoint = "RtlZeroMemory")]
    private static extern void RtlZeroMemory(IntPtr dest, IntPtr size);

    public static void Sanitize(char[]? buffer)
    {
        if (buffer == null || buffer.Length == 0) return;
        Array.Clear(buffer, 0, buffer.Length);
    }

    public static unsafe void SanitizePinned(char* ptr, int length)
    {
        if (ptr == null || length <= 0) return;
        RtlZeroMemory((IntPtr)ptr, (IntPtr)(length * sizeof(char)));
    }
}
