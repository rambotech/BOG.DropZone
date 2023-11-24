using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace BOG.DropZone
{
    /// <summary>
    /// Queued payloads
    /// </summary>
    public class ListFIFO
    {
        public string Sequence { get; set; }
        public string Recipient { get; set; } = "*";
        public DateTime ExpiresOn { get; set; } = DateTime.MaxValue;
        public string Tracking { get; set; }
        public string Payload { get; set; }
    }

    /// <summary>
    /// Reference payloads
    /// </summary>
    public class ListKV
    {
        public string Key { get; set; }
        public string Payload { get; set; }
        public DateTime ExpiresOn { get; set; } = DateTime.MaxValue;
    }
}
