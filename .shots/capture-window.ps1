# 截图脚本（指定页面，可选先点击一次再截第二张）。
# 必须在加载 System.Windows.Forms / System.Drawing *之前*让本进程 DPI 感知，
# 否则 Screen.Bounds / CopyFromScreen 用的都是被虚拟化过的逻辑坐标，只会截到左上角。
if (-not ("Win32W" -as [type])) {
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public class Win32W {
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, IntPtr extra);
}
'@
}
[Win32W]::SetProcessDPIAware() | Out-Null
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

$page  = $env:MSL_PAGE
if (-not $page) { $page = 'Main' }
$click = $env:MSL_CLICK      # 形如 "1234,567"

$exe = "D:\.projects\MinecraftServerLauncher\bin\Debug\net10.0\MinecraftServerLauncher.exe"
$outDir = "D:\.projects\MinecraftServerLauncher\.shots"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$p = Start-Process -FilePath $exe -ArgumentList '--page', $page -PassThru
Start-Sleep -Seconds 6
$p.Refresh()
if ($p.HasExited) { "EXITED: $($p.ExitCode)"; exit 1 }

$hwnd = $p.MainWindowHandle
[Win32W]::SetWindowPos($hwnd, [IntPtr]::new(-1), 40, 30, 1800, 1150, 0x0040) | Out-Null
[Win32W]::SetForegroundWindow($hwnd) | Out-Null
Start-Sleep -Seconds 2

$bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
function Shot($name) {
    $bmp = New-Object System.Drawing.Bitmap($bounds.Width, $bounds.Height)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)
    $f = Join-Path $outDir $name
    $bmp.Save($f, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    "SAVED=$f"
}
Shot "page-$page.png"

if ($click) {
    $xy = $click.Split(',')
    $x = [int]$xy[0]; $y = [int]$xy[1]
    [Win32W]::SetCursorPos($x, $y) | Out-Null
    Start-Sleep -Milliseconds 400
    [Win32W]::mouse_event(0x0002, 0, 0, 0, [IntPtr]::Zero)   # LEFTDOWN
    Start-Sleep -Milliseconds 80
    [Win32W]::mouse_event(0x0004, 0, 0, 0, [IntPtr]::Zero)   # LEFTUP
    Start-Sleep -Seconds 2
    Shot "page-$page-after.png"
}

$p.Kill()
