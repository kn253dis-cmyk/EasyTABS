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

        public string AlbumCoverPath { get; set; } = "album_placeholder.png";
        public string FilePath { get; set; } = string.Empty;

        public Song() { }
        public Song(string title, string album, int artistId, string albumCoverPath, string filePath)
        {
            Title = title;
            Album = album;
            ArtistId = artistId;
            AlbumCoverPath = albumCoverPath;
            FilePath = filePath;
        }
    }
}
