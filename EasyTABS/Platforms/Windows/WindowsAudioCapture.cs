using System;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using EasyTABS.Services;

namespace EasyTABS.Platforms.Windows
{
    /// <summary>
    /// Windows-реалізація захоплення через WASAPI (NAudio).
    /// Це по суті твій початковий код із ChordDetectorService,
    /// але тепер він лише захоплює семпли, а аналіз винесено в AudioAnalyzer.
    /// </summary>
    public class WindowsAudioCapture : IAudioCapture
    {
        private WasapiCapture? _capture;

        public int SampleRate { get; private set; }
        public bool IsRunning => _capture != null;
        public event Action<float[], int>? SamplesAvailable;

        public void Start()
        {
            _capture = new WasapiCapture();

            if (_capture.WaveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
                throw new NotSupportedException("WASAPI capture must be in IEEE Float format.");

            SampleRate = _capture.WaveFormat.SampleRate;

            // Діагностика: який пристрій і формат реально захоплюється.
            try
            {
                using var en = new MMDeviceEnumerator();
                var dev = en.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Console);
                System.Diagnostics.Debug.WriteLine(
                    $"[Capture] Device: {dev.FriendlyName}, " +
                    $"{_capture.WaveFormat.SampleRate} Hz, " +
                    $"{_capture.WaveFormat.Channels} ch, " +
                    $"{_capture.WaveFormat.BitsPerSample}-bit {_capture.WaveFormat.Encoding}");
            }
            catch { /* діагностика не критична */ }

            _capture.DataAvailable += OnDataAvailable;
            _capture.StartRecording();
        }

        public void Stop()
        {
            if (_capture == null) return;
            _capture.StopRecording();
            _capture.DataAvailable -= OnDataAvailable;
            _capture.Dispose();
            _capture = null;
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (_capture == null) return;

            int bytesPerSample = _capture.WaveFormat.BitsPerSample / 8;
            int channels = _capture.WaveFormat.Channels;
            int frameBytes = bytesPerSample * channels;

            // Беремо лише перший канал (моно-обробка).
            int sampleCount = e.BytesRecorded / frameBytes;
            var mono = new float[sampleCount];
            int written = 0;

            for (int i = 0; i + 4 <= e.BytesRecorded; i += frameBytes)
            {
                mono[written++] = BitConverter.ToSingle(e.Buffer, i);
            }

            SamplesAvailable?.Invoke(mono, written);
        }
    }
}
