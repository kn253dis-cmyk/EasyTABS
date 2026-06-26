namespace EasyTABS.Models
{
    public class Song
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string Album { get; set; } = string.Empty;
        public string AlbumCoverPath { get; set; } = "album_placeholder.png";
        public string FilePath { get; set; } = string.Empty;

        public Song() { }
        public Song(int id, string title, string artist, string album, string albumCoverPath, string filePath)
        {
            Id = id;
            Title = title;
            Artist = artist;
            Album = album;
            AlbumCoverPath = albumCoverPath;
            FilePath = filePath;
        }
    }
}
