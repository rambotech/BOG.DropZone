using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BOG.DropZone.Storage
{
    /// <summary>
    /// A string with a perish date.
    /// </summary>
    public class StoredValue
    {
        /// <summary>
        /// The content
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// The point in time where the content is no longer valid.
        /// </summary>
        public DateTime Expires { get; set; } = DateTime.MaxValue;
    }
}
