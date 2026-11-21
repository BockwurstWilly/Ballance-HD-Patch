using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace BallanceHD
{
    /// <summary>Minimal INI editor that keeps comments and order (Player.ini, BallanceHD.ini).</summary>
    class IniDoc
    {
        readonly List<string> lines;
        readonly string path;

        IniDoc(string path, List<string> lines)
        {
            this.path = path;
            this.lines = lines;
        }

        public static IniDoc Load(string path)
        {
            var lines = File.Exists(path) ? new List<string>(File.ReadAllLines(path, Encoding.Default)) : new List<string>();
            return new IniDoc(path, lines);
        }

        public string Get(string section, string key, string fallback = null)
        {
            int i = Find(section, key);
            if (i < 0)
                return fallback;
            var line = lines[i];
            return line.Substring(line.IndexOf('=') + 1).Trim();
        }

        public int GetInt(string section, string key, int fallback)
        {
            int v;
            return int.TryParse(Get(section, key), out v) ? v : fallback;
        }

        public bool GetBool(string section, string key, bool fallback)
        {
            var v = Get(section, key);
            return v == null ? fallback : v.Trim() != "0";
        }

        public void Set(string section, string key, string value)
        {
            int i = Find(section, key);
            if (i >= 0)
            {
                var line = lines[i];
                int eq = line.IndexOf('=');
                lines[i] = line.Substring(0, eq + 1) + value;
                return;
            }
            int sec = FindSection(section);
            if (sec < 0)
            {
                lines.Add("[" + section + "]");
                lines.Add(key + "=" + value);
                return;
            }
            int insert = sec + 1;
            while (insert < lines.Count && !lines[insert].TrimStart().StartsWith("["))
                insert++;
            while (insert > sec + 1 && lines[insert - 1].Trim().Length == 0)
                insert--;
            lines.Insert(insert, key + "=" + value);
        }

        public void Set(string section, string key, int value) { Set(section, key, value.ToString()); }
        public void Set(string section, string key, bool value) { Set(section, key, value ? "1" : "0"); }

        public void Save()
        {
            var text = string.Join("\r\n", lines) + "\r\n";
            FileUtil.WriteAtomic(path, Encoding.Default.GetBytes(text));
        }

        int FindSection(string section)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                var t = lines[i].Trim();
                if (t.StartsWith("[") && t.EndsWith("]") &&
                    string.Equals(t.Substring(1, t.Length - 2).Trim(), section, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }

        int Find(string section, string key)
        {
            int sec = FindSection(section);
            if (sec < 0)
                return -1;
            for (int i = sec + 1; i < lines.Count; i++)
            {
                var t = lines[i].Trim();
                if (t.StartsWith("["))
                    break;
                if (t.StartsWith(";") || t.StartsWith("#"))
                    continue;
                int eq = t.IndexOf('=');
                if (eq > 0 && string.Equals(t.Substring(0, eq).Trim(), key, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }
    }

    /// <summary>
    /// The BML+ config format (ModLoader\Configs\*.cfg):
    ///   Category {
    ///       # comment
    ///       B Key 1          (B bool, I int, F float, S string, K key code)
    ///   }
    /// BML+ keeps values it finds and fills in the rest with its defaults, so a partial file is fine.
    /// </summary>
    class BmlCfg
    {
        readonly List<string> lines;
        readonly string path;

        BmlCfg(string path, List<string> lines)
        {
            this.path = path;
            this.lines = lines;
        }

        public static BmlCfg Load(string path)
        {
            var lines = File.Exists(path) ? new List<string>(File.ReadAllLines(path, Encoding.UTF8)) : new List<string>();
            return new BmlCfg(path, lines);
        }

        int Category(string name)
        {
            var re = new Regex(@"^\s*" + Regex.Escape(name) + @"\s*\{\s*$", RegexOptions.IgnoreCase);
            for (int i = 0; i < lines.Count; i++)
                if (re.IsMatch(lines[i]))
                    return i;
            return -1;
        }

        int Find(string category, string key, out int end)
        {
            end = -1;
            int c = Category(category);
            if (c < 0)
                return -1;
            var re = new Regex(@"^\s*[BIFSK]\s+" + Regex.Escape(key) + @"(\s|$)");
            for (int i = c + 1; i < lines.Count; i++)
            {
                if (lines[i].Trim() == "}")
                {
                    end = i;
                    return -1;
                }
                if (re.IsMatch(lines[i]))
                    return i;
            }
            end = lines.Count;
            return -1;
        }

        public string Get(string category, string key)
        {
            int end;
            int i = Find(category, key, out end);
            if (i < 0)
                return null;
            var parts = lines[i].Trim().Split(new[] { ' ', '\t' }, 3, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 3 ? parts[2] : "";
        }

        public bool GetBool(string category, string key, bool fallback)
        {
            var v = Get(category, key);
            return v == null ? fallback : v.Trim() != "0";
        }

        public int GetInt(string category, string key, int fallback)
        {
            int v;
            return int.TryParse(Get(category, key), out v) ? v : fallback;
        }

        public void Set(string category, char type, string key, string value)
        {
            int end;
            int i = Find(category, key, out end);
            string line = "\t" + type + " " + key + " " + value;
            if (i >= 0)
            {
                lines[i] = line;
                return;
            }
            if (end >= 0)
            {
                lines.Insert(end, line);
                return;
            }
            if (lines.Count > 0 && lines[lines.Count - 1].Trim().Length > 0)
                lines.Add("");
            lines.Add(category + " {");
            lines.Add(line);
            lines.Add("}");
        }

        public void SetBool(string category, string key, bool value) { Set(category, 'B', key, value ? "1" : "0"); }
        public void SetInt(string category, string key, int value) { Set(category, 'I', key, value.ToString()); }

        public void Save()
        {
            if (lines.Count == 0 || !lines[0].StartsWith("#"))
                lines.Insert(0, "# Configuration File - written by the Ballance HD launcher");
            var text = string.Join("\r\n", lines) + "\r\n";
            FileUtil.WriteAtomic(path, new UTF8Encoding(false).GetBytes(text));
        }
    }

    /// <summary>RenderEngines\CK2_3D.ini: "Key = value" lines inside &lt;CK2_3D&gt; … &lt;/CK2_3D&gt;.</summary>
    static class RenderIni
    {
        public static string Get(string path, string key)
        {
            if (!File.Exists(path))
                return null;
            var re = new Regex(@"^\s*" + Regex.Escape(key) + @"\s*=\s*(\S+)", RegexOptions.IgnoreCase);
            foreach (var line in File.ReadAllLines(path, Encoding.Default))
            {
                var m = re.Match(line);
                if (m.Success)
                    return m.Groups[1].Value;
            }
            return null;
        }

        public static void Set(string path, IDictionary<string, string> values)
        {
            var lines = new List<string>(File.ReadAllLines(path, Encoding.Default));
            foreach (var kv in values)
            {
                var re = new Regex(@"^(\s*" + Regex.Escape(kv.Key) + @"\s*=\s*)\S*(.*)$", RegexOptions.IgnoreCase);
                bool found = false;
                for (int i = 0; i < lines.Count; i++)
                {
                    if (lines[i].TrimStart().StartsWith("#"))
                        continue;
                    var m = re.Match(lines[i]);
                    if (m.Success)
                    {
                        lines[i] = m.Groups[1].Value + kv.Value;
                        found = true;
                    }
                }
                if (!found)
                {
                    int close = lines.FindLastIndex(l => l.Trim().Equals("</CK2_3D>", StringComparison.OrdinalIgnoreCase));
                    if (close < 0)
                        throw new InvalidDataException(path + ": </CK2_3D> not found");
                    lines.Insert(close, "\t" + kv.Key + " = " + kv.Value);
                }
            }
            FileUtil.WriteAtomic(path, Encoding.Default.GetBytes(string.Join("\r\n", lines) + "\r\n"));
        }
    }

    static class FileUtil
    {
        /// <summary>Write via a temporary file, so a crash never leaves a half-written file behind.</summary>
        public static void WriteAtomic(string path, byte[] data)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            var tmp = path + ".tmp";
            File.WriteAllBytes(tmp, data);
            if (File.Exists(path))
            {
                File.SetAttributes(path, FileAttributes.Normal);
                File.Replace(tmp, path, null);
            }
            else
            {
                File.Move(tmp, path);
            }
        }
    }
}
