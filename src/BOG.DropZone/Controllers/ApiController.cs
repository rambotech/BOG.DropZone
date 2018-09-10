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
        private IHttpContextAccessor _accessor;
        private IStorage _storage;
        private IConfiguration _configuration;

        private Stopwatch upTime = new Stopwatch();

        private object LockClientWatchList = new object();
        private object lockDropZoneInfo = new object();

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
        [RequestSizeLimit(1024)]
        [ProducesResponseType(200, Type = typeof(string))]
        [ProducesResponseType(401)]
        [ProducesResponseType(451)]
        [ProducesResponseType(500)]
        [Produces("text/plain")]
        public IActionResult Heartbeat([FromHeader] string AccessToken)
        {
            var clientIp = _accessor.HttpContext.Connection.RemoteIpAddress.ToString();
            if (!IsValidatedClient(clientIp, AccessToken, "*global*", System.Reflection.MethodBase.GetCurrentMethod().Name))
            {
                return Unauthorized();
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
        [RequestSizeLimit(5242880)]
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
            var clientIp = _accessor.HttpContext.Connection.RemoteIpAddress.ToString();
            if (!IsValidatedClient(clientIp, AccessToken, dropzoneName, System.Reflection.MethodBase.GetCurrentMethod().Name))
            {
                return Unauthorized();
            }
            lock (lockDropZoneInfo)
            {
                if (!_storage.DropZoneList.ContainsKey(dropzoneName))
                {
                    if (_storage.DropZoneList.Count >= _storage.MaxDropzones)
                    {
                        return StatusCode(429, $"Can't create new dropzone {dropzoneName}.. at maximum of {_storage.MaxDropzones} dropzone definitions.");
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
        }

        /// <summary>
        /// Pickup a payload from a drop zone
        /// </summary>
        /// <param name="AccessToken">Optional: access token value if used.</param>
        /// <param name="dropzoneName">the dropzone identifier</param>
        /// <returns>the data to transfer</returns>
        [HttpGet("payload/pickup/{dropzoneName}", Name = "PickupPayload")]
        [RequestSizeLimit(5242880)]
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
            var clientIp = _accessor.HttpContext.Connection.RemoteIpAddress.ToString();
            if (!IsValidatedClient(clientIp, AccessToken, dropzoneName, System.Reflection.MethodBase.GetCurrentMethod().Name))
            {
                return Unauthorized();
            }
            lock (lockDropZoneInfo)
            {
                if (!_storage.DropZoneList.ContainsKey(dropzoneName))
                {
                    if (_storage.DropZoneList.Count >= _storage.MaxDropzones)
                    {
                        return StatusCode(429, $"Can't create new dropzone {dropzoneName}.. at maximum of {_storage.MaxDropzones} dropzone definitions.");
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
        }

        /// <summary>
        /// Get statistics for a dropzone
        /// </summary>
        /// <param name="AccessToken">Optional: access token value if used.</param>
        /// <param name="dropzoneName">the dropzone identifier</param>
        /// <returns>varies: see method declaration</returns>
        [HttpGet("payload/statistics/{dropzoneName}", Name = "GetStatistics")]
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
            var clientIp = _accessor.HttpContext.Connection.RemoteIpAddress.ToString();
            if (!IsValidatedClient(clientIp, AccessToken, dropzoneName, System.Reflection.MethodBase.GetCurrentMethod().Name))
            {
                return Unauthorized();
            }
            lock (lockDropZoneInfo)
            {
                if (!_storage.DropZoneList.ContainsKey(dropzoneName))
                {
                    if (_storage.DropZoneList.Count >= _storage.MaxDropzones)
                    {
                        return StatusCode(429, $"Can't create new dropzone {dropzoneName}.. at maximum of {_storage.MaxDropzones} dropzone definitions.");
                    }
                    CreateDropZone(dropzoneName);
                }
                return StatusCode(200, Serializer<DropZoneInfo>.ToJson(_storage.DropZoneList[dropzoneName].Statistics));
            }
        }

        /// <summary>
        /// Get security information for a dropzone
        /// </summary>
        /// <param name="AccessToken">Optional: access token value if used.</param>
        /// <returns>varies: see method declaration</returns>
        [HttpGet("securityinfo", Name = "GetSecurityInfo")]
        [RequestSizeLimit(1024)]
        [ProducesResponseType(401)]
        [ProducesResponseType(451)]
        [ProducesResponseType(200, Type = typeof(string))]
        [ProducesResponseType(500)]
        [Produces("text/plain")]
        public IActionResult GetSecurityInfo(
            [FromHeader] string AccessToken)
        {
            var clientIp = _accessor.HttpContext.Connection.RemoteIpAddress.ToString();
            if (!IsValidatedClient(clientIp, AccessToken, "*global*", System.Reflection.MethodBase.GetCurrentMethod().Name))
            {
                return Unauthorized();
            }
            lock (LockClientWatchList)
            {
                return StatusCode(200, Serializer<List<ClientWatch>>.ToJson(_storage.ClientWatchList));
            }
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
        [RequestSizeLimit(5242880)]
        [ProducesResponseType(400, Type = typeof(string))]
        [ProducesResponseType(401)]
        [ProducesResponseType(451)]
        [ProducesResponseType(200, Type = typeof(string))]
        [ProducesResponseType(429, Type = typeof(string))]
        [ProducesResponseType(500)]
        [Produces("text/plain")]
        public IActionResult SetReference(
            [FromHeader] string AccessToken,
            [FromRoute] string dropzoneName,
            [FromRoute] string key,
            [FromBody] string value)
        {
            var clientIp = _accessor.HttpContext.Connection.RemoteIpAddress.ToString();
            if (!IsValidatedClient(clientIp, AccessToken, dropzoneName, System.Reflection.MethodBase.GetCurrentMethod().Name))
            {
                return Unauthorized();
            }
            var fixedValue = value ?? string.Empty;
            lock (lockDropZoneInfo)
            {
                if (!_storage.DropZoneList.ContainsKey(dropzoneName))
                {
                    if (_storage.DropZoneList.Count >= _storage.MaxDropzones)
                    {
                        return StatusCode(429, $"Can't create new dropzone {dropzoneName}.. at maximum of {_storage.MaxDropzones} dropzone definitions.");
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
        }

        /// <summary>
        /// Gets the value of a reference key in a dropzone.Statistics.  A reference is a key/value setting.
        /// </summary>
        /// <param name="AccessToken">Optional: access token value if used.</param>
        /// <param name="dropzoneName">the dropzone identifier</param>
        /// <param name="key">the name identifying the value to retrieve</param>
        /// <returns>string which is the reference value (always text/plain)</returns>
        [HttpGet("reference/get/{dropzoneName}/{key}", Name = "GetReference")]
        [RequestSizeLimit(5242880)]
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
            var clientIp = _accessor.HttpContext.Connection.RemoteIpAddress.ToString();
            if (!IsValidatedClient(clientIp, AccessToken, dropzoneName, System.Reflection.MethodBase.GetCurrentMethod().Name))
            {
                return Unauthorized();
            }
            lock (lockDropZoneInfo)
            {
                if (!_storage.DropZoneList.ContainsKey(dropzoneName))
                {
                    if (_storage.DropZoneList.Count >= _storage.MaxDropzones)
                    {
                        return StatusCode(429, $"Can't create new dropzone {dropzoneName}.. at maximum of {_storage.MaxDropzones} dropzone definitions.");
                    }
                    CreateDropZone(dropzoneName);
                }
                var dropzone = _storage.DropZoneList[dropzoneName];
                string result = null;
                if (dropzone.References.Count == 0 || !dropzone.References.ContainsKey(key))
                {
                    return StatusCode(204);
                }
                else
                {
                    result = dropzone.References[key];
                }
                dropzone.Statistics.LastGetReference = DateTime.Now;
                return Ok(result);
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
            var clientIp = _accessor.HttpContext.Connection.RemoteIpAddress.ToString();
            if (!IsValidatedClient(clientIp, AccessToken, dropzoneName, System.Reflection.MethodBase.GetCurrentMethod().Name))
            {
                return Unauthorized();
            }
            lock (lockDropZoneInfo)
            {
                if (!_storage.DropZoneList.ContainsKey(dropzoneName))
                {
                    if (_storage.DropZoneList.Count >= _storage.MaxDropzones)
                    {
                        return StatusCode(429, $"Can't create new dropzone {dropzoneName}.. at maximum of {_storage.MaxDropzones} dropzone definitions.");
                    }
                    CreateDropZone(dropzoneName);
                }
                var dropzone = _storage.DropZoneList[dropzoneName];
                return Ok(dropzone.References.Keys.ToList());
            }
        }

        /// <summary>
        /// Clear: drops all payloads and references from a specific drop zone, and resets its statistics.
        /// </summary>
        /// <param name="AccessToken">Optional: access token value if used.</param>
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
            [FromHeader] string AccessToken,
            [FromRoute] string dropzoneName)
        {
            var clientIp = _accessor.HttpContext.Connection.RemoteIpAddress.ToString();
            if (!IsValidatedClient(clientIp, AccessToken, dropzoneName, System.Reflection.MethodBase.GetCurrentMethod().Name))
            {
                return Unauthorized();
            }
            lock (lockDropZoneInfo)
            {
                if (_storage.DropZoneList.ContainsKey(dropzoneName))
                {
                    ReleaseDropZone(dropzoneName);
                }
            }
            return Ok($"drop zone {dropzoneName} cleared");
        }

        /// <summary>
        /// Reset: clear all drop zones and their data.  Essentially a soft boot.
        /// </summary>
        /// <param name="AccessToken">Optional: access token value if used.</param>
        /// <returns>string</returns>
        [HttpGet("reset", Name = "Reset")]
        [RequestSizeLimit(1024)]
        [ProducesResponseType(200, Type = typeof(string))]
        [ProducesResponseType(401)]
        [ProducesResponseType(451)]
        [ProducesResponseType(500)]
        [Produces("text/plain")]
        public IActionResult Reset(
            [FromHeader] string AccessToken)
        {
            var clientIp = _accessor.HttpContext.Connection.RemoteIpAddress.ToString();
            if (!IsValidatedClient(clientIp, AccessToken, "*global*", System.Reflection.MethodBase.GetCurrentMethod().Name))
            {
                return Unauthorized();
            }
            lock (lockDropZoneInfo)
            {
                _storage.Reset();
            }
            return Ok("all drop zone data cleared");
        }

        /// <summary>
        /// Shutdown: clear all drop zones and their data
        /// </summary>
        /// <param name="AccessToken">Optional: access token value if used.</param>
        /// <returns>string</returns>
        [HttpGet("shutdown", Name = "Shutdown")]
        [RequestSizeLimit(1024)]
        [ProducesResponseType(200, Type = typeof(string))]
        [ProducesResponseType(401)]
        [ProducesResponseType(451)]
        [ProducesResponseType(500)]
        [Produces("text/plain")]
        public IActionResult Shutdown(
            [FromHeader] string AccessToken)
        {
            var clientIp = _accessor.HttpContext.Connection.RemoteIpAddress.ToString();
            if (!IsValidatedClient(clientIp, AccessToken, "*global*", System.Reflection.MethodBase.GetCurrentMethod().Name))
            {
                return Unauthorized();
            }
            _storage.Shutdown();
            return Ok("Shutdown requested... bye");
        }

        #region Helper Methods

        // Adds the new drop zone to the memory storage tracker.
        private void CreateDropZone(string dropzoneName)
        {
            _storage.DropZoneList.Add(dropzoneName, new DropPoint());
            _storage.DropZoneList[dropzoneName].Statistics.Name = dropzoneName;
        }

        private void ReleaseDropZone(string dropzoneName)
        {
            _storage.DropZoneList.Remove(dropzoneName);
        }

        // Validates that the auth token provided is valid, and tracks failures.
        // NOTE:
        //     This method will return false when a valid access token is provided, but the client has exceeded
        //     the maximum allowed attempts within a timeframe (i.e. is in a lockdown period).  This protects 
        //     against brute force attacks, because the attacker can believe that it has not yet discovered the
        //     correct value.
        private bool IsValidatedClient(string ipAddress, string accessToken, string dropzoneName, string apiMethodName)
        {
            lock (LockClientWatchList)
            {
                var validAuthToken = string.Compare(accessToken ?? string.Empty, _storage.AccessToken ?? string.Empty, false) == 0;
                var allowAccess = validAuthToken;
                var clientInfo = _storage.ClientWatchList.Where(t => t.IpAddress == ipAddress).FirstOrDefault();
                if (clientInfo == null)
                {
                    _storage.ClientWatchList.Add(new ClientWatch
                    {
                        IpAddress = ipAddress
                    });
                    clientInfo = _storage.ClientWatchList.Where(t => t.IpAddress == ipAddress).First();
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
                    if (clientInfo.FailedAccessTimes.Count == _storage.MaximumFailedAttemptsBeforeLockout)
                    {
                        clientInfo.FailedAccessTimes.Dequeue();
                    }
                    clientInfo.FailedAccessTimes.Enqueue(DateTime.Now);
                }

                // prune out failed attempts that have expired.
                while (clientInfo.FailedAccessTimes.Count > 0)
                {
                    var thisAttemptTime = clientInfo.FailedAccessTimes.Peek();
                    if (thisAttemptTime.AddSeconds(_storage.LockoutSeconds) < DateTime.Now)
                    {
                        clientInfo.FailedAccessTimes.Dequeue();
                        continue;
                    }
                    break;
                }

                allowAccess &= (clientInfo.FailedAccessTimes.Count < _storage.MaximumFailedAttemptsBeforeLockout);
                return allowAccess;
            }
        }
        #endregion
    }
}