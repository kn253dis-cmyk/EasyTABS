using System.ComponentModel.DataAnnotations;

namespace EasyTABS.Models
{
    // Альбом зберігає обкладинку один раз; усі пісні альбому посилаються на нього.
    // Це прибирає дублювання картинок у БД (одна обкладинка на альбом, а не на пісню).
    public class Album
    {
        [Key]
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        // Обкладинка альбому (PNG/JPG) — одна на всі пісні альбому.
        public byte[]? CoverData { get; set; }

        // Альбом належить артисту (той самий альбом у різних артистів — різні записи).
        public int ArtistId { get; set; }
        public Artist Artist { get; set; } = null!;

        public ICollection<Song> Songs { get; set; } = new List<Song>();
    }
}
