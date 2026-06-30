using NAudio.CoreAudioApi;
using NAudio.Dsp;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
//using Xamarin.Google.Crypto.Tink.Shaded.Protobuf;

namespace EasyTABS.Models
{
    public class ChordDetectorService
    {
        // Налаштування FFT (4096 семплів дають високу роздільну здатність за частотою)
        private const int FftSize = 4096;
        private const int M = 12; 
        private const int HopSize = 2048; 

        private WasapiCapture _capture;
        private readonly List<float> _sampleBuffer = new List<float>();
        private readonly object _lockObject = new object();
        private Dictionary<string, double[]> _chordTemplates;

        // Подія, яка спрацьовує при розпізнаванні акорду
        public event Action<string, double> ChordDetected;

        public ChordDetectorService()
        {
            InitializeChordTemplates();
        }

        public void Start()
        {
            // Використовуємо WasapiCapture для захоплення звуку з дефолтного мікрофона/входу
            _capture = new WasapiCapture();

            // Перевіряємо, що формат підтримує Float (IEEE Float є стандартом для WASAPI)
            if (_capture.WaveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
            {
                throw new NotSupportedException("WASAPI capture must be in IEEE Float format.");
            }

            _capture.DataAvailable += OnDataAvailable;
            _capture.StartRecording();
        }

        public void Stop()
        {
            if (_capture != null)
            {
                _capture.StopRecording();
                _capture.DataAvailable -= OnDataAvailable;
                _capture.Dispose();
                _capture = null;
            }
        }

        private void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            int bytesPerSample = _capture.WaveFormat.BitsPerSample / 8;
            int channels = _capture.WaveFormat.Channels;

            lock (_lockObject)
            {
                // Конвертуємо байти у float семпли (читаємо лише перший канал для Mono обробки)
                for (int i = 0; i < e.BytesRecorded; i += bytesPerSample * channels)
                {
                    if (i + 4 <= e.BytesRecorded)
                    {
                        float sample = BitConverter.ToSingle(e.Buffer, i);
                        _sampleBuffer.Add(sample);
                    }
                }

                // Якщо накопичилось достатньо семплів для FFT
                while (_sampleBuffer.Count >= FftSize)
                {
                    float[] frame = _sampleBuffer.Take(FftSize).ToArray();
                    _sampleBuffer.RemoveRange(0, HopSize);
                    System.Threading.Tasks.Task.Run(() => ProcessAudioFrame(frame, _capture.WaveFormat.SampleRate));
                }
            }
        }

        private void ProcessAudioFrame(float[] frame, int sampleRate)
        {
            // 1. Вікноутворення (Hann Window) та підготовка буфера для NAudio FFT
            Complex[] fftBuffer = new Complex[FftSize];
            for (int n = 0; n < FftSize; n++)
            {
                double window = 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * n / (FftSize - 1)));
                fftBuffer[n].X = (float)(frame[n] * window); // Реальна частина
                fftBuffer[n].Y = 0;                          // Уявна частина
            }

            // 2. Виконання Швидкого Перетворення Фур'є (FFT)
            // true означає Forward FFT
            FastFourierTransform.FFT(true, M, fftBuffer);

            // 3. Обчислення спектру потужності (амплітуд)
            int halfSize = FftSize / 2;
            double[] magnitudes = new double[halfSize];
            for (int i = 0; i < halfSize; i++)
                magnitudes[i] = Math.Sqrt(fftBuffer[i].X * fftBuffer[i].X + fftBuffer[i].Y * fftBuffer[i].Y);

            // 4. Побудова Хромаграми (12 нотних кошиків)
            double[] chromagram = new double[12];
            double binWidth = (double)sampleRate / FftSize;

            for (int i = 1; i < halfSize; i++)
            {
                double freq = i * binWidth;

                // Фільтруємо частоти за межами корисного діапазону акордів (приблизно E2 - C6)
                if (freq < 50 || freq > 1350) continue;

                // Перевід частоти в MIDI-ноту: n = 12 * log2(f / 440) + 69
                double midiNote = 12.0 * Math.Log(freq / 440.0, 2.0) + 69.0;

                // Отримуємо індекс ноти від 0 до 11 (0 = C, 1 = C#, ..., 9 = A, 11 = B)
                int noteIndex = ((int)Math.Round(midiNote) % 12 + 12) % 12;

                chromagram[noteIndex] += magnitudes[i];
            }

            // Нормалізація хромаграми (масштабуємо від 0 до 1)
            double maxChroma = chromagram.Max();
            if (maxChroma > 0)
            {
                for (int i = 0; i < 12; i++) chromagram[i] /= maxChroma;
            }

            // 5. Класифікація (Пошук найкращого збігу через Косинусну подібність)
            string bestChord = "Unknown";
            double maxSimilarity = 0.65; // Поріг чутливості

            foreach (var template in _chordTemplates)
            {
                double similarity = CalculateCosineSimilarity(chromagram, template.Value);
                if (similarity > maxSimilarity)
                {
                    maxSimilarity = similarity;
                    bestChord = template.Key;
                }
            }

            if (bestChord != "Unknown")
            {
                // Викликаємо подію у фоновому потоці
                ChordDetected?.Invoke(bestChord, maxSimilarity);
            }
        }

        private double CalculateCosineSimilarity(double[] vecA, double[] vecB)
        {
            double dotProduct = 0;
            double normA = 0;
            double normB = 0;

            for (int i = 0; i < 12; i++)
            {
                dotProduct += vecA[i] * vecB[i];
                normA += vecA[i] * vecA[i];
                normB += vecB[i] * vecB[i];
            }

            if (normA == 0 || normB == 0) return 0;
            return dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));
        }

        private void InitializeChordTemplates()
        {
            _chordTemplates = new Dictionary<string, double[]>();

            // Мапування індексів: 0=C, 1=C#, 2=D, 3=D#, 4=E, 5=F, 6=F#, 7=G, 8=G#, 9=A, 10=A#, 11=B
            // Мажорні акорди (Тоніка, Велика терція [+4], Квінта [+7])
            _chordTemplates["C Major"] = CreateTemplate(0, 4, 7);
            _chordTemplates["D Major"] = CreateTemplate(2, 6, 9);
            _chordTemplates["E Major"] = CreateTemplate(4, 8, 11);
            _chordTemplates["F Major"] = CreateTemplate(5, 9, 0);
            _chordTemplates["G Major"] = CreateTemplate(7, 11, 2);
            _chordTemplates["A Major"] = CreateTemplate(9, 1, 4);
            _chordTemplates["B Major"] = CreateTemplate(11, 3, 6);

            // Мінорні акорди (Тоніка, Мала терція [+3], Квінта [+7])
            _chordTemplates["C Minor"] = CreateTemplate(0, 3, 7);
            _chordTemplates["D Minor"] = CreateTemplate(2, 5, 9);
            _chordTemplates["E Minor"] = CreateTemplate(4, 7, 11);
            _chordTemplates["F Minor"] = CreateTemplate(5, 8, 0);
            _chordTemplates["G Minor"] = CreateTemplate(7, 10, 2);
            _chordTemplates["A Minor"] = CreateTemplate(9, 0, 4);
            _chordTemplates["B Minor"] = CreateTemplate(11, 2, 5);
        }

        private double[] CreateTemplate(int root, int third, int fifth)
        {
            double[] template = new double[12];
            template[root] = 1.0;
            template[third] = 1.0;
            template[fifth] = 1.0;
            return template;
        }
    }
}