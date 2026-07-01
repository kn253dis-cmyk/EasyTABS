using EasyTABS.Data;
using EasyTABS.Models;
using Microsoft.EntityFrameworkCore;

namespace EasyTABS.Services
{
    public class SongRepository
    {
        // Усі пісні з артистом та альбомом (для списку на головній).
        public async Task<List<Song>> GetAllSongsAsync()
        {
            using var db = new Database();
            return await db.Songs
                .Include(s => s.Artist)
                .Include(s => s.AlbumEntity)
                .OrderBy(s => s.Title)
                .ToListAsync();
        }

        // Пошук за назвою пісні або іменем артиста.
        public async Task<List<Song>> SearchAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return await GetAllSongsAsync();

            query = query.Trim();
            using var db = new Database();
            return await db.Songs
                .Include(s => s.Artist)
                .Include(s => s.AlbumEntity)
                .Where(s => EF.Functions.ILike(s.Title, $"%{query}%")
                         || EF.Functions.ILike(s.Artist.Name, $"%{query}%"))
                .OrderBy(s => s.Title)
                .ToListAsync();
        }

        // Додавання пісні. Артист і альбом шукаються або створюються.
        // Обкладинка зберігається на рівні альбому — одна на всі пісні альбому.
        public async Task AddSongAsync(string title, string artistName, string albumTitle,
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

            Album? album = null;
            if (!string.IsNullOrWhiteSpace(albumTitle) &&
                !albumTitle.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
            {
                // Шукаємо існуючий альбом цього артиста за назвою.
                album = await db.Albums
                    .Include(a => a.Artist)
                    .FirstOrDefaultAsync(a => a.Title == albumTitle && a.Artist.Name == artistName);

                if (album is null)
                {
                    // Новий альбом — зберігаємо обкладинку тут (один раз).
                    album = new Album
                    {
                        Title = albumTitle,
                        Artist = artist,
                        CoverData = coverData
                    };
                    db.Albums.Add(album);
                }
                else if (album.CoverData is null && coverData is not null)
                {
                    // Альбом уже є, але без обкладинки — доповнюємо.
                    album.CoverData = coverData;
                }
                // Якщо обкладинка вже є — нову ігноруємо (не дублюємо).
            }

            var song = new Song
            {
                Title = title,
                Album = string.IsNullOrWhiteSpace(albumTitle) ? "Unknown" : albumTitle,
                Artist = artist,
                AlbumEntity = album,
                TabData = tabData,
                TabFileName = tabFileName
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

        // Повна пісня з артистом і альбомом для сторінки плеєра.
        public async Task<Song?> GetSongByIdAsync(int songId)
        {
            using var db = new Database();
            return await db.Songs
                .Include(s => s.Artist)
                .Include(s => s.AlbumEntity)
                .FirstOrDefaultAsync(s => s.Id == songId);
        }

        // Унікальні імена артистів (підказки для поля "Виконавець").
        public async Task<List<string>> GetArtistNamesAsync(string query)
        {
            using var db = new Database();
            var q = db.Artists.AsQueryable();

            if (!string.IsNullOrWhiteSpace(query))
            {
                query = query.Trim();
                q = q.Where(a => EF.Functions.ILike(a.Name, $"%{query}%"));
            }

            return await q.OrderBy(a => a.Name)
                          .Select(a => a.Name)
                          .Take(6)
                          .ToListAsync();
        }

        // Підказки назв пісень (для поля "Назва пісні").
        public async Task<List<string>> GetSongTitlesAsync(string query)
        {
            using var db = new Database();
            var q = db.Songs.AsQueryable();

            if (!string.IsNullOrWhiteSpace(query))
            {
                query = query.Trim();
                q = q.Where(s => EF.Functions.ILike(s.Title, $"%{query}%"));
            }

            return await q.OrderBy(s => s.Title)
                          .Select(s => s.Title)
                          .Distinct()
                          .Take(6)
                          .ToListAsync();
        }

        // Підказки альбомів (для попапу в полі "Альбом").
        // Повертає назви існуючих альбомів; за бажанням фільтрує за артистом.
        public async Task<List<AlbumSuggestion>> GetAlbumSuggestionsAsync(string query, string? artistName = null)
        {
            using var db = new Database();
            var q = db.Albums.Include(a => a.Artist).AsQueryable();

            if (!string.IsNullOrWhiteSpace(artistName))
                q = q.Where(a => a.Artist.Name == artistName.Trim());

            if (!string.IsNullOrWhiteSpace(query))
            {
                query = query.Trim();
                q = q.Where(a => EF.Functions.ILike(a.Title, $"%{query}%"));
            }

            return await q.OrderBy(a => a.Title)
                          .Take(6)
                          .Select(a => new AlbumSuggestion
                          {
                              Title = a.Title,
                              ArtistName = a.Artist.Name,
                              CoverData = a.CoverData
                          })
                          .ToListAsync();
        }
    }

    // Легка модель підказки альбому (з обкладинкою для прев'ю).
    public class AlbumSuggestion
    {
        public string Title { get; set; } = string.Empty;
        public string ArtistName { get; set; } = string.Empty;
        public byte[]? CoverData { get; set; }
    }
}
