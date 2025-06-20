using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TelegramEAManager
{
    public class ChannelInfo
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Username { get; set; } = "";
        public string Type { get; set; } = "";
        public int MembersCount { get; set; }
        public long AccessHash { get; set; }
        public DateTime LastActivity { get; set; }
    }
}
