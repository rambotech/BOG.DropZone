using System;

namespace BOG.DropZone.Entity
{
    public class QueueItem
    {
        public string Sequence { get; set; }
        public string Recipient { get; set; } = "*";
        public DateTime ExpiresOn { get; set; } = DateTime.MaxValue;
        public string Tracking { get; set; }
        public string Payload { get; set; }
    }
}
