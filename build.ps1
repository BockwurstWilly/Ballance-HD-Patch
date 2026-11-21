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
    # button glyphs by Arks (https://arks.itch.io), CC BY 4.0
    'PS4 BUTTONS - Assets 1_1.zip'       = 'D34D71BD0CD6EA9EC296ACEA042439C151D897C556B67F19737A45EBEFC504C3'
    'XBOX BUTTONS - Assets 1_0.zip'      = '267434E110E63F40406A659304A70786FD7F654CB06282B0D1CCF9B8AC68F433'
}

# Microsoft Visual C++ 2015-2022 runtime (x86) for BML+: fetched from Microsoft at build time (not kept in git),
# the setup installs it silently when it is missing
$vcredist = Join-Path $payload 'vc_redist.x86.exe'
if (-not (Test-Path $vcredist)) {
    Write-Host '== downloading vc_redist.x86.exe from Microsoft'
    $ProgressPreference = 'SilentlyContinue'
    Invoke-WebRequest 'https://aka.ms/vs/17/release/vc_redist.x86.exe' -OutFile $vcredist -UseBasicParsing
}
$sig = Get-AuthenticodeSignature $vcredist
if ($sig.Status -ne 'Valid' -or $sig.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation') { throw 'vc_redist.x86.exe: no valid Microsoft signature' }
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

# --- button glyphs: the few the game needs, as 128 px TGA (game) and 64 px PNG (launcher) ---------
Add-Type -AssemblyName System.Drawing
$psDir = Join-Path $x 'PS4 BUTTONS - Assets 1_1\PS4 BUTTONS - Premium Assets\PS4 BUTTONS - Premium Assets'
$xbDir = Join-Path $x 'XBOX BUTTONS - Assets 1_0\XBOX BUTTONS - Premium Assets\XBOX BUTTONS - Premium Assets'
$glyphs = @{
    xbox = @{
        a = 'Digital Buttons\ABXY\button_xbox_digital_a_1.png'; b = 'Digital Buttons\ABXY\button_xbox_digital_b_1.png'
        x = 'Digital Buttons\ABXY\button_xbox_digital_x_1.png'; y = 'Digital Buttons\ABXY\button_xbox_digital_y_1.png'
        lb = 'Digital Buttons\Shoulder\button_xbox_digital_bumper_light_1.png'; rb = 'Digital Buttons\Shoulder\button_xbox_digital_bumper_light_2.png'
        lt = 'Analog Triggers\button_xbox_analog_trigger_light_1.png'; rt = 'Analog Triggers\button_xbox_analog_trigger_light_2.png'
        start = 'Digital Buttons\System\button_xbox_digital_menu_1.png'; back = 'Digital Buttons\System\button_xbox_digital_view_1.png'
        dpad = 'D-Pad\button_xbox_dpad_light_7.png'; lstick = 'Analog Sticks\Left\button_xbox_analog_l_direction_11.png'
        rstick_lr = 'Analog Sticks\Right\button_xbox_analog_r_direction_10.png'; rstick_up = 'Analog Sticks\Right\button_xbox_analog_r_direction_1.png'
        rstick = 'Analog Sticks\Right\button_xbox_analog_r_direction_11.png'; l3 = 'Analog Sticks\Left\button_xbox_analog_l.png'; r3 = 'Analog Sticks\Right\button_xbox_analog_r.png'
    }
    ps = @{
        a = 'Main Buttons\Button - PS Cross 1.png'; b = 'Main Buttons\Button - PS Circle 1.png'
        x = 'Main Buttons\Button - PS Square 1.png'; y = 'Main Buttons\Button - PS Triangle 1.png'
        lb = 'R and L Inputs\Button - PS L1.png'; rb = 'R and L Inputs\Button - PS R1.png'
        lt = 'R and L Inputs\Button - PS L2.png'; rt = 'R and L Inputs\Button - PS R2.png'
        start = 'Other Inputs\Button - PS Options.png'; back = 'Other Inputs\Button - PS Share.png'
        dpad = 'Directional Arrows\Button - PS Directional Arrows.png'; lstick = 'Analogue L Inputs\Button - PS Analogue L Up Down Left Right.png'
        rstick_lr = 'Analogue R Inputs\Button - PS Analogue R Left Right.png'; rstick_up = 'Analogue R Inputs\Button - PS Analogue R Up.png'
        rstick = 'Analogue R Inputs\Button - PS Analogue R Up Down Left Right.png'; l3 = 'R and L Inputs\Button - PS L3.png'; r3 = 'R and L Inputs\Button - PS R3.png'
    }
}
$glyphOut = Join-Path $obj 'glyphs'
New-Item -ItemType Directory -Force "$glyphOut\tga", "$glyphOut\png" | Out-Null

function Resize-Image([string]$src, [int]$size) {
    $img = [System.Drawing.Image]::FromFile($src)
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = 'HighQualityBicubic'; $g.PixelOffsetMode = 'HighQuality'; $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.DrawImage($img, 0, 0, $size, $size)
    $g.Dispose(); $img.Dispose()
    $bmp
}

# uncompressed 32-bit TGA, top-left origin (Virtools' image reader loads TGA with alpha)
function Save-Tga($bmp, [string]$path) {
    $w = $bmp.Width; $h = $bmp.Height
    $rect = New-Object System.Drawing.Rectangle 0, 0, $w, $h
    $data = $bmp.LockBits($rect, 'ReadOnly', [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $pixels = New-Object byte[] ($w * $h * 4)
    for ($row = 0; $row -lt $h; $row++) {
        [Runtime.InteropServices.Marshal]::Copy([IntPtr]($data.Scan0.ToInt64() + $row * $data.Stride), $pixels, $row * $w * 4, $w * 4)
    }
    $bmp.UnlockBits($data)
    $hdr = New-Object byte[] 18
    $hdr[2] = 2; $hdr[12] = $w -band 0xFF; $hdr[13] = $w -shr 8; $hdr[14] = $h -band 0xFF; $hdr[15] = $h -shr 8; $hdr[16] = 32; $hdr[17] = 0x28
    $fs = [IO.File]::Create($path); $fs.Write($hdr, 0, 18); $fs.Write($pixels, 0, $pixels.Length); $fs.Close()
}

foreach ($kind in $glyphs.Keys) {
    $base = if ($kind -eq 'ps') { $psDir } else { $xbDir }
    foreach ($key in $glyphs[$kind].Keys) {
        $gsrc = Join-Path $base $glyphs[$kind][$key]
        if (-not (Test-Path -LiteralPath $gsrc)) { throw "glyph missing: $gsrc" }
        $big = Resize-Image $gsrc 128; Save-Tga $big "$glyphOut\tga\${kind}_$key.tga"; $big.Dispose()
        $small = Resize-Image $gsrc 64; $small.Save("$glyphOut\png\${kind}_$key.png", [System.Drawing.Imaging.ImageFormat]::Png); $small.Dispose()
    }
}
$glyphRes = Get-ChildItem "$glyphOut\png\*.png" | ForEach-Object { "/resource:$($_.FullName),glyph/$($_.Name)" }

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
Invoke-Checked 'launcher' { & $csc @cscBase "/out:$launcher" @common (Get-ChildItem "$src\launcher\*.cs" | ForEach-Object { $_.FullName }) @glyphRes }

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
foreach ($f in Get-ChildItem "$glyphOut\tga" -File) { $items["ModLoader/BallanceHD/glyphs/$($f.Name)"] = $f.FullName }
$items['doc/licenses/ButtonGlyphs-Arks-CC-BY-4.0.txt'] = "$root\docs\ButtonGlyphs-License.txt"
# installed to _HDPatch\redist and run by the setup when the runtime is missing
$items['redist/vc_redist.x86.exe'] = $vcredist
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
Copy-Item "$root\docs\ButtonGlyphs-License.txt" "$rel\Lizenzen - Licenses\ButtonGlyphs-Arks-CC-BY-4.0.txt"
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
