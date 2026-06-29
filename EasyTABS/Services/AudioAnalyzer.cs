using System;
using System.Collections.Generic;
using System.Linq;

namespace EasyTABS.Services
{
    public enum TunerMode
    {
        Note,   // режим тюнера — одна нота + точність строю
        Chord   // режим акордів — хромаграма + акорд
    }

    /// <summary>
    /// Результат аналізу одного аудіокадру.
    /// </summary>
    public class AudioAnalysisResult
    {
        public string Chord { get; init; } = "—";
        public double ChordConfidence { get; init; }

        public string Note { get; init; } = "--";
        public double Frequency { get; init; }   // Гц, домінантна нота
        public double Cents { get; init; }        // відхилення від ладу (-50..+50)

        public double[] Chromagram { get; init; } = new double[12];

        // Гучність кадру (RMS). Нижче порогу = тиша, кадр ігнорується.
        public double Rms { get; init; }
        // true, якщо кадр досить гучний, щоб йому довіряти.
        public bool IsVoiced { get; init; }
    }

    /// <summary>
    /// Чистий C#-аналізатор: FFT, хромаграма, детекція акорду та домінантної ноти.
    /// Додано RMS-гейт та high-pass (відсіювання частот нижче CutoffHz).
    /// </summary>
    public class AudioAnalyzer
    {
        public int FftSize { get; }
        private readonly int _log2;
        private readonly double[] _hann;
        private readonly Dictionary<string, double[]> _chordTemplates;

        private static readonly string[] NoteNames =
            { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

        // Поріг подібності для класифікації акорду (налаштовується з UI).
        // Реальний сигнал гітари рідко дає 0.65 — стартуємо нижче.
        public double ChordThreshold { get; set; } = 0.5;
        // Нижня межа корисних частот (твій запит — відсіювати все нижче 40 Гц).
        public double CutoffHz { get; set; } = 40.0;
        private const double MaxNoteFreq = 1350.0;

        // Поріг гучності: кадри тихіші за це вважаємо тишею.
        // Підбирається під мікрофон; 0.01 — розумний старт.
        public double RmsGate { get; set; } = 0.01;

        public AudioAnalyzer(int fftSize = 4096)
        {
            if ((fftSize & (fftSize - 1)) != 0)
                throw new ArgumentException("fftSize має бути степенем двійки", nameof(fftSize));

            FftSize = fftSize;
            _log2 = (int)Math.Log2(fftSize);

            _hann = new double[fftSize];
            for (int n = 0; n < fftSize; n++)
                _hann[n] = 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * n / (fftSize - 1)));

            _chordTemplates = BuildChordTemplates();
        }

        public AudioAnalysisResult Analyze(float[] frame, int sampleRate, TunerMode mode)
        {
            int n = FftSize;

            // --- RMS (гучність) для гейту ---
            double sumSq = 0;
            for (int i = 0; i < n; i++) sumSq += frame[i] * (double)frame[i];
            double rms = Math.Sqrt(sumSq / n);

            // Якщо тихо — повертаємо «беззвучний» результат, нічого не рахуючи.
            if (rms < RmsGate)
            {
                return new AudioAnalysisResult
                {
                    Rms = rms,
                    IsVoiced = false,
                    Chromagram = new double[12]
                };
            }

            double[] re = new double[n];
            double[] im = new double[n];
            for (int i = 0; i < n; i++)
            {
                re[i] = frame[i] * _hann[i];
                im[i] = 0.0;
            }

            Fft(re, im);

            int half = n / 2;
            double[] mag = new double[half];
            for (int i = 0; i < half; i++)
                mag[i] = Math.Sqrt(re[i] * re[i] + im[i] * im[i]);

            double binWidth = (double)sampleRate / n;

            // High-pass: обнуляємо біни нижче CutoffHz.
            int cutoffBin = (int)Math.Ceiling(CutoffHz / binWidth);
            for (int i = 0; i < cutoffBin && i < half; i++)
                mag[i] = 0;

            // --- Хромаграма ---
            double[] chroma = new double[12];
            for (int i = 1; i < half; i++)
            {
                double freq = i * binWidth;
                if (freq < CutoffHz || freq > MaxNoteFreq) continue;

                double midi = 12.0 * Math.Log(freq / 440.0, 2.0) + 69.0;
                int idx = ((int)Math.Round(midi) % 12 + 12) % 12;
                chroma[idx] += mag[i];
            }
            double maxChroma = chroma.Max();
            if (maxChroma > 0)
                for (int i = 0; i < 12; i++) chroma[i] /= maxChroma;

            // --- Акорд (рахуємо лише в режимі акордів) ---
            string bestChord = "—";
            double bestSim = ChordThreshold;
            if (mode == TunerMode.Chord)
            {
                foreach (var t in _chordTemplates)
                {
                    double sim = CosineSimilarity(chroma, t.Value);
                    if (sim > bestSim) { bestSim = sim; bestChord = t.Key; }
                }
            }

            // --- Домінантна нота (автокореляція по часовому сигналу) ---
            // Стійкіша за пошук піку, коли гармоніки гучніші за основний тон.
            (string note, double freqHz, double cents) = FindPitchAutocorr(frame, sampleRate);

            return new AudioAnalysisResult
            {
                Chord = bestChord,
                ChordConfidence = bestChord == "—" ? 0 : bestSim,
                Note = note,
                Frequency = freqHz,
                Cents = cents,
                Chromagram = chroma,
                Rms = rms,
                IsVoiced = note != "--" || bestChord != "—"
            };
        }

        // Визначення основної частоти автокореляцією.
        // Діапазон 70..1000 Гц покриває гітару (E2..~B5).
        private (string note, double freq, double cents) FindPitchAutocorr(
            float[] x, int sampleRate, double fmin = 70, double fmax = 1000)
        {
            int n = x.Length;

            // Прибираємо постійну складову.
            double mean = 0;
            for (int i = 0; i < n; i++) mean += x[i];
            mean /= n;

            int lagMin = (int)(sampleRate / fmax);
            int lagMax = Math.Min(n - 1, (int)(sampleRate / fmin));
            if (lagMax <= lagMin) return ("--", 0, 0);

            // Автокореляція лише в потрібному діапазоні лагів.
            double bestCorr = 0;
            int bestLag = -1;
            double[] corr = new double[lagMax + 2];

            for (int lag = lagMin; lag <= lagMax; lag++)
            {
                double sum = 0;
                for (int i = 0; i < n - lag; i++)
                    sum += (x[i] - mean) * (x[i + lag] - mean);
                corr[lag] = sum;
                if (sum > bestCorr) { bestCorr = sum; bestLag = lag; }
            }

            if (bestLag <= 0 || bestCorr <= 0) return ("--", 0, 0);

            // Параболічна інтерполяція по лагу для точності.
            double interpLag = bestLag;
            if (bestLag > lagMin && bestLag < lagMax)
            {
                double a = corr[bestLag - 1], b = corr[bestLag], c = corr[bestLag + 1];
                double d = a - 2 * b + c;
                if (d != 0) interpLag = bestLag + 0.5 * (a - c) / d;
            }

            double freq = sampleRate / interpLag;
            if (freq < CutoffHz || double.IsNaN(freq) || double.IsInfinity(freq))
                return ("--", 0, 0);

            double midi = 12.0 * Math.Log(freq / 440.0, 2.0) + 69.0;
            int nearest = (int)Math.Round(midi);
            double cents = (midi - nearest) * 100.0;
            string name = NoteNames[((nearest % 12) + 12) % 12];

            return (name, freq, cents);
        }

        private static double CosineSimilarity(double[] a, double[] b)
        {
            double dot = 0, na = 0, nb = 0;
            for (int i = 0; i < 12; i++)
            {
                dot += a[i] * b[i];
                na += a[i] * a[i];
                nb += b[i] * b[i];
            }
            if (na == 0 || nb == 0) return 0;
            return dot / (Math.Sqrt(na) * Math.Sqrt(nb));
        }

        private static Dictionary<string, double[]> BuildChordTemplates()
        {
            var d = new Dictionary<string, double[]>();
            double[] T(int r, int t, int f)
            {
                var v = new double[12];
                v[r] = 1; v[t] = 1; v[f] = 1;
                return v;
            }
            d["C Major"] = T(0, 4, 7);
            d["D Major"] = T(2, 6, 9);
            d["E Major"] = T(4, 8, 11);
            d["F Major"] = T(5, 9, 0);
            d["G Major"] = T(7, 11, 2);
            d["A Major"] = T(9, 1, 4);
            d["B Major"] = T(11, 3, 6);
            d["C Minor"] = T(0, 3, 7);
            d["D Minor"] = T(2, 5, 9);
            d["E Minor"] = T(4, 7, 11);
            d["F Minor"] = T(5, 8, 0);
            d["G Minor"] = T(7, 10, 2);
            d["A Minor"] = T(9, 0, 4);
            d["B Minor"] = T(11, 2, 5);
            return d;
        }

        private void Fft(double[] re, double[] im)
        {
            int n = re.Length;
            for (int i = 1, j = 0; i < n; i++)
            {
                int bit = n >> 1;
                for (; (j & bit) != 0; bit >>= 1) j ^= bit;
                j ^= bit;
                if (i < j)
                {
                    (re[i], re[j]) = (re[j], re[i]);
                    (im[i], im[j]) = (im[j], im[i]);
                }
            }
            for (int len = 2; len <= n; len <<= 1)
            {
                double ang = -2.0 * Math.PI / len;
                double wRe = Math.Cos(ang), wIm = Math.Sin(ang);
                for (int i = 0; i < n; i += len)
                {
                    double curRe = 1.0, curIm = 0.0;
                    for (int k = 0; k < len / 2; k++)
                    {
                        int a = i + k, b = i + k + len / 2;
                        double tRe = re[b] * curRe - im[b] * curIm;
                        double tIm = re[b] * curIm + im[b] * curRe;
                        re[b] = re[a] - tRe; im[b] = im[a] - tIm;
                        re[a] += tRe; im[a] += tIm;
                        double nRe = curRe * wRe - curIm * wIm;
                        curIm = curRe * wIm + curIm * wRe;
                        curRe = nRe;
                    }
                }
            }
        }
    }
}
