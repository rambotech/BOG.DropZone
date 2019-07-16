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
        private readonly string _AppName = null;
        private readonly string _AppVersion = null;
        private enum TokenType : int { Access = 0, Admin = 1 }

        private readonly IHttpContextAccessor _Accessor;
        private readonly IStorage _Storage;
        private readonly IConfiguration _Configuration;

        private readonly Stopwatch _UpTime = new Stopwatch();

        private readonly object _LockClientWatchList = new object();
        private readonly object _LockDropZoneInfo = new object();

        /// <summary>
        /// Instantiated via injection
        /// </summary>
        /// <param name="accessor">(injected)</param>
        /// <param name="storage">(injected)</param>
        /// <param name="configuration">(injected)</param>
        public ApiController(IHttpContextAccessor accessor, IStorage storage, IConfiguration configuration)
        {
            _Accessor = accessor;
            _Storage = storage;
            _Configuration = configuration;
            _AppName = $"BOG.DropZone";
            _AppVersion = $"{GetType().Assembly.GetName().Version}";
        }

        /// <summary>
        /// Heartbeat check for clients.  No authorization header required.
        /// </summary>
        /// <returns>200 to confirm the API is active.</returns>
        [HttpGet("heartbeat", Name = "Heartbeat")]
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
            var result = new JObject();
            result.Add("name", _AppName);
            result.Add("version", _AppVersion);
            return StatusCode(200, result);
        }

        /// <summary>
        /// Deposit a payload to a drop zone
        /// </summary>
        /// <param name="AccessToken">Optional: access token value if used.</param>
        /// <param name="dropzoneName">the dropzone identifier</param>
        /// <param name="payload">the content to transfer</param>
        /// <param name="expires">(optional): when the value should no longer be returned.</param>
        /// <returns>varies: see method declaration</returns>
        [HttpPost("payload/dropoff/{dropzoneName}", Name = "DropoffPayload")]
        [RequestSizeLimit(100_000_000)]
        [ProducesResponseType(201, Type = typeof(string))]
        [ProducesResponseType(400, Type = typeof(string))]
        [ProducesResponseType(401)]
        [ProducesResponseType(429, Type = typeof(string))]
        [ProducesResponseType(500)]
        [Produces("text/plain")]
        public IActionResult DropoffPayload(
            [FromHeader] string AccessToken,
            [FromRoute] string dropzoneName,
            [FromBody] string payload,
            [FromQuery] DateTime? expires)
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
                dropzone.Payloads.Enqueue(new StoredValue
                {
                    Value = payload,
                    Expires = expires ?? DateTime.MaxValue
                });
                dropzone.Statistics.PayloadCount = dropzone.Payloads.Count();
                dropzone.Statistics.LastDropoff = DateTime.Now;

                return StatusCode(201, "Payload accepted");
            }
        }

        /// <summary>
        /// Pickup a payload from a drop zone
        /// </summary>
        /// <param name="AccessToken">Optional: access token value if used.</param>
        /// <param name="dropzoneName">the dropzone identifier</param>
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
                StoredValue payload = null;
                bool payloadAvailable = false;
                int retriesRemaining = 3;
                while (dropzone.Payloads.Count > 0 && !payloadAvailable)
                {
                    if (!dropzone.Payloads.TryDequeue(out payload))
                    {
                        if (retriesRemaining > 0)
                        {
                            retriesRemaining--;
                            System.Threading.Thread.Sleep(50);
                            continue;
                        }
                        return StatusCode(500, $"Dropzone exists with payloads, but failed to acquire a payload after three attempts.");
                    }
                    if (payload.Expires < DateTime.Now)
                    {
                        dropzone.Statistics.PayloadExpiredCount++;
                        dropzone.Statistics.PayloadSize -= payload.Value.Length;
                        dropzone.Statistics.PayloadCount = dropzone.Payloads.Count();
                        continue;
                    }
                    dropzone.Statistics.PayloadSize -= payload.Value.Length;
                    dropzone.Statistics.PayloadCount = dropzone.Payloads.Count();
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
        /// Sets the value of a reference key in a dropzone.Statistics.  A reference is a key/value setting.
        /// </summary>
        /// <param name="AccessToken">Optional: access token value if used.</param>
        /// <param name="dropzoneName">the dropzone identifier</param>
        /// <param name="key">the name for the value to store</param>
        /// <param name="expires">(optional): when the value should no longer be returned.</param>
        /// <param name="value">the value to store for the key name</param>
        /// <returns>varies: see method declaration</returns>
        [HttpPost("reference/set/{dropzoneName}/{key}", Name = "SetReference")]
        [RequestSizeLimit(100_000_000)]
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
                if ((dropzone.Statistics.ReferenceSize - sizeOffset + fixedValue.Value.Length) > dropzone.Statistics.MaxReferenceSize)
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
                dropzone.References.Remove(key, out StoredValue ignored);
                dropzone.References.AddOrUpdate(key, fixedValue, (k, o) => fixedValue);
                dropzone.Statistics.ReferenceSize += fixedValue.Value.Length;
                dropzone.Statistics.ReferenceCount = dropzone.References.Count();
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
                List<string> returnList = new List<string>();
                if (dropzone.References.Count > 0)
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