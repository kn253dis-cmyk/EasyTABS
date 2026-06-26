using Microsoft.Maui.Graphics;

namespace EasyTABS.Views
{
    public partial class TunerPage : ContentPage
    {
        public TunerPage()
        {
            InitializeComponent();
        }
    }

    // Малювання спектра. Поки що — статична плоска лінія (заглушка).
    // Коли під'єднаєте мікрофон + FFT, оновлюйте масив амплітуд
    // і викликайте GraphicsView.Invalidate() для перемалювання.
    public class SpectrumDrawable : IDrawable
    {
        public float[] Amplitudes { get; set; } = Array.Empty<float>();

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            canvas.StrokeColor = Color.FromArgb("#39FF14");
            canvas.StrokeSize = 2;

            float midY = dirtyRect.Height / 2f;

            if (Amplitudes.Length < 2)
            {
                // Пряма лінія по центру, поки немає аудіоданих.
                canvas.DrawLine(0, midY, dirtyRect.Width, midY);
                return;
            }

            var path = new PathF();
            float step = dirtyRect.Width / (Amplitudes.Length - 1);

            path.MoveTo(0, midY - Amplitudes[0] * midY);
            for (int i = 1; i < Amplitudes.Length; i++)
            {
                float x = i * step;
                float y = midY - Amplitudes[i] * midY;
                path.LineTo(x, y);
            }

            canvas.DrawPath(path);
        }
    }
}
