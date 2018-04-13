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
        /// Deposit a payload to a pathway
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
            if (!_storage.PathwayList.ContainsKey(id))
            {
                _storage.PathwayList.Add(id, new Storage.Pathway());
            }
            var pathway = _storage.PathwayList[id];
            if (pathway.PayloadSize + payload.Content.Length > pathway.MaxTotalPayloadsSize)
            {
                return StatusCode(429, $"Can't accept: Exceeds maximum payload size of {pathway.MaxTotalPayloadsSize}");
            }
            if (pathway.Payloads.Count >= pathway.MaxPayloadsCount)
            {
                return StatusCode(429, $"Can't accept: Exceeds maximum payload count of {pathway.MaxPayloadsCount}");
            }
            pathway.PayloadSize += payload.Content.Length;
            pathway.Payloads.Enqueue(payload);
            var info = new Lockbox
            {
                ContentType = "Pathway",
                Content = Serializer<Pathway>.ToJson(pathway)
            };
            _storage.PathwayList[id].References.AddOrUpdate("info", info, (k, o) => info);
            return StatusCode(200, "Payload accepted");
        }

        /// <summary>
        /// Pickup a payload from a pathway
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
            if (!_storage.PathwayList.ContainsKey(id))
            {
                return StatusCode(404, $"Pathway does not exist: {id}");
            }
            var pathway = _storage.PathwayList[id];
            if (pathway.Payloads.Count == 0)
            {
                return StatusCode(204, $"No payloads in this pathway: {id}");
            }

            if (!pathway.Payloads.TryDequeue(out Lockbox payload))
            {
                return StatusCode(424, $"Payloads exist, but failed to acquire a payload from this pathway: {id}");
            }
            pathway.PayloadSize -= payload.Content.Length;
            var info = new Lockbox
            {
                ContentType = "Pathway",
                Content = Serializer<Pathway>.ToJson(pathway)
            };
            _storage.PathwayList[id].References.AddOrUpdate("info", info, (k, o) => info);
            return StatusCode(200, payload);
        }

        /// <summary>
        /// Sets the value of a reference key in a pathway.  A reference is a key/value setting.
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
            if (!_storage.PathwayList.ContainsKey(id))
            {
                _storage.PathwayList.Add(id, new Storage.Pathway());
            }
            var pathway = _storage.PathwayList[id];
            if (pathway.References.ContainsKey(key))
            {
                pathway.ReferenceSize -= pathway.References[key].Content.Length;
                pathway.References.Remove(key, out Lockbox ignored);
            }
            if (pathway.ReferenceSize + fixedValue.Content.Length > pathway.MaxTotalReferencesSize)
            {
                return StatusCode(429, $"Can't accept: Exceeds maximum reference value size of {pathway.MaxTotalReferencesSize}");
            }
            if (pathway.References.Count >= pathway.MaxReferencesCount)
            {
                return StatusCode(429, $"Can't accept: Exceeds maximum reference count of {pathway.MaxReferencesCount}");
            }
            pathway.ReferenceSize += fixedValue.Content.Length;
            pathway.References.AddOrUpdate(key.ToLower(), fixedValue, (k, o) => fixedValue);
            return StatusCode(200, "Reference accepted");
        }

        /// <summary>
        /// Gets the value of a reference key in a pathway.  A reference is a key/value setting.
        /// </summary>
        /// <returns>string which is the reference value (always text/plain)</returns>
        [HttpGet("reference/get/{id}/{key}", Name = "GetReference")]
        [Produces("application/json")]
        [ProducesResponseType(400, Type = typeof(string))]
        [ProducesResponseType(404, Type = typeof(string))]
        [ProducesResponseType(200, Type = typeof(Lockbox))]
        [ProducesResponseType(424, Type = typeof(string))]
        public IActionResult GetReference(
            [FromRoute] string id,
            [FromRoute] string key)
        {
            if (!_storage.PathwayList.ContainsKey(id))
            {
                _storage.PathwayList.Add(id, new Pathway());
            }
            var pathway = _storage.PathwayList[id];
            if (pathway.References.Count == 0 || !pathway.References.ContainsKey(key.ToLower()))
            {
                return StatusCode(200, new Lockbox());
            }
            return Ok(pathway.References[key.ToLower()]);
        }

        /// <summary>
        /// Reset: clear all pathways and their data
        /// </summary>
        /// <returns>string</returns>
        [HttpGet("reset", Name = "Reset")]
        [Produces("text/plain")]
        [ProducesResponseType(200, Type = typeof(string))]
        public IActionResult Reset()
        {
            _storage.Reset();
            return Ok("all pathway data cleared");
        }

        /// <summary>
        /// Shutdown: clear all pathways and their data
        /// </summary>
        /// <returns>string</returns>
        [HttpGet("shutdown", Name = "Shutdown")]
        [Produces("text/plain")]
        [ProducesResponseType(200, Type = typeof(string))]
        public IActionResult Shutdown()
        {
            _storage.Shutdown();
            return Ok("all pathway data cleared");
        }
    }
}
