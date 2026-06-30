using EasyTABS.Data;
using EasyTABS.Models;
using Microsoft.EntityFrameworkCore;

namespace EasyTABS.Services
{
    public class SongRepository
    {
        // Усі пісні з артистами (для списку на головній).
        public async Task<List<Song>> GetAllSongsAsync()
        {
            using var db = new Database();
            return await db.Songs
                .Include(s => s.Artist)
                .OrderBy(s => s.Title)
                .ToListAsync();
        }

        // Пошук за назвою або іменем артиста (заміна твого фільтра по JSON).
        public async Task<List<Song>> SearchAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return await GetAllSongsAsync();

            query = query.Trim();
            using var db = new Database();
            return await db.Songs
                .Include(s => s.Artist)
                .Where(s => EF.Functions.ILike(s.Title, $"%{query}%")
                         || EF.Functions.ILike(s.Artist.Name, $"%{query}%"))
                .OrderBy(s => s.Title)
                .ToListAsync();
        }

        // Додавання пісні з файлами. Артист шукається або створюється.
        public async Task AddSongAsync(string title, string artistName, string album,
                                       byte[]? tabData, string tabFileName,
                                       byte[]? coverData)
        {
            using var db = new Database();

            var artist = await db.Artists.FirstOrDefaultAsync(a => a.Name == artistName)
                         ?? new Artist { Name = artistName };

            bool duplicate = await db.Songs
                .AnyAsync(s => s.Title == title && s.Artist.Name == artistName);
            if (duplicate)
                throw new InvalidOperationException($"Пісня \"{title}\" вже існує.");

            var song = new Song
            {
                Title = title,
                Album = string.IsNullOrWhiteSpace(album) ? "Unknown" : album,
                Artist = artist,
                TabData = tabData,
                TabFileName = tabFileName,
                AlbumCoverData = coverData
            };

            db.Songs.Add(song);
            await db.SaveChangesAsync();
        }

        // Завантаження байтів таба для плеєра.
        public async Task<(byte[]? data, string fileName)> GetTabAsync(int songId)
        {
            using var db = new Database();
            var song = await db.Songs.FirstOrDefaultAsync(s => s.Id == songId);
            return (song?.TabData, song?.TabFileName ?? string.Empty);
        }
    }
}