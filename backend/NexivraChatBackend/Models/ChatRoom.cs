using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NexivraChatBackend.Models
{
    public class ChatRoom
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
    }
}