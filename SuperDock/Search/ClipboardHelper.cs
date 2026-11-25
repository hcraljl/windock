using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace SuperDock.Search;

/// <summary>
/// 直接调用 Win32 剪贴板 API 写入文本。
/// WPF 的 Clipboard.SetText 内部走 OLE：数据写进剪贴板后还要 OleFlushClipboard，
/// 这一步需要重新 OpenClipboard，只要此刻有别的程序（输入法、剪贴板工具、Office 等）
/// 占着剪贴板就会抛 CLIPBRD_E_CANT_OPEN(0x800401D0)，
/// 于是出现「弹错误框但内容其实已经复制成功」的怪现象。
/// 这里改为 OpenClipboard + EmptyClipboard + SetClipboardData，自己控制重试，
/// 不再有“写入成功却报错”的窗口期。
/// </summary>
public static class ClipboardHelper
{
    private const uint CfUnicodeText = 13;
    private const uint GmemMoveable = 0x0002;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalUnlock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalFree(IntPtr hMem);

    /// <summary>写入纯文本；剪贴板被占用时自动重试，全部失败才返回 false。</summary>
    public static bool TrySetText(string text, int attempts = 8)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        for (var attempt = 0; attempt < attempts; attempt++)
        {
            if (attempt > 0)
            {
                Thread.Sleep(25 + (attempt * 15));
            }

            if (TrySetTextOnce(text))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TrySetTextOnce(string text)
    {
        if (!OpenClipboard(IntPtr.Zero))
        {
            return false;
        }

        var memory = IntPtr.Zero;
        try
        {
            if (!EmptyClipboard())
            {
                return false;
            }

            var bytes = ((text.Length + 1) * sizeof(char));
            memory = GlobalAlloc(GmemMoveable, (UIntPtr)(uint)bytes);
            if (memory == IntPtr.Zero)
            {
                return false;
            }

            var target = GlobalLock(memory);
            if (target == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                var characters = text.ToCharArray();
                Marshal.Copy(characters, 0, target, characters.Length);
                Marshal.WriteInt16(target, characters.Length * sizeof(char), 0);
            }
            finally
            {
                GlobalUnlock(memory);
            }

            if (SetClipboardData(CfUnicodeText, memory) == IntPtr.Zero)
            {
                return false;
            }

            // 成功以后内存所有权归系统，不能再释放
            memory = IntPtr.Zero;
            return true;
        }
        finally
        {
            if (memory != IntPtr.Zero)
            {
                GlobalFree(memory);
            }

            CloseClipboard();
        }
    }
}
