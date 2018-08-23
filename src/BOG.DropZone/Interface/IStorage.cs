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
        /// The list of dropzone information
        /// </summary>
        string AccessToken { get; set; }

        /// <summary>
        /// The list of dropzone information
        /// </summary>
        Dictionary<string, DropPoint> DropZoneList { get; set; }

        /// <summary>
        /// The list of clients who have submitted invalid 
        /// </summary>
        List<FailedAuthTokenWatch> FailedAuthTokenWatchList { get; set; }

        /// <summary>
        /// Reset the site to a fresh startup state.
        /// </summary>
        void Reset();

        /// <summary>
        /// Reset the site to a fresh startup state.
        /// </summary>
        void Clear(string dropZoneName);

        /// <summary>
        /// Shutdown the site using an application exit.
        /// </summary>
        void Shutdown();
    }
}
