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

        // Обгортка альбому як байти в БД (PNG/JPG).
        public byte[]? AlbumCoverData { get; set; }

        // Файл таба як байти в БД.
        public byte[]? TabData { get; set; }
        public string TabFileName { get; set; } = string.Empty;
    }
}