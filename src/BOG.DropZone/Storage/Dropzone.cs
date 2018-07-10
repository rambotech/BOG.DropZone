using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BOG.DropZone.Storage
{
    /// <summary>
    /// Defines a payload: its metrics, its activity and its content.
    /// </summary>
    [JsonObject]
    public class Dropzone
    {
        /// <summary>
        /// Specifies the maximum payloads this dropzone is allowed to have.
        /// </summary>
        [JsonProperty]
        public int MaxPayloadCount { get; set; } = 500;

        /// <summary>
        /// Specifies the maximum total size of all payload content the dropzone can store.
        /// </summary>
        [JsonProperty]
        public Int64 MaxPayloadSize { get; set; } = 1048L*1024*01024;

        /// <summary>
        /// The number of payload dropoffs denied due to count or size limits triggered.
        /// </summary>
        [JsonProperty]
        public int PayloadDropOffsDenied { get; set; } = 0;

        /// <summary>
        /// Specifies the maximum count of references this dropzone is allowed to have.
        /// </summary>
        [JsonProperty]
        public int MaxReferencesCount { get; set; } = 50;

        /// <summary>
        /// Specifies the maximum total size of all reference content the dropzone can store.
        /// </summary>
        [JsonProperty]
        public Int64 MaxReferenceSize { get; set; } = 500L * 1024 * 1024;

        /// <summary>
        /// The number of reference set actions denied due to count or size limits triggered.
        /// </summary>
        [JsonProperty]
        public int ReferenceSetsDenied { get; set; } = 0;

        /// <summary>
        /// The total size of all payloads currently stored in the dropzone.
        /// </summary>
        [JsonProperty]
        public Int64 PayloadSize { get; set; } = 0L;

        /// <summary>
        /// The total count of payloads currently stored in the dropzone.
        /// </summary>
        [JsonProperty]
        public int PayloadCount { get { return Payloads.Count; } }

        /// <summary>
        /// The total size of all references currently stored in the dropzone.
        /// </summary>
        [JsonProperty]
        public Int64 ReferenceSize { get; set; } = 0L;

        /// <summary>
        /// The total count of references currently stored in the dropzone.
        /// </summary>
        [JsonProperty]
        public int ReferenceCount { get { return References.Count; } }

        /// <summary>
        /// The time a payload was last dropped off.
        /// </summary>
        [JsonProperty]
        public DateTime LastDropoff { get; set; }

        /// <summary>
        /// The time a payload was last picked up.
        /// </summary>
        [JsonProperty]
        public DateTime LastPickup { get; set; }

        /// <summary>
        /// The time a reference was last retrieved.
        /// </summary>
        [JsonProperty]
        public DateTime LastGetReference { get; set; }

        /// <summary>
        /// The time a reference was last set.
        /// </summary>
        [JsonProperty]
        public DateTime LastSetReference { get; set; }

        /// <summary>
        /// The payload storage.
        /// </summary>
        [JsonIgnore]
        public ConcurrentQueue<string> Payloads { get; set; } = new ConcurrentQueue<string>();

        /// <summary>
        /// The reference storage.
        /// </summary>
        [JsonIgnore]
        public ConcurrentDictionary<string, string> References { get; set; } = new ConcurrentDictionary<string, string>();
    }
}