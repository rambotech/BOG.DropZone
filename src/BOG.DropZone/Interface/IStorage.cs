using System;
using System.Collections.Generic;
using BOG.DropZone.Base;
using BOG.DropZone.Common.Dto;
using BOG.DropZone.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;

namespace BOG.DropZone.Interface
{
	/// <summary>
	/// Defines the site's common storage items.
	/// </summary>
	public interface IStorage
	{
		/// <summary>
		/// An optional access token value which the client must provide to use operational methods.
		/// </summary>
		string AccessToken { get; set; }

		/// <summary>
		/// An optional access token value which the client must provide to use administrative methods.
		/// </summary>
		string AdminToken { get; set; }

		/// <summary>
		/// The list of dropzone information
		/// </summary>
		int MaxDropzones { get; set; }

		/// <summary>
		/// The number of consecutive mismatched access token values which triggers a lockout period.
		/// </summary>
		int MaximumFailedAttemptsBeforeLockout { get; set; }

		/// <summary>
		/// The number of seconds which a lockout endures.
		/// </summary>
		int LockoutSeconds { get; set; }

		/// <summary>
		/// The list of dropzone information.
		/// </summary>
		Dictionary<string, DropPoint> DropZoneList { get; set; }

		/// <summary>
		/// The list of clients who have submitted invalid tokens for access.  Used to block IPs with too many failures.
		/// </summary>
		List<ClientWatch> ClientWatchList { get; set; }

        /// <summary>
        /// Launched by implementer after it has completed its initialization.
		/// The implementer needs to hydrate the memory objects with the existing values, when applicable.
        /// </summary>
        void Start();

        /// <summary>
        /// Launched by implementer after it has completed its initialization.
		/// The implementer needs to hydrate the memory objects with the existing values, when applicable.
        /// </summary>
        void LoadExisting();

        /// <summary>
        /// FIFO push item
        /// </summary>
        /// <param name="zoneName"></param>
        /// <param name="recipient">Use * for any recipient</param>
        /// <param name="tracking"></param>
        /// <param name="expiresOn"></param>
        /// <param name="payload"></param>
		/// <returns>Enum to semaphore handling</returns>
        RetrievedStorage PushToQueue(string zoneName, string recipient, string tracking, DateTime expiresOn, string payload);

        /// <summary>
		/// FIFO pull item for a specific recipient (* = global queue: default for null or whitepace string)
		/// </summary>
		/// <param name="zoneName"></param>
		/// <param name="recipient"></param>
		/// <returns>Enum to semaphore handling</returns>
        RetrievedStorage PullFromQueue(string zoneName, string recipient);

        /// <summary>
        /// Reads the content from a file, using the key as the file name.
        /// </summary>
        /// <param name="zoneName"></param>
        /// <param name="key"></param>
        /// <param name="defaultValue">A default value to return if the reference is not present.</param>
		/// <returns>Enum to semaphore handling</returns>
        RetrievedStorage GetReference(string zoneName, string key, string defaultValue);

        /// <summary>
        /// Creates a referece value under the key
        /// </summary>
        /// <param name="zoneName"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns>Enum to semaphore handling</returns>
        RetrievedStorage SaveReference(string zoneName, string key, string value);

        /// <summary>
        /// Deletes a reference with the key
        /// </summary>
        /// <param name="zoneName"></param>
        /// <param name="key"></param>
        /// <returns>Enum to semaphore handling</returns>
        RetrievedStorage DeleteReference(string zoneName, string key);

        /// <summary>
        /// Make a list of the blobs keys available in this zone.
        /// </summary>
        /// <param name="zoneName"></param>
        /// <returns>Enum to semaphore handling</returns>
        RetrievedStorage GetReferenceKeys(string zoneName);

        /// <summary>
        /// Reads the content from a file, using the key as the file name.
        /// </summary>
        /// <param name="zoneName"></param>
        /// <param name="key"></param>
        /// <param name="defaultValue">A default value to return if the file is not present.</param>
        /// <returns>Enum to semaphore handling</returns>
        RetrievedStorage ReadBlob(string zoneName, string key, string defaultValue);

        /// <summary>
        /// Creates a file with the content, using the key as the file name
        /// </summary>
        /// <param name="zoneName"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns>Enum to semaphore handling</returns>
        RetrievedStorage SaveBlob(string zoneName, string key, string value);

        /// <summary>
        /// Deletes a file with the content, using the key as the file name
        /// </summary>
        /// <param name="zoneName"></param>
        /// <param name="key"></param>
        /// <returns>Enum to semaphore handling</returns>
        RetrievedStorage DeleteBlob(string zoneName, string key);

        /// <summary>
        /// Make a list of the blobs keys available in this zone.
        /// </summary>
        /// <param name="zoneName"></param>
        /// <returns>Enum to semaphore handling</returns>
        RetrievedStorage GetBlobKeys(string zoneName);

        /// <summary>
        /// Reset the site to a fresh startup state.
        /// </summary>
        /// <returns>Enum to semaphore handling</returns>
        RetrievedStorage Reset();

        /// <summary>
        /// Reset only this dropzone to a fresh startup state.
        /// </summary>
        /// <param name="zoneName"></param>
        /// <returns>Enum to semaphore handling</returns>
        RetrievedStorage Clear(string dropZoneName);

        /// <summary>
        /// Shutdown the site using an application exit.
        /// </summary>
        /// <returns>Enum to semaphore handling</returns>
        RetrievedStorage Shutdown();

		/// <summary>
		/// Determines if the name is acceptable for processing
		/// </summary>
		/// <param name="zoneName"></param>
		/// <returns></returns>
		bool IsValidZoneName(string zoneName);

		/// <summary>
		/// Determines if the name is acceptable for processing
		/// </summary>
		/// <param name="key"></param>
		/// <returns></returns>
		bool IsValidKeyName(string key);
	}
}
