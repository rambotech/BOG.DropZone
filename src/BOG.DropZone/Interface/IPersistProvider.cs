using BOG.DropZone.Storage;
using System;
using System.Collections.Generic;

namespace BOG.DropZone.Interface
{
    /// <summary>
    /// Methods for writing to the various storage location choices.
    /// </summary>
    public interface IPersistProvider
    {
        /// <summary>
        /// The root folder for any file storage
        /// </summary>
        string RootFolder { get; set; }

        /// <summary>
        /// Load persisted data into the tracing objects.
        /// </summary>
        /// <param name="dropPoints"></param>
        void GetExistingItems(ref Dictionary<string, DropPoint> dropPoints);

        /// <summary>
        /// Load persisted data into the tracing objects.
        /// </summary>
        /// <param name="dropPoints"></param>
        void SaveExistingItems(ref Dictionary<string, DropPoint> dropPoints);

    }
}
