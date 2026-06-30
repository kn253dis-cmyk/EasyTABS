using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace EasyTABS.Models
{
    public class Song
    {
        [Key]
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Album { get; set; } = string.Empty;
        public int ArtistId { get; set; }
        public Artist Artist { get; set; } = null!;
        // Зовнішнє зберігання обгортки: у БД лише URL/шлях.
        public string AlbumCoverPath { get; set; } = "album_placeholder.png";
        // Файл таба зберігається безпосередньо в БД як байти.
        // Невеликий, текстовий — підходить для byte[].
        public byte[]? TabData { get; set; }
        // Оригінальна назва файлу (напр. "song.gp5") — щоб знати розширення/тип.
        public string TabFileName { get; set; } = string.Empty;
    }
}
