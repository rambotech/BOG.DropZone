using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.Text;

namespace BOG.DropZone.Client.Entity
{
	/// <summary>
	///  The result from a payload inquiry to check if the payload is still queued.
	/// </summary>
	[JsonObject]
	public class PayloadInquiry
	{
		[JsonProperty]
		public string Tracking { get; set; } = string.Empty;
		[JsonProperty]
		public bool Found { get; set; } = false;
		[JsonProperty]
		public DateTime Expiration { get; set; } = DateTime.MinValue;
	}
}
