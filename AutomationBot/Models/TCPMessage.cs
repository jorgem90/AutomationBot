using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutomationBot.Models
{
    public class TCPMessage
    {
        public string type { get; set; }
        public string title { get; set; }
        public string? content { get; set; }
        public string? fileName { get; set; }
        public long chatId { get; set; }
        public int messageId { get; set; }
    }
}
