using Microsoft.Maui.Graphics;
using EasyTABS.ViewModels;
using MauiIcons.Material;

namespace EasyTABS.Views
{
    public partial class TunerPage : ContentPage
    {
        private TunerDrawable? _drawable;
        private TunerViewModel? _vm;

        public TunerPage()
        {
            InitializeComponent();
            _ = new MauiIcons.Core.MauiIcon(); // обхід бага MAUI з URL-namespace

            _drawable = (TunerDrawable)Resources["TunerDrawable"];
            _vm = BindingContext as TunerViewModel;

            if (_vm != null)
                _vm.SpectrumUpdated += OnSpectrumUpdated;
        }

        private void OnSpectrumUpdated()
        {
            if (_vm == null || _drawable == null) return;
            _drawable.Chromagram = _vm.Chromagram;
            _drawable.Cents = _vm.Cents;
            _drawable.IsChordMode = _vm.IsChordMode;
            _drawable.IsLocked = _vm.IsLocked;
            _drawable.HasSignal = _vm.IsListening && (_vm.NoteText != "--" || _vm.ChordText != "—");
            GraphView.Invalidate();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _vm?.StopCommand.Execute(null);
        }
    }

    /// <summary>
    /// Малює:
    ///  - у режимі тюнера: горизонтальну шкалу центів зі стрілкою + хромаграму знизу;
    ///  - у режимі акордів: великі стовпчики хромаграми (12 нот).
    /// </summary>
    public class TunerDrawable : IDrawable
    {
        public double[] Chromagram { get; set; } = new double[12];
        public double Cents { get; set; }
        public bool IsChordMode { get; set; }
        public bool IsLocked { get; set; }
        public bool HasSignal { get; set; }

        private static readonly string[] Labels =
            { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

        private static readonly Color Green = Color.FromArgb("#39FF14");
        private static readonly Color Red = Color.FromArgb("#FF4D4D");
        private static readonly Color Purple = Color.FromArgb("#8A2BE2");
        private static readonly Color Grey = Color.FromArgb("#3A3A3A");
        private static readonly Color LabelGrey = Color.FromArgb("#B0B0B0");

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            float w = dirtyRect.Width;
            float h = dirtyRect.Height;

            if (IsChordMode)
            {
                DrawChromagram(canvas, 0, 0, w, h, highlightChord: true);
            }
            else
            {
                // Верхні 60% — шкала центів, нижні 40% — хромаграма.
                float scaleH = h * 0.6f;
                DrawCentsScale(canvas, 0, 0, w, scaleH);
                DrawChromagram(canvas, 0, scaleH, w, h - scaleH, highlightChord: false);
            }
        }

        // --- Шкала точності строю (центи) ---
        private void DrawCentsScale(ICanvas canvas, float x, float y, float w, float h)
        {
            float cx = x + w / 2f;
            float midY = y + h / 2f;

            // Базова лінія.
            canvas.StrokeColor = Grey;
            canvas.StrokeSize = 2;
            canvas.DrawLine(x + 20, midY, x + w - 20, midY);

            // Поділки -50..+50 центів через кожні 10.
            float usable = w - 40;
            for (int c = -50; c <= 50; c += 10)
            {
                float px = cx + (c / 50f) * (usable / 2f);
                bool center = c == 0;
                canvas.StrokeColor = center ? Green : Grey;
                canvas.StrokeSize = center ? 3 : 1;
                float tick = center ? 22 : 12;
                canvas.DrawLine(px, midY - tick, px, midY + tick);
            }

            // Центрова зона «в строю» (±5 центів).
            canvas.FontColor = LabelGrey;
            canvas.FontSize = 11;
            canvas.DrawString("0", cx, midY + 26, HorizontalAlignment.Center);
            canvas.DrawString("-50", x + 20, midY + 26, HorizontalAlignment.Center);
            canvas.DrawString("+50", x + w - 20, midY + 26, HorizontalAlignment.Center);

            if (!HasSignal) return;

            // Стрілка поточного відхилення.
            double cents = Math.Clamp(Cents, -50, 50);
            float ax = cx + (float)(cents / 50.0) * (usable / 2f);

            bool inTune = Math.Abs(cents) <= 5;
            Color arrowColor = inTune ? Green : Red;

            canvas.FillColor = arrowColor;
            // Трикутник-вказівник зверху.
            var path = new PathF();
            path.MoveTo(ax, midY - 30);
            path.LineTo(ax - 9, midY - 48);
            path.LineTo(ax + 9, midY - 48);
            path.Close();
            canvas.FillPath(path);

            // Вертикальна лінія від стрілки до базової.
            canvas.StrokeColor = arrowColor;
            canvas.StrokeSize = 3;
            canvas.DrawLine(ax, midY - 30, ax, midY + 4);

            // Підпис «в строю».
            if (inTune)
            {
                canvas.FontColor = Green;
                canvas.FontSize = 14;
                canvas.DrawString("В строю", cx, y + 14, HorizontalAlignment.Center);
            }
        }

        // --- Хромаграма (12 стовпчиків) ---
        private void DrawChromagram(ICanvas canvas, float x, float y, float w, float h, bool highlightChord)
        {
            float bottomPad = 22f;
            int n = 12;
            float slot = w / n;
            float barW = slot * 0.6f;

            // Який стовпчик найгучніший (для підсвітки).
            int maxIdx = -1;
            double maxVal = 0;
            for (int i = 0; i < n; i++)
                if (Chromagram[i] > maxVal) { maxVal = Chromagram[i]; maxIdx = i; }

            for (int i = 0; i < n; i++)
            {
                double val = Math.Clamp(Chromagram[i], 0, 1);
                float barH = (float)(val * (h - bottomPad - 8));
                float bx = x + i * slot + (slot - barW) / 2f;
                float by = y + h - bottomPad - barH;

                bool isPeak = highlightChord && i == maxIdx && maxVal > 0.4;
                canvas.FillColor = isPeak ? Purple : Green;
                canvas.Alpha = isPeak ? 1f : 0.8f;
                if (barH > 0)
                    canvas.FillRoundedRectangle(bx, by, barW, barH, 3);

                canvas.Alpha = 1f;
                canvas.FontColor = LabelGrey;
                canvas.FontSize = 10;
                canvas.DrawString(Labels[i], x + i * slot, y + h - bottomPad + 2,
                    slot, bottomPad, HorizontalAlignment.Center, VerticalAlignment.Top);
            }
        }
    }
}
