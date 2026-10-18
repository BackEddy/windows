using System;
using System.Runtime.InteropServices;
using Backeddy.Core;

namespace Backeddy.App;

/// <summary>
/// Ícone na bandeja (perto do relógio), direto pela API do Windows, sem bibliotecas extras.
/// Clique: abre a janela. Botão direito: menu. Volta sozinho se o Explorer reiniciar.
/// </summary>
sealed class Tray : IDisposable
{
    const uint WM_APP = 0x8000, WM_TRAY = WM_APP + 1, WM_NULL = 0, WM_LBUTTONUP = 0x0202, WM_RBUTTONUP = 0x0205;
    const uint NIM_ADD = 0, NIM_MODIFY = 1, NIM_DELETE = 2, NIF_MESSAGE = 1, NIF_ICON = 2, NIF_TIP = 4, NIF_INFO = 0x10;
    const uint NIIF_WARNING = 2, WM_BALLOONUSERCLICK = 0x0405;
    const uint MF_STRING = 0, MF_SEPARATOR = 0x800, TPM_RIGHTBUTTON = 0x2, TPM_RETURNCMD = 0x100;
    const uint IMAGE_ICON = 1, LR_LOADFROMFILE = 0x10;
    const int SM_CXSMICON = 49, SM_CYSMICON = 50;
    const int CmdOpen = 1, CmdQuit = 2;

    readonly WndProc proc;            // guardado para o coletor de lixo não apagar o callback
    readonly IntPtr hwnd, hIcon;
    readonly string tip;
    readonly Action onOpen, onQuit;
    readonly uint taskbarCreated;
    bool disposed;

    public Tray(string iconPath, string tip, Action onOpen, Action onQuit)
    {
        this.tip = tip;
        this.onOpen = onOpen;
        this.onQuit = onQuit;
        proc = WindowProc;
        var className = "BackeddyTrayWindow";
        var wc = new WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(proc),
            hInstance = GetModuleHandle(null),
            lpszClassName = className,
        };
        RegisterClassEx(ref wc);
        // janela invisível de verdade (não "só mensagens"), para receber o aviso de que o Explorer reiniciou
        hwnd = CreateWindowEx(0, className, "Backeddy", 0, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, wc.hInstance, IntPtr.Zero);
        hIcon = LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, GetSystemMetrics(SM_CXSMICON), GetSystemMetrics(SM_CYSMICON), LR_LOADFROMFILE);
        taskbarCreated = RegisterWindowMessage("TaskbarCreated");
        Add();
    }

    void Add()
    {
        var d = Data();
        d.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP;
        d.uCallbackMessage = WM_TRAY;
        d.hIcon = hIcon;
        d.szTip = tip;
        Shell_NotifyIcon(NIM_ADD, ref d);
    }

    /// <summary>Aviso perto do relógio (vira notificação no Windows 10/11). Clicar nele abre a janela.</summary>
    public void Notify(string title, string text)
    {
        if (disposed) return;
        var d = Data();
        d.uFlags = NIF_INFO;
        d.szInfoTitle = title.Length > 63 ? title[..63] : title;
        d.szInfo = text.Length > 255 ? text[..255] : text;
        d.dwInfoFlags = NIIF_WARNING;
        Shell_NotifyIcon(NIM_MODIFY, ref d);
    }

    NOTIFYICONDATA Data() => new()
    {
        cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
        hWnd = hwnd,
        uID = 1,
        szTip = "",
        szInfo = "",
        szInfoTitle = "",
    };

    IntPtr WindowProc(IntPtr h, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_TRAY)
        {
            uint ev = (uint)(lParam.ToInt64() & 0xFFFF);
            if (ev == WM_LBUTTONUP || ev == WM_BALLOONUSERCLICK) onOpen();
            else if (ev == WM_RBUTTONUP) ShowMenu();
            return IntPtr.Zero;
        }
        if (msg == taskbarCreated && taskbarCreated != 0)
        {
            Add();
            return IntPtr.Zero;
        }
        return DefWindowProc(h, msg, wParam, lParam);
    }

    void ShowMenu()
    {
        var menu = CreatePopupMenu();
        AppendMenu(menu, MF_STRING, (UIntPtr)CmdOpen, Strings.T("trayOpen"));
        AppendMenu(menu, MF_SEPARATOR, UIntPtr.Zero, null);
        AppendMenu(menu, MF_STRING, (UIntPtr)CmdQuit, Strings.T("trayQuit"));
        GetCursorPos(out var pt);
        SetForegroundWindow(hwnd);   // sem isso o menu não fecha ao clicar fora
        int cmd = TrackPopupMenu(menu, TPM_RIGHTBUTTON | TPM_RETURNCMD, pt.X, pt.Y, 0, hwnd, IntPtr.Zero);
        PostMessage(hwnd, WM_NULL, IntPtr.Zero, IntPtr.Zero);
        DestroyMenu(menu);
        if (cmd == CmdOpen) onOpen();
        else if (cmd == CmdQuit) onQuit();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        var d = Data();
        Shell_NotifyIcon(NIM_DELETE, ref d);
        if (hIcon != IntPtr.Zero) DestroyIcon(hIcon);
        if (hwnd != IntPtr.Zero) DestroyWindow(hwnd);
    }

    // ---------- API do Windows ----------

    delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct WNDCLASSEX
    {
        public uint cbSize, style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra, cbWndExtra;
        public IntPtr hInstance, hIcon, hCursor, hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct NOTIFYICONDATA
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID, uFlags, uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public uint dwState, dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public uint uVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct POINT { public int X, Y; }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern ushort RegisterClassEx(ref WNDCLASSEX wc);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr CreateWindowEx(uint exStyle, string className, string windowName, uint style, int x, int y, int w, int h,
        IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);
    [DllImport("user32.dll")] static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] static extern bool DestroyWindow(IntPtr hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern uint RegisterWindowMessage(string name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr LoadImage(IntPtr hInst, string name, uint type, int cx, int cy, uint load);
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr hIcon);
    [DllImport("user32.dll")] static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] static extern IntPtr CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool AppendMenu(IntPtr menu, uint flags, UIntPtr id, string? text);
    [DllImport("user32.dll")] static extern int TrackPopupMenu(IntPtr menu, uint flags, int x, int y, int reserved, IntPtr hWnd, IntPtr rect);
    [DllImport("user32.dll")] static extern bool DestroyMenu(IntPtr menu);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT pt);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr GetModuleHandle(string? name);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern bool Shell_NotifyIcon(uint message, ref NOTIFYICONDATA data);
}
