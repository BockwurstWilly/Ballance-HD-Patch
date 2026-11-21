# Writes a minimal, valid x86 PE DLL without code (no entry point, no exports, no imports).
# LoadLibrary() succeeds on it - used for dpnhpast.dll, which the game only loads to detect DirectX 8.1;
# without it Windows 10/11 pops up the "install DirectPlay" dialog on every start.
param([Parameter(Mandatory)][string]$Path)
$b = New-Object byte[] 0x400
function W16($o,$v){ [BitConverter]::GetBytes([uint16]$v).CopyTo($b,$o) }
function W32($o,$v){ [BitConverter]::GetBytes([uint32]$v).CopyTo($b,$o) }
W16 0 0x5A4D; W32 0x3C 0x40                      # MZ, e_lfanew
W32 0x40 0x00004550                               # PE\0\0
$c = 0x44
W16 $c 0x14C; W16 ($c+2) 1; W16 ($c+16) 0xE0; W16 ($c+18) 0x2102   # i386, 1 section, opt size, DLL|32BIT|EXE
$o = $c + 20
W16 $o 0x10B; $b[$o+2]=8                          # PE32, linker 8.0
W32 ($o+4) 0x200                                  # SizeOfCode (0 would also do)
W32 ($o+16) 0                                     # AddressOfEntryPoint: none
W32 ($o+20) 0x1000; W32 ($o+24) 0x1000            # BaseOfCode, BaseOfData
W32 ($o+28) 0x10000000                            # ImageBase
W32 ($o+32) 0x1000; W32 ($o+36) 0x200             # SectionAlignment, FileAlignment
W16 ($o+40) 5; W16 ($o+42) 1; W16 ($o+48) 5; W16 ($o+50) 1   # OS / subsystem version 5.1
W32 ($o+56) 0x2000; W32 ($o+60) 0x200             # SizeOfImage, SizeOfHeaders
W16 ($o+68) 2; W16 ($o+70) 0x0100                 # GUI, NX compatible
W32 ($o+72) 0x100000; W32 ($o+76) 0x1000; W32 ($o+80) 0x100000; W32 ($o+84) 0x1000
W32 ($o+92) 16                                    # NumberOfRvaAndSizes
$s = $o + 0xE0
[Text.Encoding]::ASCII.GetBytes('.rdata').CopyTo($b,$s)
W32 ($s+8) 0x10; W32 ($s+12) 0x1000; W32 ($s+16) 0x200; W32 ($s+20) 0x200; W32 ($s+36) 0x40000040
[IO.File]::WriteAllBytes($Path, $b)
