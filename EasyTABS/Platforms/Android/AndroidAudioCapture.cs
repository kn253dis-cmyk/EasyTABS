using System;
using System.Threading;
using Android.Media;
using EasyTABS.Services;

namespace EasyTABS.Platforms.Android
{
    /// <summary>
    /// Android-реалізація через нативний AudioRecord.
    /// Потрібен дозвіл RECORD_AUDIO (запит у рантаймі — див. нижче).
    /// Зчитує 16-біт PCM і конвертує у float [-1..1].
    /// </summary>
    public class AndroidAudioCapture : IAudioCapture
    {
        private AudioRecord? _record;
        private Thread? _thread;
        private volatile bool _running;

        public int SampleRate { get; private set; } = 44100;
        public bool IsRunning => _running;
        public event Action<float[], int>? SamplesAvailable;

        public void Start()
        {
            if (_running) return;

            int minBuf = AudioRecord.GetMinBufferSize(
                SampleRate, ChannelIn.Mono, Encoding.Pcm16bit);
            int bufSize = Math.Max(minBuf, 4096 * 2);

            _record = new AudioRecord(
                AudioSource.Mic,
                SampleRate,
                ChannelIn.Mono,
                Encoding.Pcm16bit,
                bufSize);

            if (_record.State != State.Initialized)
                throw new InvalidOperationException("AudioRecord не ініціалізувався. Перевір дозвіл RECORD_AUDIO.");

            _record.StartRecording();
            _running = true;

            _thread = new Thread(() => ReadLoop(bufSize)) { IsBackground = true };
            _thread.Start();
        }

        private void ReadLoop(int bufSize)
        {
            var shortBuf = new short[bufSize / 2];
            var floatBuf = new float[shortBuf.Length];

            while (_running && _record != null)
            {
                int read = _record.Read(shortBuf, 0, shortBuf.Length);
                if (read > 0)
                {
                    for (int i = 0; i < read; i++)
                        floatBuf[i] = shortBuf[i] / 32768f;
                    SamplesAvailable?.Invoke(floatBuf, read);
                }
            }
        }

        public void Stop()
        {
            _running = false;
            _thread?.Join(500);
            _thread = null;

            if (_record != null)
            {
                try { _record.Stop(); } catch { }
                _record.Release();
                _record.Dispose();
                _record = null;
            }
        }
    }
}
