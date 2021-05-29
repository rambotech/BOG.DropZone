using System;

namespace BOG.DropZone.Storage
{
	/// <summary>
	/// A string with a perish date.
	/// </summary>
	public class StoredValue
    {
        /// <summary>
        /// The content
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// The point in time where the content is no longer valid.
        /// </summary>
        public DateTime Expires { get; set; } = DateTime.MaxValue;

        /// <summary>
        /// The optional tracking reference provider by the sender, to later query whether the entry has yet been retrieved.
        /// </summary>
        public string Tracking { get; set; } = string.Empty;
    }
}
