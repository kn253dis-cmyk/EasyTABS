using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace EasyTABS.Models
{
    public class Artist
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        public ICollection<Song> Songs { get; set; } = new List<Song>();
        public Artist() { }
        public Artist(string name)=>Name = name;
    }
}
