using BOG.DropZone.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
        Dictionary<string, Dropzone> DropzoneList { get; set; }

        /// <summary>
        /// Reset the site to a fresh startup state.
        /// </summary>
        void Reset();

        /// <summary>
        /// Reset the site to a fresh startup state.
        /// </summary>
        void Clear(string dropZone);

        /// <summary>
        /// Shutdown the site using an application exit.
        /// </summary>
        void Shutdown();
    }
}
