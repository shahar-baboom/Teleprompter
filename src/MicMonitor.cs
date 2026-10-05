using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace Teleprompter
{
    /// <summary>
    /// Lightweight microphone level monitor using the built-in Windows waveIn API (winmm.dll).
    /// No drivers, libraries or internet needed. It only measures loudness - it does not record or
    /// send audio anywhere. Used for voice-activated scrolling: "speaking" = level above the threshold,
    /// held for a short time after the voice stops so normal pauses between words don't stop the text.
    /// </summary>
    public class MicMonitor : IDisposable
    {
        [StructLayout(LayoutKind.Sequential, Pack = 2)]
        struct WAVEFORMATEX
        {
            public ushort wFormatTag, nChannels;
            public uint nSamplesPerSec, nAvgBytesPerSec;
            public ushort nBlockAlign, wBitsPerSample, cbSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct WAVEHDR
        {
            public IntPtr lpData;
            public uint dwBufferLength, dwBytesRecorded;
            public IntPtr dwUser;
            public uint dwFlags, dwLoops;
            public IntPtr lpNext, reserved;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct WAVEINCAPS
        {
            public ushort wMid, wPid;
            public uint vDriverVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szPname;
            public uint dwFormats;
            public ushort wChannels, wReserved1;
        }

        [DllImport("winmm.dll")] static extern uint waveInGetNumDevs();
        [DllImport("winmm.dll", EntryPoint = "waveInGetDevCapsW", CharSet = CharSet.Unicode)]
        static extern int waveInGetDevCaps(IntPtr uDeviceID, ref WAVEINCAPS caps, uint size);
        [DllImport("winmm.dll")] static extern int waveInOpen(out IntPtr hwi, uint deviceId, ref WAVEFORMATEX fmt, IntPtr callback, IntPtr instance, uint flags);
        [DllImport("winmm.dll")] static extern int waveInPrepareHeader(IntPtr hwi, IntPtr hdr, uint size);
        [DllImport("winmm.dll")] static extern int waveInUnprepareHeader(IntPtr hwi, IntPtr hdr, uint size);
        [DllImport("winmm.dll")] static extern int waveInAddBuffer(IntPtr hwi, IntPtr hdr, uint size);
        [DllImport("winmm.dll")] static extern int waveInStart(IntPtr hwi);
        [DllImport("winmm.dll")] static extern int waveInReset(IntPtr hwi);
        [DllImport("winmm.dll")] static extern int waveInClose(IntPtr hwi);

        const uint WAVE_MAPPER = 0xFFFFFFFF;
        const uint WHDR_DONE = 1;
        const int SampleRate = 16000;
        const int BufferCount = 8;
        const int BufferBytes = SampleRate * 2 / 25; // 40 ms of 16-bit mono

        static readonly int HdrSize = Marshal.SizeOf(typeof(WAVEHDR));
        static readonly int OffFlags = Marshal.OffsetOf(typeof(WAVEHDR), "dwFlags").ToInt32();
        static readonly int OffRecorded = Marshal.OffsetOf(typeof(WAVEHDR), "dwBytesRecorded").ToInt32();

        IntPtr handle = IntPtr.Zero;
        readonly IntPtr[] headers = new IntPtr[BufferCount];
        readonly IntPtr[] buffers = new IntPtr[BufferCount];
        short[] samples = new short[BufferBytes / 2];
        int next;
        DispatcherTimer timer;
        int lastVoiceTick;
        bool heardVoice;

        /// <summary>Current level in dBFS (-90 .. 0), lightly smoothed for display.</summary>
        public double LevelDb = -90;
        /// <summary>Level (dBFS) above which we treat the input as speech.</summary>
        public double ThresholdDb = -40;
        /// <summary>How long (ms) "speaking" stays true after the voice drops below the threshold.</summary>
        public int HangoverMs = 900;

        public bool IsRunning { get { return handle != IntPtr.Zero; } }

        public bool Speaking
        {
            get { return IsRunning && heardVoice && unchecked(Environment.TickCount - lastVoiceTick) < HangoverMs; }
        }

        /// <summary>Names of the input devices; index in this list = device id.</summary>
        public static List<string> DeviceNames()
        {
            List<string> list = new List<string>();
            uint n = 0;
            try { n = waveInGetNumDevs(); } catch { return list; }
            for (uint i = 0; i < n; i++)
            {
                WAVEINCAPS caps = new WAVEINCAPS();
                string name = "Microphone " + (i + 1);
                if (waveInGetDevCaps(new IntPtr(i), ref caps, (uint)Marshal.SizeOf(typeof(WAVEINCAPS))) == 0 && !string.IsNullOrEmpty(caps.szPname))
                    name = caps.szPname;
                list.Add(name);
            }
            return list;
        }

        /// <summary>Starts listening. deviceIndex &lt; 0 = Windows default microphone.</summary>
        public void Start(int deviceIndex)
        {
            Stop();
            WAVEFORMATEX f = new WAVEFORMATEX();
            f.wFormatTag = 1; // PCM
            f.nChannels = 1;
            f.nSamplesPerSec = SampleRate;
            f.wBitsPerSample = 16;
            f.nBlockAlign = 2;
            f.nAvgBytesPerSec = SampleRate * 2;
            f.cbSize = 0;

            uint dev = deviceIndex < 0 ? WAVE_MAPPER : (uint)deviceIndex;
            IntPtr h;
            int r = waveInOpen(out h, dev, ref f, IntPtr.Zero, IntPtr.Zero, 0 /* CALLBACK_NULL */);
            if (r != 0)
                throw new InvalidOperationException("Could not open the microphone (error " + r + ").\n" +
                    "Check that a microphone is connected and that Windows Settings > Privacy & security > Microphone > " +
                    "\"Let desktop apps access your microphone\" is ON.");
            handle = h;

            for (int i = 0; i < BufferCount; i++)
            {
                buffers[i] = Marshal.AllocHGlobal(BufferBytes);
                headers[i] = Marshal.AllocHGlobal(HdrSize);
                WAVEHDR hdr = new WAVEHDR();
                hdr.lpData = buffers[i];
                hdr.dwBufferLength = BufferBytes;
                Marshal.StructureToPtr(hdr, headers[i], false);
                waveInPrepareHeader(handle, headers[i], (uint)HdrSize);
                waveInAddBuffer(handle, headers[i], (uint)HdrSize);
            }
            next = 0;
            waveInStart(handle);

            timer = new DispatcherTimer(DispatcherPriority.Normal);
            timer.Interval = TimeSpan.FromMilliseconds(15);
            timer.Tick += delegate { Poll(); };
            timer.Start();
        }

        void Poll()
        {
            if (handle == IntPtr.Zero) return;
            for (int guard = 0; guard < BufferCount; guard++)
            {
                IntPtr hp = headers[next];
                uint flags = (uint)Marshal.ReadInt32(hp, OffFlags);
                if ((flags & WHDR_DONE) == 0) break;

                int bytes = Marshal.ReadInt32(hp, OffRecorded);
                Analyse(buffers[next], bytes);

                Marshal.WriteInt32(hp, OffRecorded, 0);
                Marshal.WriteInt32(hp, OffFlags, (int)(flags & ~WHDR_DONE));
                waveInAddBuffer(handle, hp, (uint)HdrSize);
                next = (next + 1) % BufferCount;
            }
        }

        void Analyse(IntPtr data, int bytes)
        {
            int n = Math.Min(bytes / 2, samples.Length);
            if (n <= 0) return;
            Marshal.Copy(data, samples, 0, n);
            double mean = 0;
            for (int i = 0; i < n; i++) mean += samples[i];
            mean /= n;
            double sq = 0;
            for (int i = 0; i < n; i++) { double v = samples[i] - mean; sq += v * v; }
            double rms = Math.Sqrt(sq / n) / 32768.0;
            double db = 20 * Math.Log10(rms + 1e-9);
            if (db < -90) db = -90;

            if (db > ThresholdDb) { lastVoiceTick = Environment.TickCount; heardVoice = true; }
            // fast attack, slower release for a readable meter
            LevelDb = db > LevelDb ? db : LevelDb + (db - LevelDb) * 0.25;
        }

        public void Stop()
        {
            if (timer != null) { timer.Stop(); timer = null; }
            if (handle == IntPtr.Zero) return;
            waveInReset(handle);
            for (int i = 0; i < BufferCount; i++)
            {
                if (headers[i] != IntPtr.Zero)
                {
                    waveInUnprepareHeader(handle, headers[i], (uint)HdrSize);
                    Marshal.FreeHGlobal(headers[i]);
                    headers[i] = IntPtr.Zero;
                }
                if (buffers[i] != IntPtr.Zero) { Marshal.FreeHGlobal(buffers[i]); buffers[i] = IntPtr.Zero; }
            }
            waveInClose(handle);
            handle = IntPtr.Zero;
            LevelDb = -90;
            heardVoice = false;
        }

        public void Dispose() { Stop(); }
    }
}
