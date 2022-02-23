using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Timers;
using BOG.DropZone.Common.Dto;
using BOG.DropZone.Interface;
using BOG.DropZone.Storage;
using BOG.SwissArmyKnife.Extensions;
using Microsoft.Extensions.Configuration;
using BOG.SwissArmyKnife;
using static BOG.DropZone.Interface.IStorage;

namespace BOG.DropZone
{
	/// <summary>
	/// The collection of items persisted on the server, and static across calls.
	/// </summary>
	public class MemoryStorage : IStorage
	{
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
		public MemoryStorage(IConfiguration config)
		{
			PersistBaseFolder = ResolveLocalSpec(config.GetValue<string>("PersistFolderRoot", "$HOME/appdata/dropzone"));
			if (!Directory.Exists(PersistBaseFolder))
			{
				Directory.CreateDirectory(PersistBaseFolder);
			}
			stopTimer.Enabled = false;
			stopTimer.Elapsed += new ElapsedEventHandler(OnTimedEvent);
			stopTimer.Interval = 1000;
		}

		private string ResolveLocalSpec(string localFolderSpec)
		{
			string result = localFolderSpec;
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				result = result.Replace(@"/", @"\").ResolvePathPlaceholders();
				result = result.Replace("$HOME", Environment.GetEnvironmentVariable("USERPROFILE"));
				while (result[result.Length - 1] == '\\') result = result.Substring(0, result.Length - 1);
				result += "\\";
			}
			else
			{
				result = result.Replace("$HOME", Environment.GetEnvironmentVariable("HOME"));
				while (result[result.Length - 1] == '/') result = result.Substring(0, result.Length - 1);
				result += "/";
			}
			return result;
		}

		/// <summary>
		/// Reads the content from a file, using the key as the file name.
		/// </summary>
		/// <param name="zoneName"></param>
		/// <param name="key"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public BlobState ReadBlob(string zoneName, string key, out StoredValue value)
		{
			value = null;
			var zoneFolder = Path.Combine(PersistBaseFolder, zoneName);
			if (!Directory.Exists(zoneFolder)) return BlobState.NotFound;

			var filename = Path.Combine(zoneFolder, key);
			value = ObjectJsonSerializer<StoredValue>.LoadDocumentFormat(filename);
			return BlobState.Exists;
		}

		/// <summary>
		/// Creates a file with the content, using the key as the file name
		/// </summary>
		/// <param name="zoneName"></param>
		/// <param name="key"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public BlobState SaveBlob(string zoneName, string key, StoredValue value)
		{
			var zoneFolder = Path.Combine(PersistBaseFolder, zoneName);
			if (!Directory.Exists(zoneFolder)) Directory.CreateDirectory(zoneFolder);

			var filename = Path.Combine(zoneFolder, key);
			ObjectJsonSerializer<StoredValue>.SaveDocumentFormat(value, filename);
			return BlobState.Exists;
		}

		/// <summary>
		/// Deletes a file containing the content, using the key as the file name
		/// </summary>
		/// <param name="zoneName"></param>
		/// <param name="key"></param>
		public void DeleteBlob(string zoneName, string key)
		{
			var zoneFolder = Path.Combine(PersistBaseFolder, zoneName);
			if (!Directory.Exists(zoneFolder)) return;

			var filename = Path.Combine(zoneFolder, key);
			if (!File.Exists(filename)) return;

			File.Delete(filename);
		}

		/// <summary>
		/// Clear all drop zones, including their payloads and reference dictionary.
		/// </summary>
		public void Reset()
		{
			foreach (var zoneName in DropZoneList.Keys)
			{
				Clear(zoneName);
			}
			DropZoneList.Clear();
			ClientWatchList.Clear();
		}

		/// <summary>
		/// Clear a specific drop zone, including their payloads and reference dictionary.
		/// </summary>
		public void Clear(string zoneName)
		{
			var zoneFolder = Path.Combine(PersistBaseFolder, zoneName);
			if (Directory.Exists(zoneFolder)) Directory.Delete(zoneFolder);
			DropZoneList.Remove(zoneName);
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
