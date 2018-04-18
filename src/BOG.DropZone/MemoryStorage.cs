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
        /// The collection of pathways and their data.
        /// </summary>
        public Dictionary<string, Dropzone> DropzoneList { get; set; } = new Dictionary<string, Dropzone>();
        /// <summary>
        /// 
        /// </summary>
        public Dictionary<string, string> IpCaller = new Dictionary<string, string>();

        public MemoryStorage()
        {
            stopTimer.Enabled = false;
            stopTimer.Elapsed += new ElapsedEventHandler(OnTimedEvent);
            stopTimer.Interval = 1000;
        }

        public void Reset()
        {
            DropzoneList.Clear();
        }

        public void Shutdown()
        {
            stopTimer.Enabled = true;
        }

        private static void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            System.Environment.Exit(0);
        }
    }
}
