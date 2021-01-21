using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace BOG.DropZone.Common.Dto
{
	/// <summary>
	/// Defines a payload: its metrics, its activity and its content.
	/// </summary>
	[JsonObject]
    public class DropZoneInfo
    {
        /// <summary>
        /// The name of the dropzone
        /// </summary>
        [JsonProperty]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The list of recipient queues in the dropzone (* = global access), with the queued payload count for each.
        /// </summary>
        [JsonProperty]
        public Dictionary<string, int> Recipients { get; set; } = new Dictionary<string, int>();

        /// <summary>
        /// The message to display if the data is not to be trusted.
        /// </summary>
        [JsonProperty]
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// The number of reference set actions denied due to count or size limits triggered.
        /// </summary>
        [JsonProperty]
        public int ReferenceSetsFailedCount { get; set; } = 0;

        /// <summary>
        /// The number of payload dropoffs denied due to count or size limits triggered.
        /// </summary>
        [JsonProperty]
        public int PayloadDropOffsFailedCount { get; set; } = 0;

        /// <summary>
        /// The metrics to use for the dropzone.
        /// </summary>
        [JsonProperty]
        public DropZoneMetrics Metrics { get; set; } = new DropZoneMetrics();

        /// <summary>
        /// The total size of all payloads currently stored in the dropzone.
        /// </summary>
        [JsonProperty]
        public Int64 PayloadSize { get; set; } = 0L;

        /// <summary>
        /// The total count of payloads currently stored in the dropzone.
        /// </summary>
        [JsonProperty]
        public int PayloadCount { get; set; } = 0;

        /// <summary>
        /// The total count of payloads dropped because of the expiration date.
        /// </summary>
        [JsonProperty]
        public int PayloadExpiredCount { get; set; } = 0;

        /// <summary>
        /// The total size of all references currently stored in the dropzone.
        /// </summary>
        [JsonProperty]
        public Int64 ReferenceSize { get; set; } = 0L;

        /// <summary>
        /// The total count of references currently stored in the dropzone.
        /// </summary>
        [JsonProperty]
        public int ReferenceCount { get; set; } = 0;

        /// <summary>
        /// The total count of references dropped because of the expiration date.
        /// </summary>
        [JsonProperty]
        public int ReferenceExpiredCount { get; set; } = 0;

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
    }
}