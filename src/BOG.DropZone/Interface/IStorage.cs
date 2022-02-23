using System.Collections.Generic;
using BOG.DropZone.Common.Dto;
using BOG.DropZone.Storage;

namespace BOG.DropZone.Interface
{
	/// <summary>
	/// Defines the site's common storage items.
	/// </summary>
	public interface IStorage
	{
		/// <summary>
		/// Communicates that state for a blog key
		/// </summary>
		enum BlobState : int { 
			/// <summary>
			/// Key not found in the persistent storage
			/// </summary>
			NotFound = 0,
			/// <summary>
			/// The key exists, and the content is valid.
			/// </summary>
			Exists = 1,
			/// <summary>
			/// The key exists, but the content is expired.
			/// </summary>
			Expired = 2
		};

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
		/// The list of dropzone information
		/// </summary>
		Dictionary<string, DropPoint> DropZoneList { get; set; }

		/// <summary>
		/// The list of clients who have submitted invalid 
		/// </summary>
		List<ClientWatch> ClientWatchList { get; set; }

		/// <summary>
		/// Reads the content from a file, using the key as the file name.
		/// </summary>
		/// <param name="zoneName"></param>
		/// <param name="key"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		BlobState ReadBlob(string zoneName, string key, out StoredValue value);

		/// <summary>
		/// Creates a file with the content, using the key as the file name
		/// </summary>
		/// <param name="zoneName"></param>
		/// <param name="key"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		BlobState SaveBlob(string zoneName, string key, StoredValue value);

		/// <summary>
		/// Deletes a file with the content, using the key as the file name
		/// </summary>
		/// <param name="zoneName"></param>
		/// <param name="key"></param>
		void DeleteBlob(string zoneName, string key);

		/// <summary>
		/// Reset the site to a fresh startup state.
		/// </summary>
		void Reset();

		/// <summary>
		/// Reset only this dropzone to a fresh startup state.
		/// </summary>
		void Clear(string dropZoneName);

		/// <summary>
		/// Shutdown the site using an application exit.
		/// </summary>
		void Shutdown();
	}
}
