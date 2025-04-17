using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MailKit;

namespace FinTrack.Models
{
    public class EmailMessage
    {
        public string From { get; set; }
        public string Subject { get; set; }
        public string Preview { get; set; }
        public string FullBody { get; set; }
        public UniqueId Uid { get; set; }
    }
}

