using System;

namespace EasyTABS.Services
{
    /// <summary>
    /// Платформо-незалежний інтерфейс захоплення звуку з мікрофона/входу.
    /// Кожна платформа дає свою реалізацію (Windows = WASAPI/NAudio,
    /// Android = AudioRecord, iOS/Mac = AVAudioEngine).
    /// Семпли віддаються у форматі float [-1..1], моно.
    /// </summary>
    public interface IAudioCapture
    {
        /// <summary>Частота дискретизації активного потоку (Гц). Дійсна після Start().</summary>
        int SampleRate { get; }

        bool IsRunning { get; }

        /// <summary>
        /// Спрацьовує з фонового потоку. Перший аргумент — буфер float-семплів (моно),
        /// другий — кількість валідних семплів у буфері.
        /// </summary>
        event Action<float[], int>? SamplesAvailable;

        void Start();
        void Stop();
    }
}
