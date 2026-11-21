# Builds the Ballance HD patch: BallanceHDLauncher.exe and BallanceHD_Setup.exe (C# 5, .NET Framework 4.5+).
# Needs nothing but Windows: csc.exe of the .NET Framework that ships with Windows 10/11.
# Output: out\BallanceHD_Setup.exe (single file, everything embedded) and out\release\ (package for upload)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$src = Join-Path $root 'src'
$payload = Join-Path $root 'payload'
$out = Join-Path $root 'out'
$obj = Join-Path $out 'obj'
if (Test-Path $obj) { Remove-Item $obj -Recurse -Force }
New-Item -ItemType Directory -Force $obj | Out-Null

$fw = 'C:\Windows\Microsoft.NET\Framework\v4.0.30319'
$csc = Join-Path $fw 'csc.exe'

function Invoke-Checked([string]$what, [scriptblock]$cmd) {
    & $cmd
    if ($LASTEXITCODE -ne 0) { throw "$what failed (exit $LASTEXITCODE)" }
}

# --- third-party packages (checked against the SHA256 sums published with the releases) -----------
$packages = @{
    'BallancePlayer-0.3.9.0-vc6-x86.zip' = 'A4411C69E4CCD39310716BBC37886B195436964A5D7E40958818315515DADE79'
    'BMLPlus-v0.3.14.zip'                = 'BB7AA0DF79CECEBCFC9CFEE77280BF22B1DE980D8A788F5FA4CACEEAB8BDF785'
}
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$x = Join-Path $obj 'x'
foreach ($name in $packages.Keys) {
    $zip = Join-Path $payload $name
    $hash = (Get-FileHash $zip -Algorithm SHA256).Hash
    if ($hash -ne $packages[$name]) { throw "${name}: SHA256 $hash does not match the release" }
    [IO.Compression.ZipFile]::ExtractToDirectory($zip, (Join-Path $x ([IO.Path]::GetFileNameWithoutExtension($name))))
}
$bp = Join-Path $x 'BallancePlayer-0.3.9.0-vc6-x86\BallancePlayer-0.3.9.0-vc6-x86'
$bml = Join-Path $x 'BMLPlus-v0.3.14'

# --- icon (generated, so the package carries no game artwork): a wooden ball against a dusk sky --
$ico = Join-Path $obj 'ballancehd.ico'
Add-Type -AssemblyName System.Drawing
$frames = foreach ($size in 256, 48, 32, 16) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $rect = New-Object System.Drawing.Rectangle 0, 0, $size, $size
    $sky = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, ([System.Drawing.Color]::FromArgb(255, 34, 30, 60)), ([System.Drawing.Color]::FromArgb(255, 236, 140, 70)), 90
    $g.FillRectangle($sky, $rect)
    $m = $size * 0.12; $d = $size - 2 * $m
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddEllipse([float]$m, [float]$m, [float]$d, [float]$d)
    $ball = New-Object System.Drawing.Drawing2D.PathGradientBrush $path
    $ball.CenterPoint = New-Object System.Drawing.PointF ([float]($m + $d * 0.36)), ([float]($m + $d * 0.3))
    $ball.CenterColor = [System.Drawing.Color]::FromArgb(255, 246, 214, 160)
    $ball.SurroundColors = @([System.Drawing.Color]::FromArgb(255, 112, 66, 30))
    $g.FillPath($ball, $path)
    if ($size -ge 32) {
        $stripe = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(170, 90, 50, 20)), ([Math]::Max(1.2, $size / 22))
        $g.SetClip($path)
        $g.DrawEllipse($stripe, [float]($m + $d * 0.2), [float]$m, [float]($d * 0.6), [float]$d)
        $g.DrawLine($stripe, [float]$m, [float]($m + $d * 0.5), [float]($m + $d), [float]($m + $d * 0.5))
        $g.ResetClip()
    }
    $g.Dispose()
    $ms = New-Object IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    , @($size, $ms.ToArray())
}
$fs = [IO.File]::Create($ico)
$w = New-Object IO.BinaryWriter $fs
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$frames.Count)
$offset = 6 + 16 * $frames.Count
foreach ($f in $frames) {
    $s = if ($f[0] -ge 256) { 0 } else { $f[0] }
    $w.Write([byte]$s); $w.Write([byte]$s); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32); $w.Write([uint32]$f[1].Length); $w.Write([uint32]$offset)
    $offset += $f[1].Length
}
foreach ($f in $frames) { $w.Write($f[1]) }
$w.Close()

# --- C# ---------------------------------------------------------------------------------------
$refs = @('System.dll', 'System.Core.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll', 'Microsoft.CSharp.dll',
          'System.IO.Compression.dll') | ForEach-Object { "/reference:$fw\$_" }
$common = Get-ChildItem "$src\common\*.cs" | ForEach-Object { $_.FullName }
$cscBase = @('/nologo', '/target:winexe', '/optimize+', '/warnaserror+', '/platform:anycpu',
             "/win32manifest:$src\app.manifest", "/win32icon:$ico", '/utf8output', '/codepage:65001',
             "/resource:$src\res\compiware.png,res/compiware.png") + $refs

Write-Host '== BallanceHDLauncher.exe'
$launcher = Join-Path $obj 'BallanceHDLauncher.exe'
Invoke-Checked 'launcher' { & $csc @cscBase "/out:$launcher" @common (Get-ChildItem "$src\launcher\*.cs" | ForEach-Object { $_.FullName }) }

# --- payload.zip: paths relative to the game folder; doc/... ends up in _HDPatch\doc ----------------
Write-Host '== payload.zip'
$stub = Join-Path $obj 'dpnhpast.dll'
& (Join-Path $root 'tools\StubDll.ps1') -Path $stub
$items = [ordered]@{
    'Bin/Player.exe'                       = "$bp\Player.exe"
    'Bin/dpnhpast.dll'                     = $stub
    'BuildingBlocks/BMLPlus.dll'           = "$bml\BuildingBlocks\BMLPlus.dll"
    'BuildingBlocks/AngelScript.dll'       = "$bml\BuildingBlocks\AngelScript.dll"
    'ModLoader/Mods/BallanceHD.mod.as'     = "$src\mod\BallanceHD.mod.as"
    'BallanceHDLauncher.exe'               = $launcher
    'doc/Liesmich - Readme.txt'            = "$root\README.txt"
    'doc/THIRD-PARTY.txt'                  = "$root\THIRD-PARTY.txt"
    'doc/licenses/BallanceHD-License.txt'  = "$root\LICENSE"
    'doc/licenses/BallancePlayer-License.txt' = "$bp\LICENSE"
    'doc/licenses/BMLPlus-License.txt'     = "$bml\LICENSE"
}
foreach ($f in Get-ChildItem "$bml\ModLoader\Fonts" -File) { $items["ModLoader/Fonts/$($f.Name)"] = $f.FullName }
foreach ($f in Get-ChildItem "$bml\ModLoader\Mods" -File) { $items["ModLoader/Mods/$($f.Name)"] = $f.FullName }
$pzip = Join-Path $obj 'payload.zip'
$za = [IO.Compression.ZipFile]::Open($pzip, 'Create')
try {
    foreach ($k in $items.Keys) { [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($za, $items[$k], $k, 'Optimal') }
}
finally { $za.Dispose() }

Write-Host '== BallanceHD_Setup.exe'
$setup = Join-Path $out 'BallanceHD_Setup.exe'
Invoke-Checked 'setup' { & $csc @cscBase "/out:$setup" @common (Get-ChildItem "$src\setup\*.cs" | ForEach-Object { $_.FullName }) "/resource:$pzip,payload.zip" }
Copy-Item $launcher (Join-Path $out 'BallanceHDLauncher.exe') -Force

$hash = (Get-FileHash $setup -Algorithm SHA256).Hash
Write-Host ("== done: {0} ({1:N1} MB) SHA256 {2}" -f $setup, ((Get-Item $setup).Length / 1MB), $hash)

# --- release package ----------------------------------------------------------------------------
$version = ([regex]::Match((Get-Content "$src\common\Text.cs" -Raw), 'Text = "([^"]+)"')).Groups[1].Value
$name = "BallanceHD_Patch_$version"
$rel = Join-Path $out "release\$name"
if (Test-Path $rel) { Remove-Item $rel -Recurse -Force }
New-Item -ItemType Directory -Force "$rel\Lizenzen - Licenses" | Out-Null
Copy-Item $setup "$rel\BallanceHD_Setup.exe"
Copy-Item "$root\README.txt" "$rel\Liesmich - Readme.txt"
Copy-Item "$root\THIRD-PARTY.txt" "$rel\Lizenzen - Licenses\THIRD-PARTY.txt"
Copy-Item "$root\LICENSE" "$rel\Lizenzen - Licenses\BallanceHD-License.txt"
Copy-Item "$bp\LICENSE" "$rel\Lizenzen - Licenses\BallancePlayer-License.txt"
Copy-Item "$bml\LICENSE" "$rel\Lizenzen - Licenses\BMLPlus-License.txt"
"$hash *BallanceHD_Setup.exe" | Set-Content "$rel\BallanceHD_Setup.exe.sha256" -Encoding ASCII
$zip = Join-Path $out "release\$name.zip"
if (Test-Path $zip) { Remove-Item $zip }
$za = [IO.Compression.ZipFile]::Open($zip, 'Create')
try {
    foreach ($f in Get-ChildItem $rel -Recurse -File) {
        $entry = $name + '/' + $f.FullName.Substring($rel.Length + 1).Replace([char]92, [char]47)
        [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($za, $f.FullName, $entry, 'Optimal')
    }
}
finally { $za.Dispose() }
Write-Host ("== release: {0} ({1:N1} MB)" -f $zip, ((Get-Item $zip).Length / 1MB))
