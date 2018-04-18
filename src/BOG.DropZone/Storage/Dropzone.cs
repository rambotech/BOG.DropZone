using BOG.DropZone.Client.Model;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BOG.DropZone.Storage
{
    [JsonObject]
    public class Dropzone
    {
        public int MaxDropzonesCount { get; set; } = 200;
        public Int64 MaxTotalDropzonesSize { get; set; } = 100*1024*01024;
        public int MaxReferencesCount { get; set; } = 10;
        public Int64 MaxTotalReferencesSize { get; set; } = 10 * 1024 * 1024;

        public Int64 PayloadSize { get; set; } = 0L;
        public int PayloadCount { get { return Dropzones.Count; } }
        public Int64 ReferenceSize { get; set; } = 0L;
        public int ReferenceCount { get { return References.Count; } }

        [JsonIgnore]
        public ConcurrentQueue<Lockbox> Dropzones { get; set; } = new ConcurrentQueue<Lockbox>();
        [JsonIgnore]
        public ConcurrentDictionary<string, Lockbox> References { get; set; } = new ConcurrentDictionary<string, Lockbox>();
    }
}