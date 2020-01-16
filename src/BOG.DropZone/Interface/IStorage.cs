using BOG.DropZone.Common.Dto;
using BOG.DropZone.Storage;
using System.Collections.Generic;

namespace BOG.DropZone.Interface
{
    /// <summary>
    /// Defines the site's common storage items.
    /// </summary>
    public interface IStorage
    {
        /// <summary>
        /// An optional access token value which the client must provide to use operational methods.
        /// </summary>
        string AccessToken { get; set; }

        /// <summary>
        /// An optional access token value which the client must provide to use administrative methods.
        /// </summary>
        string AdminToken { get; set; }

        /// <summary>
        /// The root folder of the file system for persisting payloads and references (if memory is not sufficient).
        /// </summary>
        string PersistencePath { get; set; }

        /// <summary>
        /// The list of dropzone information
        /// </summary>
        int MaxDropzones { get; set; }

        /// <summary>
        /// The number of consecutive mismatched access token values which triggers a lockout period.
        /// </summary>
        int MaximumFailedAttemptsBeforeLockout { get; set; }

        /// <summary>
        /// The number of seconds which a lockout endures.
        /// </summary>
        int LockoutSeconds { get; set; }

        /// <summary>
        /// The list of dropzone information
        /// </summary>
        Dictionary<string, DropPoint> DropZoneList { get; set; }

        /// <summary>
        /// The list of clients who have submitted invalid 
        /// </summary>
        List<ClientWatch> ClientWatchList { get; set; }

        /// <summary>
        /// Reset the site to a fresh startup state.
        /// </summary>
        void Reset();

        /// <summary>
        /// Reset only this dropzone to a fresh startup state.
        /// </summary>
        void Clear(string dropZoneName);

        /// <summary>
        /// Shutdown the site using an application exit.
        /// </summary>
        void Shutdown();
    }
}
