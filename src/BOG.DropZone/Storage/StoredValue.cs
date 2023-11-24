using System;

namespace BOG.DropZone.Storage
{
	/// <summary>
	/// A string with a perish date.
	/// </summary>
	public class StoredValue
    {
        /// <summary>
        /// The intended recipient
        /// </summary>
        public string Recipient{ get; set; }

        /// <summary>
        /// The name within the storage and type to retrieve the value
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The optional tracking reference provider by the sender, to later query whether the entry has yet been retrieved.
        /// </summary>
        public string Tracking { get; set; } = string.Empty;

        /// <summary>
        /// The point in time where the content is no longer valid.
        /// </summary>
        public DateTime Expires { get; set; } = DateTime.MaxValue;

    }
}
