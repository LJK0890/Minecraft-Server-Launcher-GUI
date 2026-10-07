# 截图脚本：必须让本进程在加载 System.Windows.Forms / System.Drawing *之前*
# 就变成 DPI 感知的，否则 Screen.Bounds 返回的是被虚拟化过的逻辑尺寸
# （本机 150% 缩放：物理 2560x1600 会被报告成 1707x1067），
# 于是 CopyFromScreen 只截到物理屏幕的左上角，窗口下半部分和右侧全部丢失。

if (-not ("Win32Shot" -as [type])) {
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public class Win32Shot {
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
}
'@
}
[Win32Shot]::SetProcessDPIAware() | Out-Null

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

$HWND_TOPMOST   = [IntPtr]::new(-1)
$SWP_NOMOVE     = 0x0002
$SWP_NOSIZE     = 0x0001
$SWP_SHOWWINDOW = 0x0040

$exe = "D:\.projects\MinecraftServerLauncher\bin\Debug\net10.0\MinecraftServerLauncher.exe"
$outDir = "D:\.projects\MinecraftServerLauncher\.shots"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
"Physical screen: $($b.Width)x$($b.Height)"

foreach ($page in @('Main', 'ServerConfig', 'AiConfig', 'AppSettings', 'About')) {
    $p = Start-Process -FilePath $exe -ArgumentList '--page', $page -PassThru
    Start-Sleep -Seconds 6
    $p.Refresh()
    if ($p.HasExited) { "PAGE $page EXITED: $($p.ExitCode)"; continue }

    $hwnd = $p.MainWindowHandle
    [Win32Shot]::SetWindowPos($hwnd, $HWND_TOPMOST, 0, 0, 0, 0, ($SWP_NOMOVE -bor $SWP_NOSIZE -bor $SWP_SHOWWINDOW)) | Out-Null
    [Win32Shot]::SetForegroundWindow($hwnd) | Out-Null
    Start-Sleep -Seconds 2

    $bmp = New-Object System.Drawing.Bitmap($b.Width, $b.Height)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($b.Location, [System.Drawing.Point]::Empty, $b.Size)
    $file = Join-Path $outDir "page-$page.png"
    $bmp.Save($file, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    "SAVED=$file"

    $p.Kill()
    Start-Sleep -Seconds 1
}
