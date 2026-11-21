using System;using System.Drawing;using System.Drawing.Imaging;using System.Runtime.InteropServices;
class Shot{
 [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct DM{[MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string n;public short a,b,c,d;public int f;public int x,y,o,fo;public short col,dup,yres,tt,co;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string fn;public short lp;public int bpp,w,h,df,freq;}
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern bool EnumDisplaySettings(string d,int m,ref DM dm);
 static int Main(string[] a){SetProcessDPIAware();var dm=new DM();dm.c=(short)Marshal.SizeOf(typeof(DM));EnumDisplaySettings(null,-1,ref dm);
  if(a.Length==0){Console.WriteLine(dm.w+"x"+dm.h+"@"+dm.freq);return 0;}
  using(var b=new Bitmap(dm.w,dm.h)){using(var g=Graphics.FromImage(b))g.CopyFromScreen(0,0,0,0,b.Size);
   int sw=a.Length>1?int.Parse(a[1]):dm.w; using(var s=new Bitmap(b,sw,sw*dm.h/dm.w)) s.Save(a[0],ImageFormat.Png);}return 0;}}
