using System;
using System.Collections.Generic;
using System.Text;
using BOG.SwissArmyKnife;

namespace BOG.DropZone.Common.Dto
{
    public class FailedAuthTokenWatch
    {
        /// <summary>
        /// The Ip adress of the caller.
        /// </summary>
        public string IpAddress { get; set; }

        /// <summary>
        /// The names of the dropzones targeted, and the method calls to each zone.
        /// Only the first 20 are logged, in case a flooding attack occurs.
        /// </summary>
        public Dictionary<string, Dictionary<string, long>> AccessPoints { get; set; } = new Dictionary<string, Dictionary<string, long>>();

        /// <summary>
        /// The total count of unique dropzone names used for methods.
        /// </summary>
        public long AccessPointCount { get; set; } = 0L;

        /// <summary>
        /// The total number of failed attempts made.
        /// </summary>
        public long Attempts { get; set; }

        /// <summary>
        /// The time the first failed attempt was made.
        /// </summary>
        public DateTime FirstAttempt { get; set; } = DateTime.Now;

        /// <summary>
        /// The time the latest failed attempt was made.
        /// </summary>
        public DateTime LatestAttempt { get; set; } = DateTime.Now;
    }
}
