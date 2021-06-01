using System;
using Newtonsoft.Json;

namespace BOG.DropZone.Client
{
	/// <summary>
	/// Specifies handling properties about a payload
	/// </summary>
	[JsonObject]
	public class PayloadMetadata
	{
		/// <summary>
		///  When the payload should be discarded instead of being returned to the caller.
		///  Default is never.
		/// </summary>
		[JsonProperty]
		public DateTime ExpiresOn { get; set; } = DateTime.MaxValue;

		/// <summary>
		/// A tracking number which can be used to later query the drop zone payloads to determine if it is awating pickup.
		/// </summary>
		[JsonProperty]
		public string Tracking { get; set; } = string.Empty;

		/// <summary>
		/// The payload can only be delivered to a specific recipient.  Default is any requestor.
		/// </summary>
		[JsonProperty]
		public string Recipient { get; set; } = string.Empty;
	}
}
