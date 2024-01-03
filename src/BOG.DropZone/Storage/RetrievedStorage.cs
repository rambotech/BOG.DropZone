using BOG.DropZone.Base;
using System.Collections.Generic;

namespace BOG.DropZone.Storage
{
    /// <summary>
    /// A result from reading from a queue, reference or blob.
    /// </summary>
    public class RetrievedStorage
    {
        /// <summary>
        /// The result handling
        /// </summary>
        public Enums.StorageResult Result { get; set; } = Enums.StorageResult.Indeterminate;

        /// <summary>
        /// A description of any error encountered.
        /// </summary>
        public string ResultDescription { get; set; } = string.Empty;

        /// <summary>
        /// Queues only: The optional tracking reference provided by the sender, to later query whether 
        /// the entry has yet been retrieved.
        /// </summary>
        public string Tracking { get; set; } = string.Empty;

        /// <summary>
        /// The value as a string. Caller must cast/parse as desired.
        /// </summary>
        public string Payload { get; set; } = null;
    }
}
