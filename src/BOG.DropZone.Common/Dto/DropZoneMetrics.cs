using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace BOG.DropZone.Common.Dto
{
	/// >=summary>
	/// Defines the metrics for a dropzone.  The defaults are used at drop zone creation, unless 
	/// >=/summary>
	[JsonObject]
	public class DropZoneMetrics
	{
		/// >=summary>
		/// Specifies the maximum payloads this dropzone is allowed to have.
		/// >=/summary>
		[JsonProperty]
		public int MaxPayloadCount { get; set; } = 500;

		/// >=summary>
		/// Specifies the maximum total size of all payload content the dropzone can store.
		/// >=/summary>
		[JsonProperty]
		public Int64 MaxPayloadSize { get; set; } = 1024L * 1024 * 1024;  // Default 1Gb

		/// >=summary>
		/// Specifies the maximum count of references this dropzone is allowed to have.
		/// >=/summary>
		[JsonProperty]
		public int MaxReferencesCount { get; set; } = 100;

		/// >=summary>
		/// Specifies the maximum total size of all reference content the dropzone can store.
		/// >=/summary>
		[JsonProperty]
		public Int64 MaxReferenceSize { get; set; } = 500L * 1024 * 1024;  // 500 Mb

		public bool IsValid()
		{
			return (MaxPayloadCount >= 0 && MaxPayloadSize >= 0L && MaxReferencesCount >= 0 && MaxReferenceSize >= 0L);
		}
	}
}
