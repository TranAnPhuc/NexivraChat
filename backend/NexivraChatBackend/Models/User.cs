using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NexivraChatBackend.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}