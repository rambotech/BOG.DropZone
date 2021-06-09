﻿using System;
using System.Collections.Generic;
using System.Linq;
using BOG.DropZone.Common.Dto;
using BOG.DropZone.Interface;
using BOG.DropZone.Storage;
using BOG.DropZone.Client.Entity;
using BOG.SwissArmyKnife;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace BOG.DropZone.Controllers
{
	/// <summary>
	/// Provides all the client access points for the dropzones and admin functions.
	/// </summary>
	[Produces("text/plain")]
	[Route("api")]
	public class ApiController : Controller
	{
		private enum TokenType : int { Access = 0, Admin = 1 }

		private readonly IHttpContextAccessor _Accessor;
		private readonly IStorage _Storage;
		private readonly IAssemblyVersion _AssemblyVersion;

		private readonly object _LockClientWatchList = new();
		private readonly object _LockDropZoneInfo = new();

		/// <summary>
		/// Instantiated via injection
		/// </summary>
		/// <param name="accessor">(injected)</param>
		/// <param name="storage">(injected)</param>
		/// <param name="assemblyVersion">(injected)</param>
		public ApiController(IHttpContextAccessor accessor, IStorage storage, IAssemblyVersion assemblyVersion)
		{
			_Accessor = accessor;
			_Storage = storage;
			_AssemblyVersion = assemblyVersion;
		}

		/// <summary>
		/// Heartbeat check for clients.  No authorization header required.
		/// </summary>
		/// <returns>200 to confirm the API is active.</returns>
		[Route("heartbeat", Name = "Heartbeat")]
		[HttpGet]
		[RequestSizeLimit(1024)]
		[ProducesResponseType(200, Type = typeof(string))]
		[ProducesResponseType(500)]
		[Produces("application/json")]
		public IActionResult Heartbeat([FromHeader] string AccessToken)
		{
			var clientIp = _Accessor.HttpContext.Connection.RemoteIpAddress.ToString();
			if (!IsValidatedClient(clientIp, AccessToken, TokenType.Access, "*global*", System.Reflection.MethodBase.GetCurrentMethod().Name))
			{
				return Unauthorized();
			}
			return StatusCode(200, new JObject
			{
				{ "name", _AssemblyVersion.Name },
				{ "version", _AssemblyVersion.Version },
				{ "built", _AssemblyVersion.BuildDate.ToString("G") }
			}.ToString(Newtonsoft.Json.Formatting.Indented));
		}

		/// <summary>
		/// Deposit a payload to a drop zone
		/// </summary>
		/// <param name="AccessToken">Optional: access token value if used.</param>
		/// <param name="dropzoneName">the dropzone identifier</param>
		/// <param name="payload">the content to transfer</param>
		/// <param name="recipient">(optional): a specific identifier for a specfic  recipient of the payload.</param>
		/// <param name="tracking">(optional): a tracking key which can later be used to check if the payload was picked up.</param>
		/// <param name="expiresOn">(optional): when the value should no longer be returned.</param>
		/// <returns>varies: see method declaration</returns>
		[HttpPost("payload/dropoff/{dropzoneName}", Name = "DropoffPayload")]
		[ProducesResponseType(201, Type = typeof(string))]
		[ProducesResponseType(400, Type = typeof(string))]
		[ProducesResponseType(401)]
		[ProducesResponseType(429, Type = typeof(string))]
		[ProducesResponseType(500)]
		[Produces("text/plain")]
		public IActionResult DropoffPayload(
				[FromHeader] string AccessToken,
				[FromBody] string payload,
				[FromRoute] string dropzoneName,
				[FromQuery] string recipient = null,
				[FromQuery] string tracking = null,
				[FromQuery] DateTime expiresOn = default)
		{
			var recipientKey = string.IsNullOrWhiteSpace(recipient) ? "*" : recipient;
			var trackingKey = string.IsNullOrWhiteSpace(tracking) ? string.Empty : tracking;
			var expirationTime = expiresOn == default ? DateTime.MaxValue : expiresOn;
			var clientIp = _Accessor.HttpContext.Connection.RemoteIpAddress.ToString();
			if (!IsValidatedClient(clientIp, AccessToken, TokenType.Access, dropzoneName, System.Reflection.MethodBase.GetCurrentMethod().Name))
			{
				return Unauthorized();
			}
			lock (_LockDropZoneInfo)
			{
				if (!_Storage.DropZoneList.ContainsKey(dropzoneName))
				{
					if (_Storage.DropZoneList.Count >= _Storage.MaxDropzones)
					{
						return StatusCode(429, $"Can't create new dropzone {dropzoneName}.. at maximum of {_Storage.MaxDropzones} dropzone definitions.");
					}
					CreateDropZone(dropzoneName);
				}
				var dropzone = _Storage.DropZoneList[dropzoneName];
				// To ensure optimum use of memory, purge any entries
				if (dropzone.Statistics.PayloadSize + payload.Length > dropzone.Statistics.Metrics.MaxPayloadSize)
				{
					dropzone.Statistics.PayloadDropOffsFailedCount++;
					return StatusCode(429, $"Can't accept: Exceeds maximum payload size of {dropzone.Statistics.Metrics.MaxPayloadSize}");
				}
				if (dropzone.Statistics.PayloadCount + 1 > dropzone.Statistics.Metrics.MaxPayloadCount)
				{
					dropzone.Statistics.PayloadDropOffsFailedCount++;
					return StatusCode(429, $"Can't accept: Exceeds maximum payload count of {dropzone.Statistics.Metrics.MaxPayloadCount}");
				}
				dropzone.Statistics.PayloadSize += payload.Length;
				dropzone.Statistics.PayloadCount++;
				if (!dropzone.Payloads.ContainsKey(recipientKey))
				{
					dropzone.Payloads.TryAdd(recipientKey, new Dictionary<long, StoredValue>());
					dropzone.Statistics.Recipients.Add(recipientKey, 0);
				}
				dropzone.Statistics.Recipients[recipientKey]++;
				dropzone.Payloads[recipientKey].TryAdd(DateTime.Now.Ticks, new StoredValue
				{
					Value = payload,
					Expires = expirationTime,
					Tracking = trackingKey
				});
				dropzone.Statistics.LastDropoff = DateTime.Now;

				return StatusCode(201, "Payload accepted");
			}
		}

		/// <summary>
		/// Use the recipient and tracking number combination to determine if a payload, previously dropped off, 
		/// is still in the pickup area.  Optionally, a new expiration date can be establshed for the payload.
		/// </summary>
		/// <param name="AccessToken">Optional: access token value if used.</param>
		/// <param name="dropzoneName">the dropzone identifier</param>
		/// <param name="recipient">(optional): a specific identifier for a specfic recipient of the payload.</param>
		/// <param name="tracking">(optional): a tracking key which can later be used to check if the payload was picked up.</param>
		/// <param name="expireOn">(optional): an new expiration time to set for the payload.</param>
		/// <returns>200: PayloadInquir object with the results.</returns>
		/// <remarks>
		/// The same arguments must be used for recipient and tracking query parameters, as used when the payload was dropped off.
		/// Otherwise, unpredictble answers will ok.  The response is an empty text body: the HTTP Response code contins the answer.
		/// </remarks>
		[HttpGet("payload/inquiry/{dropzoneName}", Name = "PayloadInquiry")]
		[ProducesResponseType(200, Type = typeof(string))]
		[ProducesResponseType(401)]
		[ProducesResponseType(429, Type = typeof(string))]
		[ProducesResponseType(500)]
		[Produces("text/plain")]
		public IActionResult PayloadInquiry(
				[FromHeader] string AccessToken,
				[FromRoute] string dropzoneName,
				[FromQuery] string recipient = null,
				[FromQuery] string tracking = null,
				[FromQuery] string expireOn = null)
		{
			var recipientKey = string.IsNullOrWhiteSpace(recipient) ? "*" : recipient;
			var trackingKey = string.IsNullOrWhiteSpace(tracking) ? string.Empty : tracking;
			var expirationOn = DateTime.MinValue;
			if (!string.IsNullOrWhiteSpace(expireOn))
			{
				if (!DateTime.TryParse(expireOn, out expirationOn))
				{
					return BadRequest("expireOn query parameter is not a valid DateTime");
				}

			}
			var clientIp = _Accessor.HttpContext.Connection.RemoteIpAddress.ToString();
			if (!IsValidatedClient(clientIp, AccessToken, TokenType.Access, dropzoneName, System.Reflection.MethodBase.GetCurrentMethod().Name))
			{
				return Unauthorized();
			}
			lock (_LockDropZoneInfo)
			{
				if (!_Storage.DropZoneList.ContainsKey(dropzoneName))
				{
					if (_Storage.DropZoneList.Count >= _Storage.MaxDropzones)
					{
						return StatusCode(429, $"Can't create new dropzone {dropzoneName}.. at maximum of {_Storage.MaxDropzones} dropzone definitions.");
					}
					CreateDropZone(dropzoneName);
				}
				var result = new PayloadInquiry
				{
					Tracking = trackingKey
				};
				var dropzone = _Storage.DropZoneList[dropzoneName];
				if (dropzone.Payloads.ContainsKey(recipientKey))
				{
					var entry = dropzone.Payloads[recipientKey].Where(o => string.Compare(o.Value.Tracking, trackingKey, false) == 0).FirstOrDefault().Value;
					if (entry != null)
					{
						result.Found = true;
						result.Expiration = string.IsNullOrWhiteSpace(expireOn) ? entry.Expires : expirationOn;
					}
				}
				return StatusCode(200, Serializer<PayloadInquiry>.ToJson(result));
			}
		}

		/// <summary>
		/// Pickup a payload from a drop zone
		/// </summary>
		/// <param name="AccessToken">Optional: access token value if used.</param>
		/// <param name="dropzoneName">the dropzone identifier</param>
		/// <param name="recipient">(optional): retrieve only a payload intended for the specific recipient.</param>
		/// <returns>the data to transfer</returns>
		[HttpGet("payload/pickup/{dropzoneName}", Name = "PickupPayload")]
		[RequestSizeLimit(1024)]
		[Produces("text/plain")]
		[ProducesResponseType(200, Type = typeof(string))]
		[ProducesResponseType(204, Type = typeof(string))]
		[ProducesResponseType(400, Type = typeof(string))]
		[ProducesResponseType(401)]
		[ProducesResponseType(451)]
		[ProducesResponseType(429, Type = typeof(string))]
		[ProducesResponseType(500)]
		public IActionResult PickupPayload(
				[FromHeader] string AccessToken,
				[FromRoute] string dropzoneName,
				[FromQuery] string recipient = null)
		{
			var recipientKey = string.IsNullOrWhiteSpace(recipient) ? "*" : recipient;
			var clientIp = _Accessor.HttpContext.Connection.RemoteIpAddress.ToString();
			if (!IsValidatedClient(clientIp, AccessToken, TokenType.Access, dropzoneName, System.Reflection.MethodBase.GetCurrentMethod().Name))
			{
				return Unauthorized();
			}
			lock (_LockDropZoneInfo)
			{
				if (!_Storage.DropZoneList.ContainsKey(dropzoneName))
				{
					if (_Storage.DropZoneList.Count >= _Storage.MaxDropzones)
					{
						return StatusCode(429, $"Can't create new dropzone {dropzoneName}.. at maximum of {_Storage.MaxDropzones} dropzone definitions.");
					}
					CreateDropZone(dropzoneName);
				}
				var dropzone = _Storage.DropZoneList[dropzoneName];
				StoredValue payload = null;
				bool recipientKeyIsKnown = dropzone.Payloads.ContainsKey(recipientKey);
				bool payloadAvailable = false;

				// Keep cycling through the payloads for the recipient, in chronological order of posting,
				// until one is found which has not expired. This will drop any expired payloads for the recipient,
				// until a non-expired payload is encountered.
				while (!payloadAvailable)
				{
					var key = dropzone.Payloads[recipientKey].Keys.OrderBy(o => o).FirstOrDefault();
					if (key == 0L) break;  // list is empty
					payload = new StoredValue
					{
						Value = dropzone.Payloads[recipientKey][key].Value,
						Expires = dropzone.Payloads[recipientKey][key].Expires,
						Tracking = dropzone.Payloads[recipientKey][key].Tracking
					};
					dropzone.Payloads[recipientKey].Remove(key);

					dropzone.Statistics.PayloadSize -= payload.Value.Length;
					dropzone.Statistics.PayloadCount--;
					dropzone.Statistics.Recipients[recipientKey]--;
					if (payload.Expires < DateTime.Now)  // disqualified, toss and get next.
					{
						dropzone.Statistics.PayloadExpiredCount++;
						continue;
					}
					dropzone.Statistics.LastPickup = DateTime.Now;
					payloadAvailable = true;
				}
				if (!payloadAvailable)
				{
					return StatusCode(204);
				}
				return StatusCode(200, payload.Value);
			}
		}

		/// <summary>
		/// Get statistics for a dropzone (includes current metrics)
		/// </summary>
		/// <param name="AccessToken">Optional: access token value if used.</param>
		/// <param name="dropzoneName">the dropzone identifier</param>
		/// <returns>varies: see method declaration</returns>
		[HttpGet("statistics/{dropzoneName}", Name = "GetStatistics")]
		[RequestSizeLimit(1024)]
		[ProducesResponseType(400, Type = typeof(string))]
		[ProducesResponseType(401)]
		[ProducesResponseType(451)]
		[ProducesResponseType(200, Type = typeof(string))]
		[ProducesResponseType(429, Type = typeof(string))]
		[ProducesResponseType(500)]
		[Produces("text/plain")]
		public IActionResult GetStatistics(
				[FromHeader] string AccessToken,
				[FromRoute] string dropzoneName)
		{
			var clientIp = _Accessor.HttpContext.Connection.RemoteIpAddress.ToString();
			if (!IsValidatedClient(clientIp, AccessToken, TokenType.Access, dropzoneName, System.Reflection.MethodBase.GetCurrentMethod().Name))
			{
				return Unauthorized();
			}
			lock (_LockDropZoneInfo)
			{
				if (!_Storage.DropZoneList.ContainsKey(dropzoneName))
				{
					if (_Storage.DropZoneList.Count >= _Storage.MaxDropzones)
					{
						return StatusCode(429, $"Can't create new dropzone {dropzoneName}.. at maximum of {_Storage.MaxDropzones} dropzone definitions.");
					}
					CreateDropZone(dropzoneName);
				}
				return StatusCode(200, Serializer<DropZoneInfo>.ToJson(_Storage.DropZoneList[dropzoneName].Statistics));
			}
		}

		/// <summary>
		/// Update the metric values for a drop zone.
		/// </summary>
		/// <param name="AccessToken">Optional: access token value if used.</param>
		/// <param name="dropzoneName">the dropzone identifier</param>
		/// <param name="metrics">the max counts and max sizes of payloads and references.</param>
		/// <returns>varies: see method declaration</returns>
		[HttpPost("metrics/{dropzoneName}", Name = "SetMetrics")]
		[ProducesResponseType(201, Type = typeof(string))]
		[ProducesResponseType(400, Type = typeof(string))]
		[ProducesResponseType(401)]
		[ProducesResponseType(429, Type = typeof(string))]
		[ProducesResponseType(500)]
		[Produces("text/plain")]
		public IActionResult SetMetrics(
				[FromHeader] string AccessToken,
				[FromRoute] string dropzoneName,
				[FromBody] DropZoneMetrics metrics)
		{
			var clientIp = _Accessor.HttpContext.Connection.RemoteIpAddress.ToString();
			if (!IsValidatedClient(clientIp, AccessToken, TokenType.Access, dropzoneName, System.Reflection.MethodBase.GetCurrentMethod().Name))
			{
				return Unauthorized();
			}
			// vadlidate the metrics
			if (!metrics.IsValid())
			{
				return StatusCode(400, $"Invalid metrics: all values must zero or greater.");
			}
			lock (_LockDropZoneInfo)
			{
				if (!_Storage.DropZoneList.ContainsKey(dropzoneName))
				{
					if (_Storage.DropZoneList.Count >= _Storage.MaxDropzones)
					{
						return StatusCode(429, $"Can't create new dropzone {dropzoneName}.. at maximum of {_Storage.MaxDropzones} dropzone definitions.");
					}
					CreateDropZone(dropzoneName);
				}
				_Storage.DropZoneList[dropzoneName].Statistics.Metrics = metrics;
				return StatusCode(201, "Metrics accepted");
			}
		}

		/// <summary>
		/// Get security information for a dropzone
		/// </summary>
		/// <param name="AdminToken">Optional: admin token value if used.</param>
		/// <returns>varies: see method declaration</returns>
		[HttpGet("securityinfo", Name = "GetSecurityInfo")]
		[RequestSizeLimit(1024)]
		[ProducesResponseType(401)]
		[ProducesResponseType(451)]
		[ProducesResponseType(200, Type = typeof(string))]
		[ProducesResponseType(500)]
		[Produces("text/plain")]
		public IActionResult GetSecurityInfo([FromHeader] string AdminToken)
		{
			var clientIp = _Accessor.HttpContext.Connection.RemoteIpAddress.ToString();
			if (!IsValidatedClient(clientIp, AdminToken, TokenType.Admin, "*global*", System.Reflection.MethodBase.GetCurrentMethod().Name))
			{
				return Unauthorized();
			}
			lock (_LockClientWatchList)
			{
				return StatusCode(200, Serializer<List<ClientWatch>>.ToJson(_Storage.ClientWatchList));
			}
		}

		/// <summary>
		/// Sets the value of a reference key in a dropzone.Statistics.
		/// A reference is a key/value setting.  The value can any string, but cannot be null.
		/// Use DropReference to remove an entry in the references.
		/// </summary>
		/// <param name="AccessToken">Optional: access token value if used.</param>
		/// <param name="dropzoneName">the dropzone identifier</param>
		/// <param name="key">the name for the value to store</param>
		/// <param name="expires">(optional): when the value should no longer be returned.</param>
		/// <param name="value">the value to store for the key name</param>
		/// <returns>varies: see method declaration</returns>
		[HttpPost("reference/set/{dropzoneName}/{key}", Name = "SetReference")]
		[ProducesResponseType(400, Type = typeof(string))]
		[ProducesResponseType(401)]
		[ProducesResponseType(451)]
		[ProducesResponseType(201, Type = typeof(string))]
		[ProducesResponseType(429, Type = typeof(string))]
		[ProducesResponseType(500)]
		[Produces("text/plain")]
		public IActionResult SetReference(
				[FromHeader] string AccessToken,
				[FromRoute] string dropzoneName,
				[FromRoute] string key,
				[FromBody] string value,
				[FromQuery] DateTime? expires)
		{
			var clientIp = _Accessor.HttpContext.Connection.RemoteIpAddress.ToString();
			if (!IsValidatedClient(clientIp, AccessToken, TokenType.Access, dropzoneName, System.Reflection.MethodBase.GetCurrentMethod().Name))
			{
				return Unauthorized();
			}
			var fixedValue = new StoredValue
			{
				Value = value ?? string.Empty,
				Expires = expires ?? DateTime.MaxValue
			};

			lock (_LockDropZoneInfo)
			{
				if (!_Storage.DropZoneList.ContainsKey(dropzoneName))
				{
					if (_Storage.DropZoneList.Count >= _Storage.MaxDropzones)
					{
						return StatusCode(429, $"Can't create new dropzone {dropzoneName}.. at maximum of {_Storage.MaxDropzones} dropzone definitions.");
					}
					CreateDropZone(dropzoneName);
				}
				var dropzone = _Storage.DropZoneList[dropzoneName];
				int sizeOffset = 0;
				if (dropzone.References.ContainsKey(key))
				{
					sizeOffset = dropzone.References[key].Value.Length;
				}
				if ((dropzone.Statistics.ReferenceSize - sizeOffset + fixedValue.Value.Length) > dropzone.Statistics.Metrics.MaxReferenceSize)
				{
					dropzone.Statistics.ReferenceSetsFailedCount++;
					return StatusCode(429, $"Can't accept: Exceeds maximum reference value size of {dropzone.Statistics.Metrics.MaxReferenceSize}");
				}
				if (dropzone.References.Count >= dropzone.Statistics.Metrics.MaxReferencesCount)
				{
					dropzone.Statistics.ReferenceSetsFailedCount++;
					return StatusCode(429, $"Can't accept: Exceeds maximum reference count of {dropzone.Statistics.Metrics.MaxReferencesCount}");
				}
				dropzone.Statistics.ReferenceSize -= sizeOffset;
				dropzone.References.Remove(key);
				dropzone.References.Add(key, fixedValue);
				dropzone.Statistics.ReferenceSize += fixedValue.Value.Length;
				dropzone.Statistics.ReferenceCount = dropzone.References.Count;
				dropzone.Statistics.LastSetReference = DateTime.Now;
				return StatusCode(201, "Reference accepted");
			}
		}

		/// <summary>
		/// Gets the value of a reference key in a dropzone.Statistics.  A reference is a key/value setting.
		/// </summary>
		/// <param name="AccessToken">Optional: access token value if used.</param>
		/// <param name="dropzoneName">the dropzone identifier</param>
		/// <param name="key">the name identifying the value to retrieve</param>
		/// <returns>string which is the reference value (always text/plain)</returns>
		[HttpGet("reference/get/{dropzoneName}/{key}", Name = "GetReference")]
		[RequestSizeLimit(1024)]
		[ProducesResponseType(400, Type = typeof(string))]
		[ProducesResponseType(401)]
		[ProducesResponseType(451)]
		[ProducesResponseType(429, Type = typeof(string))]
		[ProducesResponseType(200, Type = typeof(string))]
		[ProducesResponseType(204, Type = typeof(string))]
		[ProducesResponseType(500)]
		[Produces("text/plain")]
		public IActionResult GetReference(
				[FromHeader] string AccessToken,
				[FromRoute] string dropzoneName,
				[FromRoute] string key)
		{
			var clientIp = _Accessor.HttpContext.Connection.RemoteIpAddress.ToString();
			if (!IsValidatedClient(clientIp, AccessToken, TokenType.Access, dropzoneName, System.Reflection.MethodBase.GetCurrentMethod().Name))
			{
				return Unauthorized();
			}
			lock (_LockDropZoneInfo)
			{
				if (!_Storage.DropZoneList.ContainsKey(dropzoneName))
				{
					if (_Storage.DropZoneList.Count >= _Storage.MaxDropzones)
					{
						return StatusCode(429, $"Can't create new dropzone {dropzoneName}.. at maximum of {_Storage.MaxDropzones} dropzone definitions.");
					}
					CreateDropZone(dropzoneName);
				}
				var dropzone = _Storage.DropZoneList[dropzoneName];
				string result = null;
				if (dropzone.References.Count == 0 || !dropzone.References.ContainsKey(key))
				{
					return StatusCode(204);
				}
				else if (dropzone.References[key].Expires <= DateTime.Now)
				{
					dropzone.References.Remove(key, out StoredValue ignored);
					dropzone.Statistics.ReferenceExpiredCount++;
					return StatusCode(204);
				}
				else
				{
					result = dropzone.References[key].Value;
				}
				dropzone.Statistics.LastGetReference = DateTime.Now;
				return Ok(result);
			}
		}

		/// <summary>
		/// Drops a reference key and its value in a dropzone, if the key is found.
		/// </summary>
		/// <param name="AccessToken">Optional: access token value if used.</param>
		/// <param name="dropzoneName">the dropzone identifier</param>
		/// <param name="key">the name for the value to store</param>
		/// <returns>varies: see method declaration</returns>
		[HttpDelete("reference/drop/{dropzoneName}/{key}", Name = "DropReference")]
		[ProducesResponseType(401)]
		[ProducesResponseType(451)]
		[ProducesResponseType(200, Type = typeof(string))]
		[ProducesResponseType(404, Type = typeof(string))]
		[ProducesResponseType(500)]
		[Produces("text/plain")]
		public IActionResult DropReference(
				[FromHeader] string AccessToken,
				[FromRoute] string dropzoneName,
				[FromRoute] string key)
		{
			var clientIp = _Accessor.HttpContext.Connection.RemoteIpAddress.ToString();
			if (!IsValidatedClient(clientIp, AccessToken, TokenType.Access, dropzoneName, System.Reflection.MethodBase.GetCurrentMethod().Name))
			{
				return Unauthorized();
			}

			lock (_LockDropZoneInfo)
			{
				if (!_Storage.DropZoneList.ContainsKey(dropzoneName))
				{
					return StatusCode(404, $"Can't find dropzone {dropzoneName}");
				}
				var dropzone = _Storage.DropZoneList[dropzoneName];
				if (dropzone.References.ContainsKey(key))
				{
					var len = dropzone.References[key].Value.Length;
					if (!dropzone.References.Remove(key, out var ignoreThis))
					{
						return StatusCode(500, $"Failed to remove reference");
					}
					dropzone.Statistics.ReferenceSize -= len;
				}
				dropzone.Statistics.ReferenceCount = dropzone.References.Count;
				dropzone.Statistics.LastSetReference = DateTime.Now;
				return StatusCode(200, "OK");
			}
		}

		/// <summary>
		/// Get a list of the reference key names available
		/// </summary>
		/// <param name="AccessToken">Optional: access token value if used.</param>
		/// <param name="dropzoneName">the dropzone identifier</param>
		/// <returns>list of strings which contain the reference key names</returns>
		[HttpGet("reference/list/{dropzoneName}", Name = "ListReferences")]
		[RequestSizeLimit(1024)]
		[ProducesResponseType(200, Type = typeof(List<string>))]
		[ProducesResponseType(400, Type = typeof(string))]
		[ProducesResponseType(401)]
		[ProducesResponseType(451)]
		[ProducesResponseType(404, Type = typeof(string))]
		[ProducesResponseType(500)]
		[Produces("application/json")]
		public IActionResult ListReferences(
				[FromHeader] string AccessToken,
				[FromRoute] string dropzoneName)
		{
			var clientIp = _Accessor.HttpContext.Connection.RemoteIpAddress.ToString();
			if (!IsValidatedClient(clientIp, AccessToken, TokenType.Access, dropzoneName, System.Reflection.MethodBase.GetCurrentMethod().Name))
			{
				return Unauthorized();
			}
			lock (_LockDropZoneInfo)
			{
				if (!_Storage.DropZoneList.ContainsKey(dropzoneName))
				{
					if (_Storage.DropZoneList.Count >= _Storage.MaxDropzones)
					{
						return StatusCode(429, $"Can't create new dropzone {dropzoneName}.. at maximum of {_Storage.MaxDropzones} dropzone definitions.");
					}
					CreateDropZone(dropzoneName);
				}
				var dropzone = _Storage.DropZoneList[dropzoneName];
				List<string> returnList = new();
				if (dropzone.References.Count == 0)
				{
					returnList = _Storage.DropZoneList[dropzoneName].References
					.Where(o => o.Value.Expires > DateTime.Now)
					.Select(o => o.Key).ToList();
					returnList.Sort();
				}
				return Ok(returnList);
			}
		}

		/// <summary>
		/// Clear: drops all payloads and references from a specific drop zone, and resets its statistics.
		/// </summary>
		/// <param name="AdminToken">Optional: admin token value if used.</param>
		/// <param name="dropzoneName">the dropzone identifier</param>
		/// <returns>string</returns>
		[HttpGet("clear/{dropzoneName}", Name = "Clear")]
		[RequestSizeLimit(1024)]
		[ProducesResponseType(200, Type = typeof(string))]
		[ProducesResponseType(401)]
		[ProducesResponseType(451)]
		[ProducesResponseType(500)]
		[Produces("text/plain")]
		public IActionResult Clear(
				[FromHeader] string AdminToken,
				[FromRoute] string dropzoneName)
		{
			var clientIp = _Accessor.HttpContext.Connection.RemoteIpAddress.ToString();
			if (!IsValidatedClient(clientIp, AdminToken, TokenType.Admin, dropzoneName, System.Reflection.MethodBase.GetCurrentMethod().Name))
			{
				return Unauthorized();
			}
			lock (_LockDropZoneInfo)
			{
				if (_Storage.DropZoneList.ContainsKey(dropzoneName))
				{
					ReleaseDropZone(dropzoneName);
				}
			}
			return Ok($"drop zone {dropzoneName} cleared");
		}

		/// <summary>
		/// Reset: clear all drop zones and their data.  Essentially a soft boot.
		/// </summary>
		/// <param name="AdminToken">Optional: admin token value if used.</param>
		/// <returns>string</returns>
		[HttpGet("reset", Name = "Reset")]
		[RequestSizeLimit(1024)]
		[ProducesResponseType(200, Type = typeof(string))]
		[ProducesResponseType(401)]
		[ProducesResponseType(451)]
		[ProducesResponseType(500)]
		[Produces("text/plain")]
		public IActionResult Reset([FromHeader] string AdminToken)
		{
			var clientIp = _Accessor.HttpContext.Connection.RemoteIpAddress.ToString();
			if (!IsValidatedClient(clientIp, AdminToken, TokenType.Admin, "*global*", System.Reflection.MethodBase.GetCurrentMethod().Name))
			{
				return Unauthorized();
			}
			lock (_LockDropZoneInfo)
			{
				_Storage.Reset();
			}
			return Ok("all drop zone data cleared");
		}

		/// <summary>
		/// Shutdown: clear all drop zones and their data
		/// </summary>
		/// <param name="AdminToken">Optional: admin token value if used.</param>
		/// <returns>string</returns>
		[HttpGet("shutdown", Name = "Shutdown")]
		[RequestSizeLimit(1024)]
		[ProducesResponseType(200, Type = typeof(string))]
		[ProducesResponseType(401)]
		[ProducesResponseType(451)]
		[ProducesResponseType(500)]
		[Produces("text/plain")]
		public IActionResult Shutdown([FromHeader] string AdminToken)
		{
			var clientIp = _Accessor.HttpContext.Connection.RemoteIpAddress.ToString();
			if (!IsValidatedClient(clientIp, AdminToken, TokenType.Admin, "*global*", System.Reflection.MethodBase.GetCurrentMethod().Name))
			{
				return Unauthorized();
			}
			_Storage.Shutdown();
			return Ok("Shutdown requested... bye");
		}

		#region Helper Methods

		// Adds the new drop zone to the memory storage tracker.
		private void CreateDropZone(string dropzoneName)
		{
			_Storage.DropZoneList.Add(dropzoneName, new DropPoint());
			_Storage.DropZoneList[dropzoneName].Statistics.Name = dropzoneName;
		}

#if FALSE
		// Prunes the expired payloads in a dropzone.  Assumed to be operating within a lock() block.
		private void PruneDropZone(string dropzoneName)
		{
			if (!_Storage.DropZoneList.ContainsKey(dropzoneName)) return;
			foreach (var recipient in _Storage.DropZoneList[dropzoneName].Payloads.Keys)
			{
 				foreach (var tickValue in _Storage.DropZoneList[dropzoneName].Payloads[recipient].Keys.Where(o=> o < DateTime.Now.Ticks).ToList())
				{
					_Storage.DropZoneList[dropzoneName].Payloads[recipient].Remove(tickValue);
				}
			}
		}
#endif

		private void ReleaseDropZone(string dropzoneName)
		{
			_Storage.DropZoneList.Remove(dropzoneName);
		}

		// Validates that the auth token provided is valid, and tracks failures.
		// NOTE:
		//     This method will return false when a valid access token is provided, but the client has exceeded
		//     the maximum allowed attempts within a timeframe (i.e. is in a lockdown period).  This protects 
		//     against brute force attacks, because the attacker can believe that it has not yet discovered the
		//     correct value.
		private bool IsValidatedClient(string ipAddress, string tokenValue, TokenType tokenType, string dropzoneName, string apiMethodName)
		{
			lock (_LockClientWatchList)
			{
				var configuredTokenValue = tokenType == TokenType.Access ? _Storage.AccessToken : _Storage.AdminToken;
				var validAuthToken = string.Compare(tokenValue ?? string.Empty, configuredTokenValue ?? string.Empty, false) == 0;
				var allowAccess = validAuthToken;
				var clientInfo = _Storage.ClientWatchList.Where(t => t.IpAddress == ipAddress).FirstOrDefault();
				if (clientInfo == null)
				{
					_Storage.ClientWatchList.Add(new ClientWatch
					{
						IpAddress = ipAddress
					});
					clientInfo = _Storage.ClientWatchList.Where(t => t.IpAddress == ipAddress).First();
				}

				clientInfo.LatestAttempt = DateTime.Now;
				clientInfo.AccessAttemptsTotalCount++;
				if (!clientInfo.AccessPoints.ContainsKey(dropzoneName))
				{
					clientInfo.AccessPoints.Add(dropzoneName, new Dictionary<string, long>());
				}
				if (!clientInfo.AccessPoints[dropzoneName].ContainsKey(apiMethodName))
				{
					clientInfo.AccessPoints[dropzoneName].Add(apiMethodName, 0);
				}
				clientInfo.AccessPoints[dropzoneName][apiMethodName]++;

				if (!validAuthToken)
				{
					clientInfo.FailedAccessAttemptsTotalCount++;
					if (clientInfo.FailedAccessTimes.Count == _Storage.MaximumFailedAttemptsBeforeLockout)
					{
						clientInfo.FailedAccessTimes.Dequeue();
					}
					clientInfo.FailedAccessTimes.Enqueue(DateTime.Now);
				}

				// prune out failed attempts that have expired.
				while (clientInfo.FailedAccessTimes.Count > 0)
				{
					var thisAttemptTime = clientInfo.FailedAccessTimes.Peek();
					if (thisAttemptTime.AddSeconds(_Storage.LockoutSeconds) < DateTime.Now)
					{
						clientInfo.FailedAccessTimes.Dequeue();
						continue;
					}
					break;
				}

				allowAccess &= (clientInfo.FailedAccessTimes.Count < _Storage.MaximumFailedAttemptsBeforeLockout);
				return allowAccess;
			}
		}
		#endregion
	}
}