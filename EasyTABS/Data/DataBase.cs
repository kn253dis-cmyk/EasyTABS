using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using EasyTABS.Entity;
using EasyTABS.Models;

namespace EasyTABS.Data
{
    public class Database : DbContext
    {
        public DbSet<User> Users => Set<User>();
        public DbSet<Song> Songs => Set<Song>();
        public DbSet<Artist> Artists => Set<Artist>();
        public DbSet<Album> Albums => Set<Album>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            string connectionString =
                "Host=ep-royal-dream-abh0pnss-pooler.eu-west-2.aws.neon.tech;" +
                "Timeout=60;Command Timeout=60;Database=neondb;" +
                "Username=neondb_owner;Password=npg_cexS96JAkaoj;" +
                "SSL Mode=VerifyFull;Channel Binding=Require;";

            optionsBuilder.UseNpgsql(connectionString, builder =>
            {
                builder.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorCodesToAdd: null);
            });
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Artist>(e =>
            {
                e.HasKey(a => a.Id);
                e.Property(a => a.Name).IsRequired().HasMaxLength(200);
            });

            modelBuilder.Entity<Album>(e =>
            {
                e.HasKey(a => a.Id);
                e.Property(a => a.Title).IsRequired().HasMaxLength(300);
                e.Property(a => a.CoverData).HasColumnType("bytea");

                e.HasOne(a => a.Artist)
                 .WithMany()
                 .HasForeignKey(a => a.ArtistId)
                 .OnDelete(DeleteBehavior.Cascade);

                // Один альбом на артиста за назвою.
                e.HasIndex(a => new { a.ArtistId, a.Title }).IsUnique();
            });

            modelBuilder.Entity<Song>(e =>
            {
                e.HasKey(s => s.Id);
                e.Property(s => s.Title).IsRequired().HasMaxLength(300);
                e.Property(s => s.Album).HasMaxLength(300);
                e.Property(s => s.TabFileName).HasMaxLength(300);

                // Файл таба — bytea у PostgreSQL.
                e.Property(s => s.TabData).HasColumnType("bytea");

                e.HasOne(s => s.Artist)
                 .WithMany(a => a.Songs)
                 .HasForeignKey(s => s.ArtistId)
                 .OnDelete(DeleteBehavior.Cascade);

                // Пісня посилається на альбом (nullable). Видалення альбому
                // не видаляє пісні — просто обнуляє посилання.
                e.HasOne(s => s.AlbumEntity)
                 .WithMany(a => a.Songs)
                 .HasForeignKey(s => s.AlbumId)
                 .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<User>(e =>
            {
                e.HasKey(u => u.ID);
                e.HasIndex(u => u.Email).IsUnique();
                e.HasIndex(u => u.NickName).IsUnique();
            });
        }

        public string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            var sb = new StringBuilder();
            foreach (var b in bytes)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
