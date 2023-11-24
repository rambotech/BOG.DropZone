using System;

namespace BOG.DropZone.Entity
{
    public class ReferenceItem
    {
        public string Key { get; set; }
        public string Payload { get; set; }
        public DateTime ExpiresOn { get; set; } = DateTime.MaxValue;
    }
}
