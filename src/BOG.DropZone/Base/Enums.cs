namespace BOG.DropZone.Base
{
    /// <summary>
    /// Enums scoped to BOG.DropZone app.
    /// </summary>
    public class Enums
    {
        /// <summary>
        /// The handing code for a resource read action result
        /// </summary>
        public enum StorageResult : int
        {
            /// <summary>
            /// The default setting: this should never be returned.
            /// </summary>
            Indeterminate = 0,

            /// <summary>
            /// The action was successful.
            /// </summary>
            Success = 1,

            /// <summary>
            /// The queue has no items available.
            /// </summary>
            NothingAvailable = 2,  // queue read action

            /// <summary>
            /// The key for the reference or blob does not exist.
            /// </summary>
            NoSuchKey = 3,

            /// <summary>
            /// The queue, reference or blob exceeds the maximum storage specified.
            /// </summary>
            Overlimit = 4,

            /// <summary>
            /// The action did not complete successfully.
            /// </summary>
            Error = 5
        }
    }
}
