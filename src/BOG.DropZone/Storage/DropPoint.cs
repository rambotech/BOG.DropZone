using System.Collections.Concurrent;
using BOG.DropZone.Common.Dto;

namespace BOG.DropZone.Storage
{
	/// <summary>
	/// Defines a payload: its properties, its activity and its content.
	/// </summary>
	public class DropPoint
    {
        /// <summary>
        /// DropZoneInfo
        /// </summary>
        public DropZoneInfo Statistics { get; set; } = new DropZoneInfo();

        /// <summary>
        /// The payload storage. Key is empty or null for any, otherwise the optional identifier on the DropOffPayload or PickUpPayload endpoints.
        /// </summary>
        public ConcurrentDictionary<string, ConcurrentQueue<StoredValue>> Payloads { get; set; } = new ConcurrentDictionary<string, ConcurrentQueue<StoredValue>>();

        /// <summary>
        /// The reference storage.
        /// </summary>
        public ConcurrentDictionary<string, StoredValue> References { get; set; } = new ConcurrentDictionary<string, StoredValue>();
    }
}