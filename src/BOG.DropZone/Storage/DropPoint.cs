using System.Collections.Concurrent;
using System.Collections.Generic;
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
		/// The dictionary for stored values uses a key of 
		/// </summary>
		public Dictionary<string, Dictionary<long, StoredValue>> Payloads { get; set; } = new Dictionary<string, Dictionary<long, StoredValue>>();

		/// <summary>
		/// The reference storage.
		/// </summary>
		public Dictionary<string, StoredValue> References { get; set; } = new Dictionary<string, StoredValue>();
	}
}