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

namespace BOG.DropZone.Controllers
{
    [Produces("text/plain")]
    [Route("api")]
    public class ApiController : Controller
    {
        private IStorage _storage;

        /// <summary>
        /// Instantiate via injection
        /// </summary>
        /// <param name="storage">(injected)</param>
        public ApiController(IStorage storage)
        {
            _storage = storage;
        }

        /// <summary>
        /// Deposit a payload to a drop zone
        /// </summary>
        /// <returns>varies: see method declaration</returns>
        [HttpPost("payload/dropoff/{id}", Name = "DropoffPayload")]
        [ProducesResponseType(400, Type = typeof(string))]
        [ProducesResponseType(200, Type = typeof(string))]
        [ProducesResponseType(429, Type = typeof(string))]
        [ProducesResponseType(500, Type = typeof(string))]
        [Produces("text/plain")]
        [Consumes("application/json")]
        public IActionResult DropoffPayload(
            [FromRoute] string id,
            [FromBody] Lockbox payload)
        {
            if (!_storage.DropzoneList.ContainsKey(id))
            {
                _storage.DropzoneList.Add(id, new Storage.Dropzone());
            }
            var dropzone = _storage.DropzoneList[id];
            if (dropzone.PayloadSize + payload.Content.Length > dropzone.MaxTotalDropzonesSize)
            {
                return StatusCode(429, $"Can't accept: Exceeds maximum payload size of {dropzone.MaxTotalDropzonesSize}");
            }
            if (dropzone.Dropzones.Count >= dropzone.MaxDropzonesCount)
            {
                return StatusCode(429, $"Can't accept: Exceeds maximum payload count of {dropzone.MaxDropzonesCount}");
            }
            dropzone.PayloadSize += payload.Content.Length;
            dropzone.Dropzones.Enqueue(payload);
            var info = new Lockbox
            {
                ContentType = "drop zone",
                Content = Serializer<Dropzone>.ToJson(dropzone)
            };
            _storage.DropzoneList[id].References.AddOrUpdate("info", info, (k, o) => info);
            return StatusCode(200, "Payload accepted");
        }

        /// <summary>
        /// Pickup a payload from a drop zone
        /// </summary>
        /// <returns>string which is the payload content  (always text/plain)</returns>
        [HttpGet("payload/pickup/{id}", Name = "PickupPayload")]
        [Produces("application/json")]
        [ProducesResponseType(404, Type = typeof(string))]
        [ProducesResponseType(200, Type = typeof(Lockbox))]
        [ProducesResponseType(204, Type = typeof(string))]
        [ProducesResponseType(424, Type = typeof(string))]
        [ProducesResponseType(500)]
        public IActionResult PickupPayload([FromRoute] string id)
        {
            if (!_storage.DropzoneList.ContainsKey(id))
            {
                return StatusCode(404, $"drop zone does not exist: {id}");
            }
            var dropzone = _storage.DropzoneList[id];
            if (dropzone.Dropzones.Count == 0)
            {
                return StatusCode(204, $"No payloads in this drop zone: {id}");
            }

            if (!dropzone.Dropzones.TryDequeue(out Lockbox payload))
            {
                return StatusCode(424, $"Dropzones exist, but failed to acquire a payload from this drop zone: {id}");
            }
            dropzone.PayloadSize -= payload.Content.Length;
            var info = new Lockbox
            {
                ContentType = "drop zone",
                Content = Serializer<Dropzone>.ToJson(dropzone)
            };
            _storage.DropzoneList[id].References.AddOrUpdate("info", info, (k, o) => info);
            return StatusCode(200, payload);
        }

        /// <summary>
        /// Sets the value of a reference key in a dropzone.  A reference is a key/value setting.
        /// </summary>
        /// <returns>varies: see method declaration</returns>
        [HttpPost("reference/set/{id}/{key}", Name = "SetReference")]
        [Produces("text/plain")]
        [ProducesResponseType(400, Type = typeof(string))]
        [ProducesResponseType(200, Type = typeof(string))]
        [ProducesResponseType(429, Type = typeof(string))]
        [ProducesResponseType(500, Type = typeof(string))]
        [Consumes("application/json")]
        public IActionResult SetReference(
            [FromRoute] string id,
            [FromRoute] string key,
            [FromBody] Lockbox value)
        {
            var fixedValue = value ?? new Lockbox();
            if (!_storage.DropzoneList.ContainsKey(id))
            {
                _storage.DropzoneList.Add(id, new Storage.Dropzone());
            }
            var dropzone = _storage.DropzoneList[id];
            if (dropzone.References.ContainsKey(key))
            {
                dropzone.ReferenceSize -= dropzone.References[key].Content.Length;
                dropzone.References.Remove(key, out Lockbox ignored);
            }
            if (dropzone.ReferenceSize + fixedValue.Content.Length > dropzone.MaxTotalReferencesSize)
            {
                return StatusCode(429, $"Can't accept: Exceeds maximum reference value size of {dropzone.MaxTotalReferencesSize}");
            }
            if (dropzone.References.Count >= dropzone.MaxReferencesCount)
            {
                return StatusCode(429, $"Can't accept: Exceeds maximum reference count of {dropzone.MaxReferencesCount}");
            }
            dropzone.ReferenceSize += fixedValue.Content.Length;
            dropzone.References.AddOrUpdate(key.ToLower(), fixedValue, (k, o) => fixedValue);
            return StatusCode(200, "Reference accepted");
        }

        /// <summary>
        /// Gets the value of a reference key in a dropzone.  A reference is a key/value setting.
        /// </summary>
        /// <returns>string which is the reference value (always text/plain)</returns>
        [HttpGet("reference/get/{id}/{key}", Name = "GetReference")]
        [Produces("application/json")]
        [ProducesResponseType(400, Type = typeof(string))]
        [ProducesResponseType(404, Type = typeof(string))]
        [ProducesResponseType(200, Type = typeof(List<string>))]
        [ProducesResponseType(424, Type = typeof(string))]
        public IActionResult GetReference(
            [FromRoute] string id,
            [FromRoute] string key)
        {
            if (!_storage.DropzoneList.ContainsKey(id))
            {
                _storage.DropzoneList.Add(id, new Dropzone());
            }
            var dropzone = _storage.DropzoneList[id];
            if (dropzone.References.Count == 0 || !dropzone.References.ContainsKey(key.ToLower()))
            {
                return StatusCode(200, new Lockbox());
            }
            return Ok(dropzone.References[key.ToLower()]);
        }

        /// <summary>
        /// Get a list of the refence key names
        /// </summary>
        /// <returns>list of strings which contain the reference key names</returns>
        [HttpGet("reference/list/{id}", Name = "ListReferences")]
        [Produces("application/json")]
        [ProducesResponseType(400, Type = typeof(string))]
        [ProducesResponseType(404, Type = typeof(string))]
        [ProducesResponseType(200, Type = typeof(Lockbox))]
        [ProducesResponseType(424, Type = typeof(string))]
        public IActionResult ListReferences([FromRoute] string id)
        {
            if (!_storage.DropzoneList.ContainsKey(id))
            {
                _storage.DropzoneList.Add(id, new Dropzone());
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
        public IActionResult Shutdown()
        {
            _storage.Shutdown();
            return Ok("all drop zone data cleared");
        }
    }
}
