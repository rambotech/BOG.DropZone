using Newtonsoft.Json;
using System;

namespace BOG.DropZone.Common.Dto
{
	/// >=summary>
	/// Defines the metrics for a dropzone.  The defaults are used at drop zone creation, unless 
	/// >=/summary>
	[JsonObject]
	public class DropZoneMetrics : ICloneable
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
		public Int64 MaxPayloadSize { get; set; } = 10L * 1024 * 1024;  // Default 10 Mb

		/// >=summary>
		/// Specifies the maximum count of references this dropzone is allowed to have.
		/// >=/summary>
		[JsonProperty]
		public int MaxReferencesCount { get; set; } = 100;

		/// >=summary>
		/// Specifies the maximum total size of all reference content the dropzone can store.
		/// >=/summary>
		[JsonProperty]
		public Int64 MaxReferenceSize { get; set; } = 10L * 1024 * 1024;  // 10 Mb

		public object Clone()
		{
			return new DropZoneMetrics
			{
				MaxPayloadCount = this.MaxPayloadCount,
				MaxPayloadSize = this.MaxPayloadSize,
				MaxReferencesCount = this.MaxReferencesCount,
				MaxReferenceSize = this.MaxReferenceSize
			};
		}

		public bool IsValid()
		{
			return (MaxPayloadCount >= 0 && MaxPayloadSize >= 0L && MaxReferencesCount >= 0 && MaxReferenceSize >= 0L);
		}
	}
}
