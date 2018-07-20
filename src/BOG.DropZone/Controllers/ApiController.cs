﻿using System;
using System.Collections.Generic;
using System.Linq;
using BOG.DropZone.Interface;
using BOG.DropZone.Storage;
using BOG.SwissArmyKnife;
using BOG.DropZone.Common.Dto;
using Microsoft.AspNetCore.Mvc;

namespace BOG.dropzone.Statistics.Controllers
{
    /// <summary>
    /// Provides all the client access points for the dropzones and admin functions.
    /// </summary>
    [Produces("text/plain")]
    [Route("api")]
    public class ApiController : Controller
    {
        private const int MaxDropzones = 10;

        private IStorage _storage;

        /// <summary>
        /// Instantiated via injection
        /// </summary>
        /// <param name="storage">(injected)</param>
        public ApiController(IStorage storage)
        {
            _storage = storage;
        }

        /// <summary>
        /// Heartbeat check for clients
        /// </summary>
        /// <returns>varies: see method declaration</returns>
        [HttpGet("heartbeat", Name = "Heartbeat")]
        [ProducesResponseType(200, Type = typeof(string))]
        [ProducesResponseType(500)]
        [Produces("text/plain")]
        public IActionResult Heartbeat()
        {
            return StatusCode(200, "Active");
        }

        /// <summary>
        /// Deposit a payload to a drop zone
        /// </summary>
        /// <param name="dropzoneName">the dropzone identifier</param>
        /// <param name="payload">the content to transfer</param>
        /// <returns>varies: see method declaration</returns>
        [HttpPost("payload/dropoff/{dropzoneName}", Name = "DropoffPayload")]
        [ProducesResponseType(400, Type = typeof(string))]
        [ProducesResponseType(200, Type = typeof(string))]
        [ProducesResponseType(429, Type = typeof(string))]
        [ProducesResponseType(500)]
        [Produces("text/plain")]
        public IActionResult DropoffPayload(
            [FromRoute] string dropzoneName,
            [FromBody] string payload)
        {
            if (!_storage.DropZoneList.ContainsKey(dropzoneName))
            {
                if (_storage.DropZoneList.Count >= MaxDropzones)
                {
                    return StatusCode(429, $"Can't create new dropzone {dropzoneName}.. at maximum of {MaxDropzones} dropzone definitions.");
                }
                _storage.DropZoneList.Add(dropzoneName, new DropPoint());
            }
            var dropzone = _storage.DropZoneList[dropzoneName];
            if (dropzone.Statistics.PayloadSize + payload.Length > dropzone.Statistics.MaxPayloadSize)
            {
                dropzone.Statistics.PayloadDropOffsDenied++;
                return StatusCode(429, $"Can't accept: Exceeds maximum payload size of {dropzone.Statistics.MaxPayloadSize}");
            }
            if (dropzone.Payloads.Count >= dropzone.Statistics.MaxPayloadCount)
            {
                dropzone.Statistics.PayloadDropOffsDenied++;
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
        /// <param name="dropzoneName">the dropzone identifier</param>
        /// <returns>the data to transfer</returns>
        [HttpGet("payload/pickup/{dropzoneName}", Name = "PickupPayload")]
        [Produces("text/plain")]
        [ProducesResponseType(200, Type = typeof(string))]
        [ProducesResponseType(204, Type = typeof(string))]
        [ProducesResponseType(400, Type = typeof(string))]
        [ProducesResponseType(410, Type = typeof(string))]
        [ProducesResponseType(429, Type = typeof(string))]
        [ProducesResponseType(500)]
        public IActionResult PickupPayload([FromRoute] string dropzoneName)
        {
            if (!_storage.DropZoneList.ContainsKey(dropzoneName))
            {
                if (_storage.DropZoneList.Count >= MaxDropzones)
                {
                    return StatusCode(429, $"Can't create new dropzone {dropzoneName}.. at maximum of {MaxDropzones} dropzone definitions.");
                }
                _storage.DropZoneList.Add(dropzoneName, new DropPoint());
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
        /// <param name="dropzoneName">the dropzone identifier</param>
        /// <returns>varies: see method declaration</returns>
        [HttpGet("payload/statistics/{dropzoneName}", Name = "GetStatistics")]
        [ProducesResponseType(400, Type = typeof(string))]
        [ProducesResponseType(200, Type = typeof(string))]
        [ProducesResponseType(429, Type = typeof(string))]
        [ProducesResponseType(500)]
        [Produces("text/plain")]
        public IActionResult GetStatistics([FromRoute] string dropzoneName)
        {
            if (!_storage.DropZoneList.ContainsKey(dropzoneName))
            {
                if (_storage.DropZoneList.Count >= MaxDropzones)
                {
                    return StatusCode(429, $"Can't create new dropzone {dropzoneName}.. at maximum of {MaxDropzones} dropzone definitions.");
                }
                _storage.DropZoneList.Add(dropzoneName, new DropPoint());
                _storage.DropZoneList[dropzoneName].Statistics.Name = dropzoneName;
            }
            return StatusCode(200, Serializer<DropZoneInfo>.ToJson(_storage.DropZoneList[dropzoneName].Statistics));
        }

        /// <summary>
        /// Sets the value of a reference key in a dropzone.Statistics.  A reference is a key/value setting.
        /// </summary>
        /// <param name="dropzoneName">the dropzone identifier</param>
        /// <param name="key">the name for the value to store</param>
        /// <param name="value">the value to store for the key name</param>
        /// <returns>varies: see method declaration</returns>
        [HttpPost("reference/set/{dropzoneName}/{key}", Name = "SetReference")]
        [Produces("text/plain")]
        [ProducesResponseType(400, Type = typeof(string))]
        [ProducesResponseType(200, Type = typeof(string))]
        [ProducesResponseType(429, Type = typeof(string))]
        [ProducesResponseType(500)]
        public IActionResult SetReference(
            [FromRoute] string dropzoneName,
            [FromRoute] string key,
            [FromBody] string value)
        {
            var fixedValue = value;
            if (!_storage.DropZoneList.ContainsKey(dropzoneName))
            {
                if (_storage.DropZoneList.Count >= MaxDropzones)
                {
                    return StatusCode(429, $"Can't create new dropzone {dropzoneName}.. at maximum of {MaxDropzones} dropzone definitions.");
                }
                _storage.DropZoneList.Add(dropzoneName, new DropPoint());
            }
            var dropzone = _storage.DropZoneList[dropzoneName];
            if (dropzone.References.ContainsKey(key))
            {
                dropzone.Statistics.ReferenceSize -= dropzone.References[key].Length;
                dropzone.References.Remove(key, out string ignored);
            }
            if (dropzone.Statistics.ReferenceSize + fixedValue.Length > dropzone.Statistics.MaxReferenceSize)
            {
                dropzone.Statistics.ReferenceSetsDenied++;
                return StatusCode(429, $"Can't accept: Exceeds maximum reference value size of {dropzone.Statistics.MaxReferenceSize}");
            }
            if (dropzone.References.Count >= dropzone.Statistics.MaxReferencesCount)
            {
                dropzone.Statistics.ReferenceSetsDenied++;
                return StatusCode(429, $"Can't accept: Exceeds maximum reference count of {dropzone.Statistics.MaxReferencesCount}");
            }
            dropzone.References.AddOrUpdate(key, fixedValue, (k, o) => fixedValue);
            dropzone.Statistics.ReferenceSize += fixedValue.Length;
            dropzone.Statistics.ReferenceCount = dropzone.References.Count();
            dropzone.Statistics.LastSetReference = DateTime.Now;
            return StatusCode(200, "Reference accepted");
        }

        /// <summary>
        /// Gets the value of a reference key in a dropzone.Statistics.  A reference is a key/value setting.
        /// </summary>
        /// <param name="dropzoneName">the dropzone identifier</param>
        /// <param name="key">the name identifying the value to retrieve</param>
        /// <returns>string which is the reference value (always text/plain)</returns>
        [HttpGet("reference/get/{dropzoneName}/{key}", Name = "GetReference")]
        [Produces("text/plain")]
        [ProducesResponseType(400, Type = typeof(string))]
        [ProducesResponseType(429, Type = typeof(string))]
        [ProducesResponseType(200, Type = typeof(string))]
        [ProducesResponseType(500)]
        public IActionResult GetReference(
            [FromRoute] string dropzoneName,
            [FromRoute] string key)
        {
            if (!_storage.DropZoneList.ContainsKey(dropzoneName))
            {
                if (_storage.DropZoneList.Count >= MaxDropzones)
                {
                    return StatusCode(429, $"Can't create new dropzone {dropzoneName}.. at maximum of {MaxDropzones} dropzone definitions.");
                }
                _storage.DropZoneList.Add(dropzoneName, new DropPoint());
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
        /// <param name="dropzoneName">the dropzone identifier</param>
        /// <returns>list of strings which contain the reference key names</returns>
        [HttpGet("reference/list/{dropzoneName}", Name = "ListReferences")]
        [Produces("application/json")]
        [ProducesResponseType(400, Type = typeof(string))]
        [ProducesResponseType(404, Type = typeof(string))]
        [ProducesResponseType(200, Type = typeof(List<string>))]
        [ProducesResponseType(500)]
        public IActionResult ListReferences([FromRoute] string dropzoneName)
        {
            if (!_storage.DropZoneList.ContainsKey(dropzoneName))
            {
                if (_storage.DropZoneList.Count >= MaxDropzones)
                {
                    return StatusCode(429, $"Can't create new dropzone {dropzoneName}.. at maximum of {MaxDropzones} dropzone definitions.");
                }
                _storage.DropZoneList.Add(dropzoneName, new DropPoint());
            }
            var dropzone = _storage.DropZoneList[dropzoneName];
            return Ok(dropzone.References.Keys.ToList());
        }

        /// <summary>
        /// Clear: drops all payloads and references from a specific drop zone, and resets its statistics.
        /// </summary>
        /// <returns>string</returns>
        [HttpGet("clear/{dropzoneName}", Name = "Clear")]
        [Produces("text/plain")]
        [ProducesResponseType(200, Type = typeof(string))]
        [ProducesResponseType(500)]
        public IActionResult Clear([FromRoute] string dropzoneName)
        {
            if (_storage.DropZoneList.ContainsKey(dropzoneName))
            {
                _storage.DropZoneList.Remove(dropzoneName);
            }
            return Ok($"drop zone {dropzoneName} cleared");
        }

        /// <summary>
        /// Reset: clear all drop zones and their data.  Essentially a soft boot.
        /// </summary>
        /// <returns>string</returns>
        [HttpGet("reset", Name = "Reset")]
        [Produces("text/plain")]
        [ProducesResponseType(200, Type = typeof(string))]
        [ProducesResponseType(500)]
        public IActionResult Reset()
        {
            _storage.Reset();
            return Ok("all drop zone data cleared");
        }

        /// <summary>
        /// Shutdown: clear all drop zones and their data
        /// </summary>
        /// <returns>string</returns>
        [HttpGet("shutdown", Name = "Shutdown")]
        [Produces("text/plain")]
        [ProducesResponseType(200, Type = typeof(string))]
        [ProducesResponseType(500)]
        public IActionResult Shutdown()
        {
            _storage.Shutdown();
            return Ok("shutdown requested. bye.");
        }
    }
}
