using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace BOG.DropZone.Common.Dto
{
	[JsonObject]
    public class ClientWatch
    {
        /// <summary>
        /// The Ip adress of the caller.  Also the unique key when used as a list.
        /// </summary>
        [JsonProperty]
        public string IpAddress { get; set; }

        /// <summary>
        /// The names of the dropzones targeted, and the method calls to each zone.
        /// Only the first 20 are logged, in case a flooding attack occurs.
        /// </summary>
        [JsonProperty]
        public Dictionary<string, Dictionary<string, long>> AccessPoints { get; set; } = new Dictionary<string, Dictionary<string, long>>();

        /// <summary>
        /// The total number of successful attempts made.
        /// </summary>
        [JsonProperty]
        public long AccessAttemptsTotalCount { get; set; } = 0;

        /// <summary>
        /// The total number of failed attempts made.
        /// </summary>
        [JsonProperty]
        public long FailedAccessAttemptsTotalCount { get; set; } = 0;

        /// <summary>
        /// The total number of failed attempts made.
        /// </summary>
        [JsonIgnore]
        public Queue<DateTime> FailedAccessTimes { get; set; } = new Queue<DateTime>();

        /// <summary>
        /// When the lock out delay ends.
        /// </summary>
        public int FailedAccessAttemptsCurrentCount { get { return FailedAccessTimes.Count; } }

        /// <summary>
        /// The time the first failed attempt was made.
        /// </summary>
        [JsonProperty]
        public DateTime FirstAttempt { get; set; } = DateTime.Now;

        /// <summary>
        /// The time the latest failed attempt was made.
        /// </summary>
        [JsonProperty]
        public DateTime LatestAttempt { get; set; } = DateTime.Now;
    }
}