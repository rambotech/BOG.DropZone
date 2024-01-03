using System;

namespace BOG.DropZone.Storage
{
	/// <summary>
	/// The object stored (less payload) for a queue or reference action.
	/// </summary>
	public class StoredValue
    {
        /// <summary>
        /// The intended recipient
        /// </summary>
        public string Recipient { get; set; }

        /// <summary>
        /// Queues only: The optional tracking reference provided by the sender, to later query whether 
        /// the entry has yet been retrieved.
        /// </summary>
        public string Tracking { get; set; } = string.Empty;

        /// <summary>
        /// The point in time where the content is no longer valid.
        /// </summary>
        public DateTime Expires { get; set; } = DateTime.MaxValue;
    }
}
