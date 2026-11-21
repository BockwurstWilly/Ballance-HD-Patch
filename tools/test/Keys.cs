using System;using System.Threading;using System.Runtime.InteropServices;
// Keys.exe key[:holdMs] ... - sends DirectInput-visible scan codes; "wait:ms" pauses
class Keys{
 [StructLayout(LayoutKind.Sequential)] struct KI{public ushort vk,scan;public uint flags,time;public IntPtr extra;}
 [StructLayout(LayoutKind.Explicit,Size=40)] struct IN{[FieldOffset(0)]public uint type;[FieldOffset(8)]public KI ki;}
 [DllImport("user32.dll")] static extern uint SendInput(uint n,IN[] i,int s);
 static void K(ushort sc,bool up){bool ext=sc>0xFF;var i=new IN{type=1};i.ki.scan=(ushort)(sc&0xFF);i.ki.flags=8u|(up?2u:0u)|(ext?1u:0u);SendInput(1,new[]{i},Marshal.SizeOf(typeof(IN)));}
 static ushort Sc(string n){switch(n){case"enter":return 0x1C;case"esc":return 1;case"space":return 0x39;case"up":return 0x1C8;case"down":return 0x1D0;case"left":return 0x1CB;case"right":return 0x1CD;case"shift":return 0x2A;case"q":return 0x10;}return Convert.ToUInt16(n,16);}
 static void Main(string[] a){foreach(var x in a){var p=x.Split(':');int ms=p.Length>1?int.Parse(p[1]):120;if(p[0]=="wait"){Thread.Sleep(ms);continue;}if(p[0].StartsWith("+")){K(Sc(p[0].Substring(1)),false);Thread.Sleep(35);continue;}if(p[0].StartsWith("-")){K(Sc(p[0].Substring(1)),true);Thread.Sleep(35);continue;}var s=Sc(p[0]);K(s,false);Thread.Sleep(ms);K(s,true);Thread.Sleep(150);}}}

