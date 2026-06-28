using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace ELOR.Laney.Core {
    public sealed class VoiceRecorder : IDisposable {
        private const int WaveMapper = -1;
        private const int WaveFormatPcm = 1;
        private const int CallbackFunction = 0x00030000;
        private const int WimData = 0x3C0;
        private const int SampleRate = 16000;
        private const short Channels = 1;
        private const short BitsPerSample = 16;
        private const int BufferSize = 4096;
        private const int BufferCount = 4;

        private readonly object syncRoot = new object();
        private readonly string outputPath;
        private readonly List<RecordingBuffer> buffers = new List<RecordingBuffer>();
        private IntPtr handle;
        private FileStream stream;
        private WaveInProc callback;
        private bool isRecording;
        private int dataLength;

        public static bool IsSupported => OperatingSystem.IsWindows();

        public string OutputPath => outputPath;

        public VoiceRecorder(string outputPath) {
            if (String.IsNullOrWhiteSpace(outputPath)) throw new ArgumentNullException(nameof(outputPath));
            this.outputPath = outputPath;
        }

        public void Start() {
            if (!IsSupported) throw new PlatformNotSupportedException("Voice recording is currently available only on Windows.");
            if (isRecording) return;

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            stream = new FileStream(outputPath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
            WriteWaveHeader(stream, 0);

            WaveFormatEx format = new WaveFormatEx {
                FormatTag = WaveFormatPcm,
                Channels = Channels,
                SamplesPerSec = SampleRate,
                AvgBytesPerSec = SampleRate * Channels * BitsPerSample / 8,
                BlockAlign = (short)(Channels * BitsPerSample / 8),
                BitsPerSample = BitsPerSample,
                Size = 0
            };

            callback = WaveInCallback;
            CheckResult(waveInOpen(out handle, WaveMapper, ref format, callback, IntPtr.Zero, CallbackFunction), "waveInOpen");

            for (int i = 0; i < BufferCount; i++) {
                RecordingBuffer buffer = new RecordingBuffer(BufferSize);
                buffers.Add(buffer);
                CheckResult(waveInPrepareHeader(handle, buffer.HeaderPointer, Marshal.SizeOf<WaveHeader>()), "waveInPrepareHeader");
                CheckResult(waveInAddBuffer(handle, buffer.HeaderPointer, Marshal.SizeOf<WaveHeader>()), "waveInAddBuffer");
            }

            isRecording = true;
            CheckResult(waveInStart(handle), "waveInStart");
        }

        public void Stop() {
            if (!isRecording && handle == IntPtr.Zero) return;

            isRecording = false;

            if (handle != IntPtr.Zero) {
                waveInStop(handle);
                waveInReset(handle);
            }

            foreach (RecordingBuffer buffer in buffers) {
                if (handle != IntPtr.Zero) waveInUnprepareHeader(handle, buffer.HeaderPointer, Marshal.SizeOf<WaveHeader>());
                buffer.Dispose();
            }
            buffers.Clear();

            if (handle != IntPtr.Zero) {
                waveInClose(handle);
                handle = IntPtr.Zero;
            }

            lock (syncRoot) {
                if (stream != null) {
                    stream.Position = 0;
                    WriteWaveHeader(stream, dataLength);
                    stream.Flush();
                    stream.Dispose();
                    stream = null;
                }
            }

            Log.Information("Voice recording saved. Path={Path}; bytes={Bytes}", outputPath, dataLength);
        }

        public void Dispose() {
            Stop();
        }

        private void WaveInCallback(IntPtr waveInHandle, int message, IntPtr instance, IntPtr parameter1, IntPtr parameter2) {
            if (message != WimData || parameter1 == IntPtr.Zero) return;

            try {
                WaveHeader header = Marshal.PtrToStructure<WaveHeader>(parameter1);
                if (header.BytesRecorded > 0) {
                    byte[] data = new byte[header.BytesRecorded];
                    Marshal.Copy(header.Data, data, 0, data.Length);

                    lock (syncRoot) {
                        if (stream != null) {
                            stream.Write(data, 0, data.Length);
                            dataLength += data.Length;
                        }
                    }
                }

                if (isRecording && handle != IntPtr.Zero) {
                    header.BytesRecorded = 0;
                    Marshal.StructureToPtr(header, parameter1, false);
                    waveInAddBuffer(handle, parameter1, Marshal.SizeOf<WaveHeader>());
                }
            } catch (Exception ex) {
                Log.Warning(ex, "Voice recording callback failed.");
            }
        }

        private static void WriteWaveHeader(Stream target, int dataLength) {
            using BinaryWriter writer = new BinaryWriter(target, System.Text.Encoding.ASCII, true);
            short blockAlign = (short)(Channels * BitsPerSample / 8);
            int byteRate = SampleRate * blockAlign;

            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataLength);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)WaveFormatPcm);
            writer.Write(Channels);
            writer.Write(SampleRate);
            writer.Write(byteRate);
            writer.Write(blockAlign);
            writer.Write(BitsPerSample);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            writer.Write(dataLength);
        }

        private static void CheckResult(int result, string operation) {
            if (result == 0) return;
            throw new InvalidOperationException($"{operation} failed with winmm code {result}.");
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WaveFormatEx {
            public short FormatTag;
            public short Channels;
            public int SamplesPerSec;
            public int AvgBytesPerSec;
            public short BlockAlign;
            public short BitsPerSample;
            public short Size;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WaveHeader {
            public IntPtr Data;
            public int BufferLength;
            public int BytesRecorded;
            public IntPtr User;
            public int Flags;
            public int Loops;
            public IntPtr Next;
            public IntPtr Reserved;
        }

        private sealed class RecordingBuffer : IDisposable {
            private readonly GCHandle dataHandle;
            public IntPtr HeaderPointer { get; }

            public RecordingBuffer(int size) {
                byte[] data = new byte[size];
                dataHandle = GCHandle.Alloc(data, GCHandleType.Pinned);

                WaveHeader header = new WaveHeader {
                    Data = dataHandle.AddrOfPinnedObject(),
                    BufferLength = size
                };

                HeaderPointer = Marshal.AllocHGlobal(Marshal.SizeOf<WaveHeader>());
                Marshal.StructureToPtr(header, HeaderPointer, false);
            }

            public void Dispose() {
                if (HeaderPointer != IntPtr.Zero) Marshal.FreeHGlobal(HeaderPointer);
                if (dataHandle.IsAllocated) dataHandle.Free();
            }
        }

        private delegate void WaveInProc(IntPtr waveInHandle, int message, IntPtr instance, IntPtr parameter1, IntPtr parameter2);

        [DllImport("winmm.dll")]
        private static extern int waveInOpen(out IntPtr waveInHandle, int deviceId, ref WaveFormatEx format, WaveInProc callback, IntPtr instance, int flags);

        [DllImport("winmm.dll")]
        private static extern int waveInPrepareHeader(IntPtr waveInHandle, IntPtr waveHeader, int size);

        [DllImport("winmm.dll")]
        private static extern int waveInUnprepareHeader(IntPtr waveInHandle, IntPtr waveHeader, int size);

        [DllImport("winmm.dll")]
        private static extern int waveInAddBuffer(IntPtr waveInHandle, IntPtr waveHeader, int size);

        [DllImport("winmm.dll")]
        private static extern int waveInStart(IntPtr waveInHandle);

        [DllImport("winmm.dll")]
        private static extern int waveInStop(IntPtr waveInHandle);

        [DllImport("winmm.dll")]
        private static extern int waveInReset(IntPtr waveInHandle);

        [DllImport("winmm.dll")]
        private static extern int waveInClose(IntPtr waveInHandle);
    }
}
