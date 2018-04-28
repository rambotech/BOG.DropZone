using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BOG.DropZone.Interface;
using BOG.DropZone.Client.Model;
using BOG.DropZone.Client.Helpers;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using BOG.DropZone.Storage;
using BOG.SwissArmyKnife;
using System.Text;

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
        /// Deposit a payload to a drop zone
        /// </summary>
        /// <param name="id">the dropzone identifier</param>
        /// <param name="payload">the content to transfer</param>
        /// <returns>varies: see method declaration</returns>
        [HttpPost("payload/dropoff/{id}", Name = "DropoffPayload")]
        [ProducesResponseType(400, Type = typeof(string))]
        [ProducesResponseType(200, Type = typeof(string))]
        [ProducesResponseType(429, Type = typeof(string))]
        [ProducesResponseType(500)]
        [Produces("text/plain")]
        [Consumes("application/json")]
        public IActionResult DropoffPayload(
            [FromRoute] string id,
            [FromBody] Lockbox payload)
        {
            if (!_storage.DropzoneList.ContainsKey(id))
            {
                if (_storage.DropzoneList.Count >= MaxDropzones)
                {
                    return StatusCode(429, $"Can't create new dropzone {id}.. at maximum of {MaxDropzones} dropzone definitions.");
                }
                _storage.DropzoneList.Add(id, new Storage.Dropzone());
            }
            var dropzone = _storage.DropzoneList[id];
            if (dropzone.PayloadSize + payload.Content.Length > dropzone.MaxPayloadSize)
            {
                dropzone.PayloadDropOffsDenied++;
                return StatusCode(429, $"Can't accept: Exceeds maximum payload size of {dropzone.MaxPayloadSize}");
            }
            if (dropzone.Payloads.Count >= dropzone.MaxPayloadCount)
            {
                dropzone.PayloadDropOffsDenied++;
                return StatusCode(429, $"Can't accept: Exceeds maximum payload count of {dropzone.MaxPayloadCount}");
            }
            dropzone.PayloadSize += payload.Content.Length;
            dropzone.Payloads.Enqueue(payload);
            dropzone.LastDropoff = DateTime.Now;
            return StatusCode(200, "Payload accepted");
        }

        /// <summary>
        /// Pickup a payload from a drop zone
        /// </summary>
        /// <param name="id">the dropzone identifier</param>
        /// <returns>the data to transfer</returns>
        [HttpGet("payload/pickup/{id}", Name = "PickupPayload")]
        [Produces("application/json")]
        [ProducesResponseType(200, Type = typeof(Lockbox))]
        [ProducesResponseType(204, Type = typeof(string))]
        [ProducesResponseType(410, Type = typeof(string))]
        [ProducesResponseType(429, Type = typeof(string))]
        [ProducesResponseType(500)]
        public IActionResult PickupPayload([FromRoute] string id)
        {
            if (!_storage.DropzoneList.ContainsKey(id))
            {
                if (_storage.DropzoneList.Count >= MaxDropzones)
                {
                    return StatusCode(429, $"Can't create new dropzone {id}.. at maximum of {MaxDropzones} dropzone definitions.");
                }
                _storage.DropzoneList.Add(id, new Storage.Dropzone());
            }
            var dropzone = _storage.DropzoneList[id];
            if (dropzone.Payloads.Count == 0)
            {
                return StatusCode(204, $"No payloads in this drop zone: {id}");
            }

            if (!dropzone.Payloads.TryDequeue(out Lockbox payload))
            {
                return StatusCode(410, $"Dropzone exists with payloads, but failed to acquire a payload");
            }
            dropzone.PayloadSize -= payload.Content.Length;
            dropzone.LastPickup = DateTime.Now;
            return StatusCode(200, payload);
        }

        /// <summary>
        /// Get statistics for a dropzone
        /// </summary>
        /// <param name="id">the dropzone identifier</param>
        /// <returns>varies: see method declaration</returns>
        [HttpGet("payload/statistics/{id}", Name = "GetStatistics")]
        [ProducesResponseType(400, Type = typeof(string))]
        [ProducesResponseType(200, Type = typeof(string))]
        [ProducesResponseType(429, Type = typeof(string))]
        [ProducesResponseType(500)]
        [Produces("text/plain")]
        public IActionResult GetStatistics([FromRoute] string id)
        {
            if (!_storage.DropzoneList.ContainsKey(id))
            {
                if (_storage.DropzoneList.Count >= MaxDropzones)
                {
                    return StatusCode(429, $"Can't create new dropzone {id}.. at maximum of {MaxDropzones} dropzone definitions.");
                }
                _storage.DropzoneList.Add(id, new Storage.Dropzone());
            }
            return StatusCode(200, Serializer<Dropzone>.ToJson(_storage.DropzoneList[id]));
        }

        /// <summary>
        /// Sets the value of a reference key in a dropzone.  A reference is a key/value setting.
        /// </summary>
        /// <param name="id">the dropzone identifier</param>
        /// <param name="key">the name for the value to store</param>
        /// <param name="value">the value to store for the key name</param>
        /// <returns>varies: see method declaration</returns>
        [HttpPost("reference/set/{id}/{key}", Name = "SetReference")]
        [Produces("text/plain")]
        [ProducesResponseType(400, Type = typeof(string))]
        [ProducesResponseType(200, Type = typeof(string))]
        [ProducesResponseType(429, Type = typeof(string))]
        [ProducesResponseType(500)]
        [Consumes("application/json")]
        public IActionResult SetReference(
            [FromRoute] string id,
            [FromRoute] string key,
            [FromBody] Lockbox value)
        {
            var fixedValue = value;
            if (!_storage.DropzoneList.ContainsKey(id))
            {
                if (_storage.DropzoneList.Count >= MaxDropzones)
                {
                    return StatusCode(429, $"Can't create new dropzone {id}.. at maximum of {MaxDropzones} dropzone definitions.");
                }
                _storage.DropzoneList.Add(id, new Storage.Dropzone());
            }
            var dropzone = _storage.DropzoneList[id];
            if (dropzone.References.ContainsKey(key))
            {
                dropzone.ReferenceSize -= dropzone.References[key].Content.Length;
                dropzone.References.Remove(key, out Lockbox ignored);
            }
            if (dropzone.ReferenceSize + fixedValue.Content.Length > dropzone.MaxReferenceSize)
            {
                dropzone.ReferenceSetsDenied++;
                return StatusCode(429, $"Can't accept: Exceeds maximum reference value size of {dropzone.MaxReferenceSize}");
            }
            if (dropzone.References.Count >= dropzone.MaxReferencesCount)
            {
                dropzone.ReferenceSetsDenied++;
                return StatusCode(429, $"Can't accept: Exceeds maximum reference count of {dropzone.MaxReferencesCount}");
            }
            dropzone.References.AddOrUpdate(key.ToLower(), fixedValue, (k, o) => fixedValue);
            dropzone.ReferenceSize += fixedValue.Content.Length;
            dropzone.LastSetReference = DateTime.Now;
            return StatusCode(200, "Reference accepted");
        }

        /// <summary>
        /// Gets the value of a reference key in a dropzone.  A reference is a key/value setting.
        /// </summary>
        /// <param name="id">the dropzone identifier</param>
        /// <param name="key">the name identifying the value to retrieve</param>
        /// <returns>string which is the reference value (always text/plain)</returns>
        [HttpGet("reference/get/{id}/{key}", Name = "GetReference")]
        [Produces("application/json")]
        [ProducesResponseType(400, Type = typeof(string))]
        [ProducesResponseType(429, Type = typeof(string))]
        [ProducesResponseType(200, Type = typeof(Lockbox))]
        [ProducesResponseType(500)]
        public IActionResult GetReference(
            [FromRoute] string id,
            [FromRoute] string key)
        {
            if (!_storage.DropzoneList.ContainsKey(id))
            {
                if (_storage.DropzoneList.Count >= MaxDropzones)
                {
                    return StatusCode(429, $"Can't create new dropzone {id}.. at maximum of {MaxDropzones} dropzone definitions.");
                }
                _storage.DropzoneList.Add(id, new Storage.Dropzone());
            }
            var dropzone = _storage.DropzoneList[id];
            Lockbox result = null;
            if (dropzone.References.Count == 0 || !dropzone.References.ContainsKey(key.ToLower()))
            {
                result = new Lockbox
                {
                    Content = string.Empty
                };
                HashMD5.Calculate(result);
            }
            else
            {
                result = dropzone.References[key.ToLower()];
            }
            dropzone.LastGetReference = DateTime.Now;
            return Ok(result);
        }

        /// <summary>
        /// Get a list of the reference key names available
        /// </summary>
        /// <param name="id">the dropzone identifier</param>
        /// <returns>list of strings which contain the reference key names</returns>
        [HttpGet("reference/list/{id}", Name = "ListReferences")]
        [Produces("application/json")]
        [ProducesResponseType(400, Type = typeof(string))]
        [ProducesResponseType(404, Type = typeof(string))]
        [ProducesResponseType(200, Type = typeof(List<string>))]
        [ProducesResponseType(500)]
        public IActionResult ListReferences([FromRoute] string id)
        {
            if (!_storage.DropzoneList.ContainsKey(id))
            {
                if (_storage.DropzoneList.Count >= MaxDropzones)
                {
                    return StatusCode(429, $"Can't create new dropzone {id}.. at maximum of {MaxDropzones} dropzone definitions.");
                }
                _storage.DropzoneList.Add(id, new Storage.Dropzone());
            }
            var dropzone = _storage.DropzoneList[id];
            return Ok(dropzone.References.Keys.ToList());
        }

        /// <summary>
        /// Reset: clear all drop zones and their data
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
            return Ok("all drop zone data cleared");
        }
    }
}
