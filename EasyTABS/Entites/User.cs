using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using EasyTABS.Models;


namespace EasyTABS.Entity
{
    public class User
    {
        [Key]
        public int ID { get; set; }
        public string Password { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string NickName { get; set; } = string.Empty;

        public User() { }

        public User(string password,  string email, string nickName)
        {
            Password = password;
            Email = email;
            NickName = nickName;
        }
    }
}
