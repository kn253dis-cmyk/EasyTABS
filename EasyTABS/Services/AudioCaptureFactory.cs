using EasyTABS.Services;

namespace EasyTABS.Services
{
    /// <summary>
    /// Повертає реалізацію захоплення для поточної платформи.
    /// Використовує умовну компіляцію — на кожному таргеті лінкується
    /// тільки відповідний платформний файл.
    /// </summary>
    public static class AudioCaptureFactory
    {
        public static IAudioCapture Create()
        {
#if WINDOWS
            return new EasyTABS.Platforms.Windows.WindowsAudioCapture();
#elif ANDROID
            return new EasyTABS.Platforms.Android.AndroidAudioCapture();
#elif IOS
            return new EasyTABS.Platforms.Apple.AppleAudioCapture();
#else
            throw new System.PlatformNotSupportedException("Захоплення звуку не підтримується на цій платформі.");
#endif
        }
    }
}
