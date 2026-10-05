using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Teleprompter
{
    /// <summary>Simple key=value settings stored in %APPDATA%\Teleprompter.</summary>
    public class Settings
    {
        public string FontFamily = "Arial";
        public double FontSize = 90;        // in "virtual" units (screen is always 1920 units wide)
        public double LineSpacing = 1.3;
        public double Margin = 150;         // left/right margin, virtual units
        public string Align = "Center";     // Left | Center | Right (absolute, on screen)
        public bool Rtl = true;             // never persisted: app always starts in RTL
        public bool Mirror = false;
        public bool Flip = false;
        public bool Cue = true;
        public double Speed = 80;           // virtual units per second
        public string Monitor = "";
        public double ReadingLine = 0.30;   // reading-marker position, fraction of screen height from the top
        public bool Dark = false;
        public double VoiceThreshold = -40; // dBFS
        public double VoiceHold = 0.9;      // seconds text keeps moving after the voice stops
        public string Mic = "";

        static string Dir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Teleprompter"); }
        }
        static string SettingsFile { get { return Path.Combine(Dir, "settings.txt"); } }
        static string ScriptFile { get { return Path.Combine(Dir, "last-script.txt"); } }

        public static Settings Load()
        {
            Settings s = new Settings();
            try
            {
                if (!File.Exists(SettingsFile)) return s;
                Dictionary<string, string> d = new Dictionary<string, string>();
                foreach (string line in File.ReadAllLines(SettingsFile, Encoding.UTF8))
                {
                    int i = line.IndexOf('=');
                    if (i > 0) d[line.Substring(0, i).Trim()] = line.Substring(i + 1).Trim();
                }
                s.FontFamily = Str(d, "FontFamily", s.FontFamily);
                s.FontSize = Num(d, "FontSize", s.FontSize);
                s.LineSpacing = Num(d, "LineSpacing", s.LineSpacing);
                s.Margin = Num(d, "Margin", s.Margin);
                s.Align = Str(d, "Align", s.Align);
                s.Mirror = Bool(d, "Mirror", s.Mirror);
                s.Flip = Bool(d, "Flip", s.Flip);
                s.Cue = Bool(d, "Cue", s.Cue);
                s.Speed = Num(d, "Speed", s.Speed);
                s.Monitor = Str(d, "Monitor", s.Monitor);
                s.ReadingLine = Num(d, "ReadingLine", s.ReadingLine);
                s.Dark = Bool(d, "Dark", s.Dark);
                s.VoiceThreshold = Num(d, "VoiceThreshold", s.VoiceThreshold);
                s.VoiceHold = Num(d, "VoiceHold", s.VoiceHold);
                s.Mic = Str(d, "Mic", s.Mic);
            }
            catch { }
            return s;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                CultureInfo c = CultureInfo.InvariantCulture;
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("FontFamily=" + FontFamily);
                sb.AppendLine("FontSize=" + FontSize.ToString(c));
                sb.AppendLine("LineSpacing=" + LineSpacing.ToString(c));
                sb.AppendLine("Margin=" + Margin.ToString(c));
                sb.AppendLine("Align=" + Align);
                sb.AppendLine("Mirror=" + Mirror);
                sb.AppendLine("Flip=" + Flip);
                sb.AppendLine("Cue=" + Cue);
                sb.AppendLine("Speed=" + Speed.ToString(c));
                sb.AppendLine("Monitor=" + Monitor);
                sb.AppendLine("ReadingLine=" + ReadingLine.ToString(c));
                sb.AppendLine("Dark=" + Dark);
                sb.AppendLine("VoiceThreshold=" + VoiceThreshold.ToString(c));
                sb.AppendLine("VoiceHold=" + VoiceHold.ToString(c));
                sb.AppendLine("Mic=" + Mic);
                File.WriteAllText(SettingsFile, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        public static string LoadScript()
        {
            try { if (File.Exists(ScriptFile)) return File.ReadAllText(ScriptFile, Encoding.UTF8); }
            catch { }
            return null;
        }

        public static void SaveScript(string text)
        {
            try { Directory.CreateDirectory(Dir); File.WriteAllText(ScriptFile, text ?? "", new UTF8Encoding(true)); }
            catch { }
        }

        /// <summary>Reads a text file as UTF-8 (with or without BOM / UTF-16 BOM); falls back to Windows-1255 Hebrew.</summary>
        public static string ReadTextFile(string path)
        {
            byte[] b = File.ReadAllBytes(path);
            if (b.Length >= 2 && ((b[0] == 0xFF && b[1] == 0xFE) || (b[0] == 0xFE && b[1] == 0xFF)))
                return File.ReadAllText(path); // UTF-16 with BOM
            try
            {
                string s = new UTF8Encoding(false, true).GetString(b);
                if (s.Length > 0 && s[0] == '﻿') s = s.Substring(1);
                return s;
            }
            catch (DecoderFallbackException)
            {
                return Encoding.GetEncoding(1255).GetString(b);
            }
        }

        static string Str(Dictionary<string, string> d, string k, string def)
        {
            string v; return d.TryGetValue(k, out v) && v.Length > 0 ? v : def;
        }
        static double Num(Dictionary<string, string> d, string k, double def)
        {
            string v; double r;
            return d.TryGetValue(k, out v) && double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out r) ? r : def;
        }
        static bool Bool(Dictionary<string, string> d, string k, bool def)
        {
            string v; bool r;
            return d.TryGetValue(k, out v) && bool.TryParse(v, out r) ? r : def;
        }
    }
}
