using BOG.DropZone.Client.Model;
using System;
using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Text;
using System.Threading.Tasks;
using BOG.DropZone.Client.Helpers;
using System.Net;

namespace BOG.DropZone.Client
{
    /// <summary>
    /// C# Client for using BOG.DropZone API
    /// </summary>
    public class RestApi
    {
        private string _baseUrl;
        private HttpClient _client;

        /// <summary>
        /// Instantiate the class with the base Url
        /// </summary>
        /// <param name="baseUrl">The schema://server:port portion of the base URL.  Do net end with a slash.</param>
        public RestApi(string baseUrl)
        {
            _client = new HttpClient();
            _baseUrl = baseUrl;
        }

        /// <summary>
        /// Place a payload into the drop zone's queue
        /// </summary>
        /// <param name="dropzoneName">The name of the drop zone</param>
        /// <param name="payload">The content to queue as a string value</param>
        /// <returns></returns>
        public async Task DropOff(string dropzoneName, string payload)
        {
            var response = await _client.PostAsync(_baseUrl + $"/api/payload/dropoff/{dropzoneName}",
                new StringContent(
                    JsonConvert.SerializeObject(
                        new Lockbox
                        {
                            Content = payload
                        }),
                    Encoding.UTF8,
                    "application/json"));
            if (response.StatusCode != HttpStatusCode.OK) throw new RestApiNonSuccessException(response.StatusCode);
        }

        public async Task<string> Pickup(string dropzoneName)
        {
            Lockbox lockbox = null;
            var response = await _client.GetAsync(_baseUrl + $"/api/payload/pickup/{dropzoneName}", HttpCompletionOption.ResponseContentRead);
            if (response.StatusCode != HttpStatusCode.OK) throw new RestApiNonSuccessException(response.StatusCode);

            lockbox = Serializer<Lockbox>.FromJson(await response.Content.ReadAsStringAsync());
            return lockbox.Content;
        }

        public async Task SetReference(string dropzoneName, string key, string value)
        {
            var response = await _client.PostAsync(_baseUrl + $"/api/reference/set/{dropzoneName}/{key}",
                new StringContent(
                    JsonConvert.SerializeObject(
                        new Lockbox
                        {
                            Content = value
                        }),
                    Encoding.UTF8,
                    "application/json"));
            if (response.StatusCode != HttpStatusCode.OK) throw new RestApiNonSuccessException(response.StatusCode);
        }

        public async Task<string> GetReference(string dropzoneName, string key)
        {
            Lockbox lockbox = null;
            var response = await _client.GetAsync(_baseUrl + $"/api/reference/get/{dropzoneName}/{key}", HttpCompletionOption.ResponseContentRead);
            if (response.StatusCode != HttpStatusCode.OK) throw new RestApiNonSuccessException(response.StatusCode);

            lockbox = Serializer<Lockbox>.FromJson(await response.Content.ReadAsStringAsync());
            return lockbox.Content;
        }

        public async Task Reset()
        {
            var response = await _client.GetAsync(_baseUrl + $"/api/reset", HttpCompletionOption.ResponseContentRead);
            if (response.StatusCode != HttpStatusCode.OK) throw new RestApiNonSuccessException(response.StatusCode);
        }

        public async Task Shutdown()
        {
            var response = await _client.GetAsync(_baseUrl + $"/api/shutdown", HttpCompletionOption.ResponseContentRead);
            if (response.StatusCode != HttpStatusCode.OK) throw new RestApiNonSuccessException(response.StatusCode);
        }
    }
}
