using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using BOG.DropZone.Interface;
using BOG.DropZone.Storage;
using Microsoft.EntityFrameworkCore;

namespace BOG.DropZone
{
    /// <summary>
    /// The collection of items persisted on the server, and static across calls.
    /// </summary>
    public class MemoryStorage : IStorage
    {
        Timer stopTimer = new Timer();

        /// <summary>
        /// The collection of drop zones and their data.
        /// </summary>
        public Dictionary<string, Dropzone> DropzoneList { get; set; } = new Dictionary<string, Dropzone>();

        /// <summary>
        /// Constructor.
        /// </summary>
        public MemoryStorage()
        {
            stopTimer.Enabled = false;
            stopTimer.Elapsed += new ElapsedEventHandler(OnTimedEvent);
            stopTimer.Interval = 1000;
        }

        /// <summary>
        /// Clear all drop zones, including their payloads and reference dictionary.
        /// </summary>
        public void Reset()
        {
            DropzoneList.Clear();
        }

        /// <summary>
        /// Shutdowns down the web server, requiring restart at the command line.
        /// </summary>
        public void Shutdown()
        {
            // triggers a timer, which does the actual shutdown after the thread is in an idle state.
            stopTimer.Enabled = true;
        }

        private static void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            System.Environment.Exit(0);
        }
    }
}
