using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BOG.DropZone.Common.Dto;
using BOG.SwissArmyKnife;

namespace BOG.DropZone.Storage
{
    /// <summary>
    /// Defines a payload: its properties, its activity and its content.
    /// </summary>
    public class DropPoint
    {
        /// <summary>
        /// DropZoneInfo
        /// </summary>
        public DropZoneInfo Statistics { get; set; } = new DropZoneInfo();

        /// <summary>
        /// The payload storage.
        /// </summary>
        public ConcurrentQueue<StoredValue> Payloads { get; set; } = new ConcurrentQueue<StoredValue>();

        /// <summary>
        /// The reference storage.
        /// </summary>
        public ConcurrentDictionary<string, StoredValue> References { get; set; } = new ConcurrentDictionary<string, StoredValue>();
    }
}