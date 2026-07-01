using System.ComponentModel.DataAnnotations;

namespace EasyTABS.Models
{
    public class Song
    {
        [Key]
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        // Денормалізована назва альбому (для швидкого пошуку/сортування/підказок).
        public string Album { get; set; } = string.Empty;

        public int ArtistId { get; set; }
        public Artist Artist { get; set; } = null!;

        // Посилання на альбом, у якому зберігається спільна обкладинка.
        // Nullable: пісня може бути без альбому (сингл).
        public int? AlbumId { get; set; }
        public Album? AlbumEntity { get; set; }

        // Файл таба як байти в БД.
        public byte[]? TabData { get; set; }
        public string TabFileName { get; set; } = string.Empty;

        // Зручний доступ до обкладинки: беремо з альбому (спільна).
        // Не мапиться в БД — обчислюється на льоту.
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public byte[]? AlbumCoverData => AlbumEntity?.CoverData;
    }
}
