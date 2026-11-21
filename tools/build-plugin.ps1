# Builds the native Ballance HD plugin (src\plugin) -> out\plugin\BallanceHD.dll
# Toolchain (portable, not in git - see Werkzeuge\): llvm-mingw (clang + lld) and the reconstructed Virtools SDK 2.1
#   https://github.com/mstorsjo/llvm-mingw   https://github.com/doyaGu/Virtools-SDK-2.1 (non-commercial use only)
# The engine is built with Visual C++: clang compiles for the Microsoft C++ ABI (i686-pc-windows-msvc), links
# with lld-link against the SDK's MSVC import libraries, without the Visual C++ runtime.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$w = Join-Path $root 'Werkzeuge'
$llvm = (Get-ChildItem $w -Directory -Filter 'llvm-mingw-*' | Select-Object -First 1).FullName
$sdk = Join-Path $w 'Virtools-SDK-2.1-main'
if (-not $llvm -or -not (Test-Path $sdk)) { throw 'Werkzeuge\llvm-mingw-* or Werkzeuge\Virtools-SDK-2.1-main missing' }
if (-not (Test-Path "$llvm\bin\lld-link.exe")) { Copy-Item "$llvm\bin\ld.lld.exe" "$llvm\bin\lld-link.exe" }
$src = Join-Path $root 'src\plugin'
$out = Join-Path $root 'out\plugin'
New-Item -ItemType Directory -Force $out | Out-Null
$ml = "$llvm\i686-w64-mingw32\lib"

$cflags = @('--target=i686-pc-windows-msvc', '-fms-extensions', '-fms-compatibility', '-fms-compatibility-version=19.29',
            '-fgnuc-version=4.2.1', '-std=c++14', '-O2', '-fno-exceptions', '-fno-rtti', '-fno-stack-protector',
            '-mno-stack-arg-probe', '-fno-math-errno', '-fno-threadsafe-statics', '-Wno-writable-strings', '-Wno-nonportable-include-path',
            '-DWIN32', '-D_WINDOWS', '-DNOMINMAX', '-D_ALLOCA_S_MARKER_SIZE=8', '-nostdinc',
            '-isystem', "$src\compat", '-isystem', "$llvm\lib\clang\23\include", '-isystem', "$llvm\include", '-I', "$sdk\Include")
$objs = @()
foreach ($f in Get-ChildItem "$src\*.cpp") {
    $obj = Join-Path $out ($f.BaseName + '.obj')
    & "$llvm\bin\clang++.exe" @cflags -c $f.FullName -o $obj
    if ($LASTEXITCODE -ne 0) { throw "compile $($f.Name) failed" }
    $objs += $obj
}
# the Windows side (controllers, keys, files) in plain C with the normal Windows headers
$wflags = @('--target=i686-w64-windows-gnu', '-O2', '-fno-stack-protector', '-mno-stack-arg-probe', '-fno-math-errno', '-fno-asynchronous-unwind-tables',
            '-Wall', '-Wno-unused-function', '-D_UCRT')
foreach ($f in Get-ChildItem "$src\*.c") {
    $obj = Join-Path $out ($f.BaseName + '.c.obj')
    & "$llvm\bin\clang.exe" @wflags -c $f.FullName -o $obj
    if ($LASTEXITCODE -ne 0) { throw "compile $($f.Name) failed" }
    $objs += $obj
}
& "$llvm\bin\lld-link.exe" /DLL /MACHINE:X86 /NODEFAULTLIB /ENTRY:DllMain /SAFESEH:NO "/OUT:$out\BallanceHD.dll" @objs `
    "$sdk\Lib\CK2.lib" "$sdk\Lib\VxMath.lib" "$ml\libkernel32.a" "$ml\libuser32.a" "$ml\libsetupapi.a" "$ml\libmsvcrt.a"
if ($LASTEXITCODE -ne 0) { throw 'link failed' }
Write-Host ("== {0} ({1:N1} KB)" -f "$out\BallanceHD.dll", ((Get-Item "$out\BallanceHD.dll").Length / 1KB))
