using System;
using System.Collections.Generic;
using System.Linq;
using BOG.DropZone.Common;
using BOG.DropZone.Interface;
using BOG.DropZone.Storage;
using BOG.SwissArmyKnife;
using BOG.DropZone.Common.Dto;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace BOG.DropZone.Controllers
{
    /// <summary>
    /// Provides all the client access points for the dropzones and admin functions.
    /// </summary>
    [Produces("text/plain")]
    [Route("api")]
    public class ApiController : Controller
    {
        private const int MaxDropzones = 10;

        private IHttpContextAccessor _accessor;
        private IStorage _storage;
        private IConfiguration _configuration;

        private Stopwatch upTime = new Stopwatch();
        private Dictionary<string, DateTime> BlacklistedClients = new Dictionary<string, DateTime>();

        private object LockBlacklist = new object();
        private object LockAuthTokenFailList = new object();

        /// <summary>
        /// Instantiated via injection
        /// </summary>
        /// <param name="accessor">(injected)</param>
        /// <param name="storage">(injected)</param>
        /// <param name="configuration">(injected)</param>
        public ApiController(IHttpContextAccessor accessor, IStorage storage, IConfiguration configuration)
        {
            _accessor = accessor;
            _storage = storage;
            _configuration = configuration;
        }

        /// <summary>
        /// Heartbeat check for clients
        /// </summary>
        /// <returns>varies: see method declaration</returns>
        [HttpGet("heartbeat", Name = "Heartbeat")]
        [ProducesResponseType(200, Type = typeof(string))]
        [ProducesResponseType(401)]
        [ProducesResponseType(451)]
        [ProducesResponseType(500)]
        [Produces("text/plain")]
        public IActionResult Heartbeat([FromHeader] string AccessToken)
        {
            if (IsClientBlacklisted(_accessor.HttpContext.Connection.RemoteIpAddress.ToString()))
            {
                return StatusCode(451, "You are not playing nice.");
            }
            if (string.Compare(AccessToken ?? string.Empty, _storage.AccessToken ?? string.Empty, false) != 0)
            {
                return HandleFailedAuthTokenValue(_accessor.HttpContext.Connection.RemoteIpAddress.ToString(), string.Empty, "Heartbeat");
            }
            return StatusCode(200, "Active");
        }

        /// <summary>
        /// Deposit a payload to a drop zone
        /// </summary>
        /// <param name="AccessToken">Optional: access token value if used.</param>
        /// <param name="dropzoneName">the dropzone identifier</param>
        /// <param name="payload">the content to transfer</param>
        /// <returns>varies: see method declaration</returns>
        [HttpPost("payload/dropoff/{dropzoneName}", Name = "DropoffPayload")]
        [ProducesResponseType(200, Type = typeof(string))]
        [ProducesResponseType(400, Type = typeof(string))]
        [ProducesResponseType(401)]
        [ProducesResponseType(451)]
        [ProducesResponseType(429, Type = typeof(string))]
        [ProducesResponseType(500)]
        [Produces("text/plain")]
        public IActionResult DropoffPayload(
            [FromHeader] string AccessToken,
            [FromRoute] string dropzoneName,
            [FromBody] string payload)
        {
            if (IsClientBlacklisted(_accessor.HttpContext.Connection.RemoteIpAddress.ToString()))
            {
                return StatusCode(451, "You are not playing nice.");
            }
            if (string.Compare(AccessToken ?? string.Empty, _storage.AccessToken ?? string.Empty, false) != 0)
            {
                return HandleFailedAuthTokenValue(_accessor.HttpContext.Connection.RemoteIpAddress.ToString(), dropzoneName, "DropoffPayload");
            }
            if (!_storage.DropZoneList.ContainsKey(dropzoneName))
            {
                if (_storage.DropZoneList.Count >= MaxDropzones)
                {
                    return StatusCode(429, $"Can't create new dropzone {dropzoneName}.. at maximum of {MaxDropzones} dropzone definitions.");
                }
                CreateDropZone(dropzoneName);
            }
            var dropzone = _storage.DropZoneList[dropzoneName];
            if (dropzone.Statistics.PayloadSize + payload.Length > dropzone.Statistics.MaxPayloadSize)
            {
                dropzone.Statistics.PayloadDropOffsFailedCount++;
                return StatusCode(429, $"Can't accept: Exceeds maximum payload size of {dropzone.Statistics.MaxPayloadSize}");
            }
            if (dropzone.Payloads.Count >= dropzone.Statistics.MaxPayloadCount)
            {
                dropzone.Statistics.PayloadDropOffsFailedCount++;
                return StatusCode(429, $"Can't accept: Exceeds maximum payload count of {dropzone.Statistics.MaxPayloadCount}");
            }
            dropzone.Statistics.PayloadSize += payload.Length;
            dropzone.Payloads.Enqueue(payload);
            dropzone.Statistics.PayloadCount = dropzone.Payloads.Count();
            dropzone.Statistics.LastDropoff = DateTime.Now;
            return StatusCode(200, "Payload accepted");
        }

        /// <summary>
        /// Pickup a payload from a drop zone
        /// </summary>
        /// <param name="AccessToken">Optional: access token value if used.</param>
        /// <param name="dropzoneName">the dropzone identifier</param>
        /// <returns>the data to transfer</returns>
        [HttpGet("payload/pickup/{dropzoneName}", Name = "PickupPayload")]
        [Produces("text/plain")]
        [ProducesResponseType(200, Type = typeof(string))]
        [ProducesResponseType(204, Type = typeof(string))]
        [ProducesResponseType(400, Type = typeof(string))]
        [ProducesResponseType(401)]
        [ProducesResponseType(451)]
        [ProducesResponseType(410, Type = typeof(string))]
        [ProducesResponseType(429, Type = typeof(string))]
        [ProducesResponseType(500)]
        public IActionResult PickupPayload(
            [FromHeader] string AccessToken,
            [FromRoute] string dropzoneName)
        {
            if (IsClientBlacklisted(_accessor.HttpContext.Connection.RemoteIpAddress.ToString()))
            {
                return StatusCode(451, "You are not playing nice.");
            }
            if (string.Compare(AccessToken ?? string.Empty, _storage.AccessToken ?? string.Empty, false) != 0)
            {
                return HandleFailedAuthTokenValue(_accessor.HttpContext.Connection.RemoteIpAddress.ToString(), string.Empty, "PickupPayload");
            }
            if (!_storage.DropZoneList.ContainsKey(dropzoneName))
            {
                if (_storage.DropZoneList.Count >= MaxDropzones)
                {
                    return StatusCode(429, $"Can't create new dropzone {dropzoneName}.. at maximum of {MaxDropzones} dropzone definitions.");
                }
                CreateDropZone(dropzoneName);
            }
            var dropzone = _storage.DropZoneList[dropzoneName];
            if (dropzone.Payloads.Count == 0)
            {
                return StatusCode(204);
            }

            if (!dropzone.Payloads.TryDequeue(out string payload))
            {
                return StatusCode(410, $"Dropzone exists with payloads, but failed to acquire a payload");
            }
            dropzone.Statistics.PayloadSize -= payload.Length;
            dropzone.Statistics.PayloadCount = dropzone.Payloads.Count();
            dropzone.Statistics.LastPickup = DateTime.Now;
            return StatusCode(200, payload);
        }

        /// <summary>
        /// Get statistics for a dropzone
        /// </summary>
        /// <param name="AccessToken">Optional: access token value if used.</param>
        /// <param name="dropzoneName">the dropzone identifier</param>
        /// <returns>varies: see method declaration</returns>
        [HttpGet("payload/statistics/{dropzoneName}", Name = "GetStatistics")]
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
            if (IsClientBlacklisted(_accessor.HttpContext.Connection.RemoteIpAddress.ToString()))
            {
                return StatusCode(451, "You are not playing nice.");
            }
            if (string.Compare(AccessToken ?? string.Empty, _storage.AccessToken ?? string.Empty, false) != 0)
            {
                return HandleFailedAuthTokenValue(_accessor.HttpContext.Connection.RemoteIpAddress.ToString(), string.Empty, "GetStatistics");
            }
            if (!_storage.DropZoneList.ContainsKey(dropzoneName))
            {
                if (_storage.DropZoneList.Count >= MaxDropzones)
                {
                    return StatusCode(429, $"Can't create new dropzone {dropzoneName}.. at maximum of {MaxDropzones} dropzone definitions.");
                }
                CreateDropZone(dropzoneName);
            }
            return StatusCode(200, Serializer<DropZoneInfo>.ToJson(_storage.DropZoneList[dropzoneName].Statistics));
        }

        /// <summary>
        /// Get security information for a dropzone
        /// </summary>
        /// <param name="AccessToken">Optional: access token value if used.</param>
        /// <returns>varies: see method declaration</returns>
        [HttpGet("securityinfo", Name = "GetSecurityInfo")]
        [ProducesResponseType(401)]
        [ProducesResponseType(451)]
        [ProducesResponseType(200, Type = typeof(string))]
        [ProducesResponseType(500)]
        [Produces("text/plain")]
        public IActionResult GetSecurityInfo(
            [FromHeader] string AccessToken)
        {
            if (IsClientBlacklisted(_accessor.HttpContext.Connection.RemoteIpAddress.ToString()))
            {
                return StatusCode(451, "You are not playing nice.");
            }
            if (string.Compare(AccessToken ?? string.Empty, _storage.AccessToken ?? string.Empty, false) != 0)
            {
                return HandleFailedAuthTokenValue(_accessor.HttpContext.Connection.RemoteIpAddress.ToString(), string.Empty, "GetSecurityStatistics");
            }
            return StatusCode(200, Serializer<List<FailedAuthTokenWatch>>.ToJson(_storage.FailedAuthTokenWatchList));
        }

        /// <summary>
        /// Sets the value of a reference key in a dropzone.Statistics.  A reference is a key/value setting.
        /// </summary>
        /// <param name="AccessToken">Optional: access token value if used.</param>
        /// <param name="dropzoneName">the dropzone identifier</param>
        /// <param name="key">the name for the value to store</param>
        /// <param name="value">the value to store for the key name</param>
        /// <returns>varies: see method declaration</returns>
        [HttpPost("reference/set/{dropzoneName}/{key}", Name = "SetReference")]
        [Produces("text/plain")]
        [ProducesResponseType(400, Type = typeof(string))]
        [ProducesResponseType(401)]
        [ProducesResponseType(451)]
        [ProducesResponseType(200, Type = typeof(string))]
        [ProducesResponseType(429, Type = typeof(string))]
        [ProducesResponseType(500)]
        public IActionResult SetReference(
            [FromHeader] string AccessToken,
            [FromRoute] string dropzoneName,
            [FromRoute] string key,
            [FromBody] string value)
        {
            if (IsClientBlacklisted(_accessor.HttpContext.Connection.RemoteIpAddress.ToString()))
            {
                return StatusCode(451, "You are not playing nice.");
            }
            if (string.Compare(AccessToken ?? string.Empty, _storage.AccessToken ?? string.Empty, false) != 0)
            {
                return HandleFailedAuthTokenValue(_accessor.HttpContext.Connection.RemoteIpAddress.ToString(), string.Empty, "SetReference");
            }
            var fixedValue = value ?? string.Empty;
            if (!_storage.DropZoneList.ContainsKey(dropzoneName))
            {
                if (_storage.DropZoneList.Count >= MaxDropzones)
                {
                    return StatusCode(429, $"Can't create new dropzone {dropzoneName}.. at maximum of {MaxDropzones} dropzone definitions.");
                }
                CreateDropZone(dropzoneName);
            }
            var dropzone = _storage.DropZoneList[dropzoneName];
            int sizeOffset = 0;
            if (dropzone.References.ContainsKey(key))
            {
                sizeOffset = dropzone.References[key].Length;
            }
            if ((dropzone.Statistics.ReferenceSize - sizeOffset + fixedValue.Length) > dropzone.Statistics.MaxReferenceSize)
            {
                dropzone.Statistics.ReferenceSetsFailedCount++;
                return StatusCode(429, $"Can't accept: Exceeds maximum reference value size of {dropzone.Statistics.MaxReferenceSize}");
            }
            if (dropzone.References.Count >= dropzone.Statistics.MaxReferencesCount)
            {
                dropzone.Statistics.ReferenceSetsFailedCount++;
                return StatusCode(429, $"Can't accept: Exceeds maximum reference count of {dropzone.Statistics.MaxReferencesCount}");
            }
            dropzone.Statistics.ReferenceSize -= sizeOffset;
            dropzone.References.Remove(key, out string ignored);
            dropzone.References.AddOrUpdate(key, fixedValue, (k, o) => fixedValue);
            dropzone.Statistics.ReferenceSize += fixedValue.Length;
            dropzone.Statistics.ReferenceCount = dropzone.References.Count();
            dropzone.Statistics.LastSetReference = DateTime.Now;
            return StatusCode(200, "Reference accepted");
        }

        /// <summary>
        /// Gets the value of a reference key in a dropzone.Statistics.  A reference is a key/value setting.
        /// </summary>
        /// <param name="AccessToken">Optional: access token value if used.</param>
        /// <param name="dropzoneName">the dropzone identifier</param>
        /// <param name="key">the name identifying the value to retrieve</param>
        /// <returns>string which is the reference value (always text/plain)</returns>
        [HttpGet("reference/get/{dropzoneName}/{key}", Name = "GetReference")]
        [Produces("text/plain")]
        [ProducesResponseType(400, Type = typeof(string))]
        [ProducesResponseType(401)]
        [ProducesResponseType(451)]
        [ProducesResponseType(429, Type = typeof(string))]
        [ProducesResponseType(200, Type = typeof(string))]
        [ProducesResponseType(500)]
        public IActionResult GetReference(
            [FromHeader] string AccessToken,
            [FromRoute] string dropzoneName,
            [FromRoute] string key)
        {
            if (IsClientBlacklisted(_accessor.HttpContext.Connection.RemoteIpAddress.ToString()))
            {
                return StatusCode(451, "You are not playing nice.");
            }
            if (string.Compare(AccessToken ?? string.Empty, _storage.AccessToken ?? string.Empty, false) != 0)
            {
                return HandleFailedAuthTokenValue(_accessor.HttpContext.Connection.RemoteIpAddress.ToString(), string.Empty, "GetReference");
            }
            if (!_storage.DropZoneList.ContainsKey(dropzoneName))
            {
                if (_storage.DropZoneList.Count >= MaxDropzones)
                {
                    return StatusCode(429, $"Can't create new dropzone {dropzoneName}.. at maximum of {MaxDropzones} dropzone definitions.");
                }
                CreateDropZone(dropzoneName);
            }
            var dropzone = _storage.DropZoneList[dropzoneName];
            string result = null;
            if (dropzone.References.Count == 0 || !dropzone.References.ContainsKey(key))
            {
                result = string.Empty;
            }
            else
            {
                result = dropzone.References[key];
            }
            dropzone.Statistics.LastGetReference = DateTime.Now;
            return Ok(result);
        }

        /// <summary>
        /// Get a list of the reference key names available
        /// </summary>
        /// <param name="AccessToken">Optional: access token value if used.</param>
        /// <param name="dropzoneName">the dropzone identifier</param>
        /// <returns>list of strings which contain the reference key names</returns>
        [HttpGet("reference/list/{dropzoneName}", Name = "ListReferences")]
        [Produces("application/json")]
        [ProducesResponseType(200, Type = typeof(List<string>))]
        [ProducesResponseType(400, Type = typeof(string))]
        [ProducesResponseType(401)]
        [ProducesResponseType(451)]
        [ProducesResponseType(404, Type = typeof(string))]
        [ProducesResponseType(500)]
        public IActionResult ListReferences(
            [FromHeader] string AccessToken,
            [FromRoute] string dropzoneName)
        {
            if (IsClientBlacklisted(_accessor.HttpContext.Connection.RemoteIpAddress.ToString()))
            {
                return StatusCode(451, "You are not playing nice.");
            }
            if (string.Compare(AccessToken ?? string.Empty, _storage.AccessToken ?? string.Empty, false) != 0)
            {
                return HandleFailedAuthTokenValue(_accessor.HttpContext.Connection.RemoteIpAddress.ToString(), string.Empty, "ListReferences");
            }
            if (!_storage.DropZoneList.ContainsKey(dropzoneName))
            {
                if (_storage.DropZoneList.Count >= MaxDropzones)
                {
                    return StatusCode(429, $"Can't create new dropzone {dropzoneName}.. at maximum of {MaxDropzones} dropzone definitions.");
                }
                CreateDropZone(dropzoneName);
            }
            var dropzone = _storage.DropZoneList[dropzoneName];
            return Ok(dropzone.References.Keys.ToList());
        }

        /// <summary>
        /// Clear: drops all payloads and references from a specific drop zone, and resets its statistics.
        /// </summary>
        /// <param name="AccessToken">Optional: access token value if used.</param>
        /// <param name="dropzoneName">the dropzone identifier</param>
        /// <returns>string</returns>
        [HttpGet("clear/{dropzoneName}", Name = "Clear")]
        [Produces("text/plain")]
        [ProducesResponseType(200, Type = typeof(string))]
        [ProducesResponseType(401)]
        [ProducesResponseType(451)]
        [ProducesResponseType(500)]
        public IActionResult Clear(
            [FromHeader] string AccessToken,
            [FromRoute] string dropzoneName)
        {
            if (IsClientBlacklisted(_accessor.HttpContext.Connection.RemoteIpAddress.ToString()))
            {
                return StatusCode(451, "You are not playing nice.");
            }
            if (string.Compare(AccessToken ?? string.Empty, _storage.AccessToken ?? string.Empty, false) != 0)
            {
                return HandleFailedAuthTokenValue(_accessor.HttpContext.Connection.RemoteIpAddress.ToString(), string.Empty, "Clear");
            }
            if (_storage.DropZoneList.ContainsKey(dropzoneName))
            {
                _storage.DropZoneList.Remove(dropzoneName);
            }
            return Ok($"drop zone {dropzoneName} cleared");
        }

        /// <summary>
        /// Reset: clear all drop zones and their data.  Essentially a soft boot.
        /// </summary>
        /// <param name="AccessToken">Optional: access token value if used.</param>
        /// <returns>string</returns>
        [HttpGet("reset", Name = "Reset")]
        [Produces("text/plain")]
        [ProducesResponseType(200, Type = typeof(string))]
        [ProducesResponseType(401)]
        [ProducesResponseType(451)]
        [ProducesResponseType(500)]
        public IActionResult Reset(
            [FromHeader] string AccessToken)
        {
            if (IsClientBlacklisted(_accessor.HttpContext.Connection.RemoteIpAddress.ToString()))
            {
                return StatusCode(451, "You are not playing nice.");
            }
            if (string.Compare(AccessToken ?? string.Empty, _storage.AccessToken ?? string.Empty, false) != 0)
            {
                return HandleFailedAuthTokenValue(_accessor.HttpContext.Connection.RemoteIpAddress.ToString(), string.Empty, "Reset");
            }
            _storage.Reset();
            return Ok("all drop zone data cleared");
        }

        /// <summary>
        /// Shutdown: clear all drop zones and their data
        /// </summary>
        /// <param name="AccessToken">Optional: access token value if used.</param>
        /// <returns>string</returns>
        [HttpGet("shutdown", Name = "Shutdown")]
        [Produces("text/plain")]
        [ProducesResponseType(200, Type = typeof(string))]
        [ProducesResponseType(401)]
        [ProducesResponseType(451)]
        [ProducesResponseType(500)]
        public IActionResult Shutdown(
            [FromHeader] string AccessToken)
        {
            if (IsClientBlacklisted(_accessor.HttpContext.Connection.RemoteIpAddress.ToString()))
            {
                return StatusCode(451, "You are not playing nice.");
            }
            if (string.Compare(AccessToken ?? string.Empty, _storage.AccessToken ?? string.Empty, false) != 0)
            {
                return HandleFailedAuthTokenValue(_accessor.HttpContext.Connection.RemoteIpAddress.ToString(), string.Empty, "Shutdown");
            }
            _storage.Shutdown();
            return Ok("Shutdown requested... bye");
        }

        private void CreateDropZone(string dropzoneName)
        {
            _storage.DropZoneList.Add(dropzoneName, new DropPoint());
            _storage.DropZoneList[dropzoneName].Statistics.Name = dropzoneName;
        }

        private bool IsClientBlacklisted(string ipAddress)
        {
            lock (LockBlacklist)
            {
                if (!BlacklistedClients.ContainsKey(ipAddress)) return false;
                if (BlacklistedClients[ipAddress] < DateTime.Now.AddMinutes(-2))
                {
                    BlacklistedClients.Remove(ipAddress);
                    return false;
                }
                return true;
            }
        }

        private IActionResult HandleFailedAuthTokenValue(string ipAddress, string dropZoneName, string apiMethodName)
        {
            var entry = new KeyValuePair<string, string>(dropZoneName, apiMethodName);
            var failedAuthToken = _storage.FailedAuthTokenWatchList.Where(t => t.IpAddress == ipAddress).FirstOrDefault();
            if (failedAuthToken == null)
            {
                _storage.FailedAuthTokenWatchList.Add(new FailedAuthTokenWatch
                {
                    IpAddress = ipAddress,
                    Attempts = 1
                });
                failedAuthToken = _storage.FailedAuthTokenWatchList.Where(t => t.IpAddress == ipAddress).First();
                failedAuthToken.AccessPoints.Add(dropZoneName, new Dictionary<string, long>());
                failedAuthToken.AccessPoints[dropZoneName].Add(apiMethodName, 1);
                failedAuthToken.AccessPointCount++;
            }
            else
            {
                failedAuthToken.Attempts++;
                failedAuthToken.LatestAttempt = DateTime.Now;
                if (failedAuthToken.AccessPointCount < 100)
                {
                    if (!failedAuthToken.AccessPoints.ContainsKey(dropZoneName))
                    {
                        failedAuthToken.AccessPoints.Add(dropZoneName, new Dictionary<string, long>());
                    }
                    if (!failedAuthToken.AccessPoints[dropZoneName].ContainsKey(apiMethodName))
                    {
                        failedAuthToken.AccessPoints[dropZoneName].Add(apiMethodName, 0);
                    }
                    failedAuthToken.AccessPoints[dropZoneName][apiMethodName]++;
                    failedAuthToken.AccessPointCount++;
                }
            }
            if (failedAuthToken.AccessPointCount >= 5)
            {
                if (!BlacklistedClients.ContainsKey(ipAddress))
                {
                    BlacklistedClients.Add(ipAddress, DateTime.Now);
                }
                else
                {
                    BlacklistedClients[ipAddress] = DateTime.Now;
                }
            }
            return Unauthorized();
        }
    }
}