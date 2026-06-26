namespace EasyTABS.Models
{
    public class Song
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string Album { get; set; } = string.Empty;

        // Шлях до обкладинки. У MAUI зображення з пакета задаються
        // просто іменем файлу (наприклад "album_cover.png").
        public string AlbumCoverPath { get; set; } = "album_placeholder.png";

        // Шлях до файлу Guitar Pro / табулатури.
        public string FilePath { get; set; } = string.Empty;
    }
}
