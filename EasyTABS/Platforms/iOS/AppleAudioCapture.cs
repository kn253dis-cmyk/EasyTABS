using System;
using AVFoundation;
using EasyTABS.Services;

namespace EasyTABS.Platforms.Apple
{
    /// <summary>
    /// iOS / Mac Catalyst реалізація через AVAudioEngine.
    /// Ставить tap на вхідний вузол і віддає float-семпли (моно).
    /// На iOS потрібен ключ NSMicrophoneUsageDescription у Info.plist
    /// та дозвіл у рантаймі. На Mac Catalyst — com.apple.security.device.audio-input
    /// в Entitlements.plist.
    /// </summary>
    public class AppleAudioCapture : IAudioCapture
    {
        public static IAudioCapture Create()
        {
#if IOS || MACCATALYST
            return new EasyTABS.Platforms.Apple.AppleAudioCapture();
#elif ANDROID
    return new EasyTABS.Platforms.Android.AndroidAudioCapture();
#elif WINDOWS
    return new EasyTABS.Platforms.Windows.WindowsAudioCapture();
#else
    throw new PlatformNotSupportedException("Аудіозахоплення не підтримується на цій платформі.");
#endif
        }
        private AVAudioEngine? _engine;

        public int SampleRate { get; private set; } = 44100;
        public bool IsRunning => _engine != null;
        public event Action<float[], int>? SamplesAvailable;

        public void Start()
        {
            if (_engine != null) return;

#if IOS
            var session = AVAudioSession.SharedInstance();
            session.SetCategory(AVAudioSessionCategory.Record);
            session.SetActive(true, out _);
#endif

            _engine = new AVAudioEngine();
            var input = _engine.InputNode;
            var format = input.GetBusOutputFormat(0);
            SampleRate = (int)format.SampleRate;

            input.InstallTapOnBus(0, 4096, format, (buffer, when) =>
            {
                unsafe
                {
                    // FloatChannelData[0] — перший канал.
                    var channel = buffer.FloatChannelData;
                    if (channel == IntPtr.Zero) return;

                    int frames = (int)buffer.FrameLength;
                    float* ptr = ((float**)channel)[0];
                    var managed = new float[frames];
                    for (int i = 0; i < frames; i++)
                        managed[i] = ptr[i];

                    SamplesAvailable?.Invoke(managed, frames);
                }
            });

            _engine.Prepare();
            _engine.StartAndReturnError(out _);
        }

        public void Stop()
        {
            if (_engine == null) return;
            _engine.InputNode.RemoveTapOnBus(0);
            _engine.Stop();
            _engine.Dispose();
            _engine = null;
        }
    }
}
