using BOG.DropZone.Common.Dto;
using BOG.DropZone.Interface;
using BOG.DropZone.Storage;
using Microsoft.DotNet.Scaffolding.Shared;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Timers;

namespace BOG.DropZone.Base
{
    public class StorageBase : IStorage
    {
        const string ZoneNamePattern = @"^[A-Za-z][A-Za-z0-9_\-\.]{0,58}[A-Za-z0-9\.]$";
        const string KeyNamePattern = @"^[A-Za-z][A-Za-z0-9_\-\.]{0,58}[A-Za-z0-9\.]$";

        private readonly IPersistProvider _provider;

        readonly Timer stopTimer = new Timer();
        readonly object lockPoint = new object();

        /// <summary>
        /// If not empty, the header value "AccessToken" from the client must contain this value to use the site.
        /// </summary>
        public string AccessToken { get; set; } = string.Empty;

        /// <summary>
        /// An optional access token value which the client must provide to use administrative methods.
        /// </summary>
        public string AdminToken { get; set; } = string.Empty;

        /// <summary>
        /// The maximum number of dropzones to allow.
        /// </summary>
        public int MaxDropzones { get; set; } = 10;

        /// <summary>
        /// The maximum number of failed attempts before a lockout delay is invoked.
        /// </summary>
        public int MaximumFailedAttemptsBeforeLockout { get; set; } = 6;

        /// <summary>
        /// The duration of the lock out when imposed.
        /// </summary>
        public int LockoutSeconds { get; set; } = 600;

        /// <summary>
        /// The collection of drop zones and their data.
        /// </summary>
        public Dictionary<string, DropPoint> DropZoneList { get; set; } = new Dictionary<string, DropPoint>();

        /// <summary>
        /// The list of clients who have submitted invalid 
        /// </summary>
        public List<ClientWatch> ClientWatchList { get; set; } = new List<ClientWatch>();

        private readonly string PersistBaseFolder;

        /// <summary>
        /// Constructor.
        /// </summary>
        public StorageBase(IConfiguration config, IPersistProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            PersistBaseFolder = config.GetValue<string>(
                    "PersistFolderRoot",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "app.data", "local", "dropzones")
            );
            if (!Directory.Exists(PersistBaseFolder))
            {
                Directory.CreateDirectory(PersistBaseFolder);
            }
        }

        /// <summary>
        /// Launched by implementer after it has completed its initialization.
        /// </summary>
        public void Start()
        {
            stopTimer.Enabled = false;
            stopTimer.Elapsed += new ElapsedEventHandler(OnTimedEvent);
            stopTimer.Interval = 1000;
        }

        /// <summary>
        /// Shutdowns down the web server, requiring restart at the command line.
        /// When running as a service, the service normally will restart it.
        /// </summary>
        public void Shutdown()
        {
            // triggers a timer, which does the actual shutdown after the thread is in an idle state.
            stopTimer.Enabled = true;
        }

        /// <summary>
        /// Ensure the patten is valid.
        /// </summary>
        /// <param name="zoneName"></param>
        /// <returns></returns>
        public bool IsValidZoneName(string zoneName)
        {
            return new Regex(ZoneNamePattern, RegexOptions.IgnoreCase).IsMatch(zoneName);
        }

        /// <summary>
        /// Ensure the patten is valid.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool IsValidKeyName(string key)
        {
            return new Regex(KeyNamePattern, RegexOptions.IgnoreCase).IsMatch(key);
        }

        /// <summary>
        /// FIFO push item
        /// </summary>
        /// <param name="zoneName"></param>
        /// <param name="recipient">Use * for any recipient</param>
        /// <param name="tracking"></param>
        /// <param name="expiresOn"></param>
        /// <param name="payload"></param>
		/// <returns>Enum to semaphore handling</returns>
        RetrievedStorage PushToQueue(string zoneName, string recipient, string tracking, DateTime expiresOn, string payload)
        {
            throw new NotImplementedException();
        }

        /// <summary>
		/// FIFO pull item for a specific recipient (* = global queue: default for null or whitepace string)
		/// </summary>
		/// <param name="zoneName"></param>
		/// <param name="recipient"></param>
		/// <param name="payload"></param>
		/// <returns>Enum to semaphore handling</returns>
        RetrievedStorage PullFromQueue(string zoneName, string recipient)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Make a list of the blobs keys available in this zone.
        /// </summary>
        /// <param name="zoneName"></param>
        /// <returns>Enum to semaphore handling</returns>
        RetrievedStorage GetBlobKeys(string zoneName)
        {
            var items = new List<string>();

            if (IsValidZoneName(zoneName))
            {
                var zoneFolder = Path.Combine(PersistBaseFolder, zoneName);
                if (Directory.Exists(zoneFolder))
                {
                    foreach (var filename in Directory.GetFiles(zoneFolder, MakeBlobFilename("*"), SearchOption.TopDirectoryOnly))
                    {
                        var filenameOnly = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(filename));
                        items.Add(filenameOnly);
                    }
                }
            }
            return new RetrievedStorage
            {
                Result = Enums.StorageResult.Success,
                Payload = (object)items
            };
        }

        /// <summary>
        /// Reads the content from a file, using the key as the file name.
        /// </summary>
        /// <param name="zoneName"></param>
        /// <param name="key"></param>
        /// <param name="value">A default value to return if the file is not present.</param>
        /// <returns></returns>
        public string ReadBlob(string zoneName, string key, string value)
        {
            if (!IsValidZoneName(zoneName)) return null;
            if (!IsValidZoneName(key)) return null;
            var zoneFolder = Path.Combine(PersistBaseFolder, zoneName);
            if (!Directory.Exists(zoneFolder)) return string.Empty;

            var filename = Path.Combine(zoneFolder, MakeBlobFilename(key));
            if (!File.Exists(filename)) return string.Empty;
            using StreamReader sr = new StreamReader(filename);
            return sr.ReadToEnd();
        }

        /// <summary>
        /// Creates a file with the content, using the key as the file name
        /// </summary>
        /// <param name="zoneName"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public void SaveBlob(string zoneName, string key, string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            if (!IsValidZoneName(zoneName)) return;
            if (!IsValidZoneName(key)) return;
            var zoneFolder = Path.Combine(PersistBaseFolder, zoneName);
            if (!Directory.Exists(zoneFolder)) Directory.CreateDirectory(zoneFolder);

            var filename = Path.Combine(zoneFolder, MakeBlobFilename(key));
            using StreamWriter sw = File.CreateText(filename);
            sw.Write(value);
            sw.Close();
        }

        /// <summary>
        /// Deletes a file containing the content, using the key as the file name
        /// </summary>
        /// <param name="zoneName"></param>
        /// <param name="key"></param>
        public void DeleteBlob(string zoneName, string key)
        {
            if (!IsValidZoneName(zoneName)) return;
            if (!IsValidZoneName(key)) return;
            var zoneFolder = Path.Combine(PersistBaseFolder, zoneName);
            if (!Directory.Exists(zoneFolder)) return;

            var filename = Path.Combine(zoneFolder, MakeBlobFilename(key));
            if (!File.Exists(filename)) return;

            File.Delete(filename);
        }

        protected void ResetCommon()
        {
            foreach (var zoneName in DropZoneList.Keys)
            {
                Clear(zoneName);
            }
            DropZoneList.Clear();
            ClientWatchList.Clear();
        }

        /// <summary>
        /// Clear all drop zones, including their payloads and reference dictionary.
        /// Implementations using other than memory should first do their actions, then
        /// call base.ResetCommon().
        /// </summary>
        public virtual void Reset()
        {
            ResetCommon();
        }

        protected void ClearCommon(string zoneName)
        {
            lock (lockPoint)
            {
                foreach (var clientEntry in ClientWatchList)
                {
                    if (clientEntry.AccessPoints.ContainsKey(zoneName))
                    {
                        clientEntry.AccessPoints.Remove(zoneName);
                    }
                }
            }
        }

        /// <summary>
        /// Clear a specific drop zone, including their payloads and reference dictionary.  Will not touch blobs.
        /// Implementations using other than memory should first do their actions, then
        /// call base.ClearCommon().
        /// </summary>
        public virtual void Clear(string zoneName)
        {
            ClearCommon(zoneName);
        }

        private static void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            SaveObjects();
            System.Environment.Exit(0);
        }

        private static string MakeBlobFilename(string rootname)
        {
            return rootname.Trim() + ".blob.json";
        }
    }
}
