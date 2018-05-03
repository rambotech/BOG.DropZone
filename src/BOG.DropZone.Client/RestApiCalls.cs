using BOG.DropZone.Client.Helpers;
using BOG.DropZone.Client.Model;
using BOG.SwissArmyKnife;
using System;
using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Collections.Generic;

namespace BOG.DropZone.Client
{
    /// <summary>
    /// C# Client for using BOG.DropZone API
    /// </summary>
    public class RestApiCalls
    {
        private string _baseUrl;
        private string _password = null;
        private string _salt = null;
        private HttpClient _client;

        /// <summary>
        /// Instantiate the class with the base Url
        /// </summary>
        /// <param name="baseUrl">The schema://server:port portion of the base URL.  Do net end with a slash.</param>
        public RestApiCalls(string baseUrl)
        {
            _client = new HttpClient();
            _baseUrl = baseUrl;
        }

        public RestApiCalls(string baseUrl, string password, string salt)
        {
            _client = new HttpClient();
            _baseUrl = baseUrl;
            _password = password;
            _salt = salt;
            if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(salt))
            {
                throw new ArgumentException("Neither password nor salt can be null or an empty string.");
            }
        }

        /// <summary>
        /// Place a payload into the drop zone's queue
        /// </summary>
        /// <param name="dropzoneName">The name of the drop zone</param>
        /// <param name="payload">The content to queue as a string value</param>
        /// <returns></returns>
        public async Task<Result> DropOff(string dropzoneName, string data)
        {
            var result = new Result { HandleAs = Result.State.OK };
            string payload = null;
            try
            {
                if (_password == null)
                {
                    payload = data ?? string.Empty;
                }
                else
                {
                    var secureGram = new SecureGram();
                    secureGram.Message = data;
                    secureGram.MessageLength = data.Length;
                    secureGram.CreateGramContent(_password, _salt);
                    payload = JsonConvert.SerializeObject(secureGram);
                }
                var response = await _client.PostAsync(_baseUrl + $"/api/payload/dropoff/{dropzoneName}", 
                    new StringContent(
                        payload,
                        Encoding.UTF8,
                        "text/plain"));
                result.StatusCode = response.StatusCode;
                switch (response.StatusCode)
                {
                    case HttpStatusCode.OK:
                        result.Content = await response.Content.ReadAsStringAsync();
                        break;

                    case HttpStatusCode.BadRequest:
                        result.HandleAs = Result.State.UnexpectedResponse;
                        result.Message = response.ReasonPhrase;
                        break;

                    case HttpStatusCode.Conflict:
                        result.HandleAs = Result.State.OverLimit;
                        result.Message = response.ReasonPhrase;
                        break;

                    case HttpStatusCode.InternalServerError:
                        result.HandleAs = Result.State.ServerError;
                        result.Message = response.ReasonPhrase;
                        break;

                    default:
                        result.HandleAs = Result.State.UnexpectedResponse;
                        result.Message = response.ReasonPhrase;
                        break;

                }
            }
            catch (HttpRequestException httpEx)
            {
                result.HandleAs = Result.State.ConnectionFailed;
                result.Message = httpEx.Message;
            }
            catch (Exception ex)
            {
                result.HandleAs = Result.State.Fatal;
                result.Exception = ex;
            }
            return result;
        }

        public async Task<Result> Pickup(string dropzoneName)
        {
            var result = new Result { HandleAs = Result.State.OK };
            try
            {
                var response = await _client.GetAsync(_baseUrl + $"/api/payload/pickup/{dropzoneName}", HttpCompletionOption.ResponseContentRead);
                result.StatusCode = response.StatusCode;
                switch (response.StatusCode)
                {
                    case HttpStatusCode.OK:
                        result.Message = await response.Content.ReadAsStringAsync();
                        if (!string.IsNullOrEmpty(_password))
                        {
                            var secureGram = new SecureGram();
                            secureGram.LoadGramContent(result.Message, _password, _salt);
                            result.Message = secureGram.Message;
                            if (secureGram.Message.Length != secureGram.MessageLength)
                            {
                                result.HandleAs = Result.State.DataCompromised;
                                result.Message = $"Secure payload length mismatch: expected {secureGram.MessageLength} but was {secureGram.Message.Length}";
                            }
                        }
                        break;

                    case HttpStatusCode.BadRequest:
                        result.HandleAs = Result.State.UnexpectedResponse;
                        result.Message = response.ReasonPhrase;
                        break;

                    case HttpStatusCode.Conflict:
                        result.HandleAs = Result.State.OverLimit;
                        result.Message = response.ReasonPhrase;
                        break;

                    case HttpStatusCode.NoContent:
                    case HttpStatusCode.Gone:
                        result.HandleAs = Result.State.NoDataAvailable;
                        break;

                    case HttpStatusCode.InternalServerError:
                        result.HandleAs = Result.State.ServerError;
                        result.Message = response.ReasonPhrase;
                        break;

                    default:
                        result.HandleAs = Result.State.UnexpectedResponse;
                        result.Message = response.ReasonPhrase;
                        break;

                }
            }
            catch (HttpRequestException httpEx)
            {
                result.HandleAs = Result.State.ConnectionFailed;
                result.Message = httpEx.Message;
            }
            catch (Exception ex)
            {
                result.HandleAs = Result.State.Fatal;
                result.Exception = ex;
            }
            return result;
        }

        public async Task<Result> GetStatistics(string dropzoneName)
        {
            var result = new Result { HandleAs = Result.State.OK };
            try
            {
                var response = await _client.GetAsync(_baseUrl + $"/api/payload/statistics/{dropzoneName}", HttpCompletionOption.ResponseContentRead);
                result.StatusCode = response.StatusCode;
                switch (response.StatusCode)
                {
                    case HttpStatusCode.OK:
                        result.Content = await response.Content.ReadAsStringAsync();
                        break;

                    case HttpStatusCode.Conflict:
                        result.HandleAs = Result.State.OverLimit;
                        result.Message = response.ReasonPhrase;
                        break;

                    case HttpStatusCode.InternalServerError:
                        result.HandleAs = Result.State.ServerError;
                        result.Message = response.ReasonPhrase;
                        break;

                    default:
                        result.HandleAs = Result.State.UnexpectedResponse;
                        result.Message = response.ReasonPhrase;
                        break;

                }
            }
            catch (HttpRequestException httpEx)
            {
                result.HandleAs = Result.State.ConnectionFailed;
                result.Message = httpEx.Message;
            }
            catch (Exception ex)
            {
                result.HandleAs = Result.State.Fatal;
                result.Exception = ex;
            }
            return result;
        }

        public async Task<Result> SetReference(string dropzoneName, string key, string value)
        {
            var result = new Result { HandleAs = Result.State.OK };
            string payload = null;
            try
            {
                if (_password == null)
                {
                    payload = value ?? string.Empty;
                }
                else
                {
                    var secureGram = new SecureGram();
                    secureGram.Message = value;
                    secureGram.MessageLength = value.Length;
                    secureGram.CreateGramContent(_password, _salt);
                    payload = JsonConvert.SerializeObject(secureGram);
                }
                var response = await _client.PostAsync(_baseUrl + $"/api/reference/set/{dropzoneName}/{key}",
                    new StringContent(payload, Encoding.UTF8, "text/plain"));
                result.StatusCode = response.StatusCode;
                switch (response.StatusCode)
                {
                    case HttpStatusCode.OK:
                        result.Content = await response.Content.ReadAsStringAsync();
                        break;

                    case HttpStatusCode.Conflict:
                        result.HandleAs = Result.State.OverLimit;
                        result.Message = response.ReasonPhrase;
                        break;

                    case HttpStatusCode.InternalServerError:
                        result.HandleAs = Result.State.ServerError;
                        result.Message = response.ReasonPhrase;
                        break;

                    default:
                        result.HandleAs = Result.State.UnexpectedResponse;
                        result.Message = response.ReasonPhrase;
                        break;
                }
            }
            catch (HttpRequestException httpEx)
            {
                result.HandleAs = Result.State.ConnectionFailed;
                result.Message = httpEx.Message;
            }
            catch (Exception ex)
            {
                result.HandleAs = Result.State.Fatal;
                result.Exception = ex;
            }
            return result;
        }

        public async Task<Result> GetReference(string dropzoneName, string key)
        {
            var result = new Result { HandleAs = Result.State.OK };
            try
            {
                var response = await _client.GetAsync(_baseUrl + $"/api/reference/get/{dropzoneName}/{key}", HttpCompletionOption.ResponseContentRead);
                result.StatusCode = response.StatusCode;
                switch (response.StatusCode)
                {
                    case HttpStatusCode.OK:
                        result.Message = await response.Content.ReadAsStringAsync();
                        if (!string.IsNullOrEmpty(_password))
                        {
                            var secureGram = new SecureGram();
                            secureGram.LoadGramContent(result.Message, _password, _salt);
                            result.Message = secureGram.Message;
                            if (secureGram.Message.Length != secureGram.MessageLength)
                            {
                                result.HandleAs = Result.State.DataCompromised;
                                result.Message = $"Secure value length mismatch: expected {secureGram.MessageLength} but was {secureGram.Message.Length}";
                            }
                        }
                        break;

                    case HttpStatusCode.Conflict:
                        result.HandleAs = Result.State.OverLimit;
                        result.Message = response.ReasonPhrase;
                        break;

                    case HttpStatusCode.InternalServerError:
                        result.HandleAs = Result.State.ServerError;
                        result.Message = response.ReasonPhrase;
                        break;

                    default:
                        result.HandleAs = Result.State.UnexpectedResponse;
                        result.Message = response.ReasonPhrase;
                        break;

                }
            }
            catch (HttpRequestException httpEx)
            {
                result.HandleAs = Result.State.ConnectionFailed;
                result.Message = httpEx.Message;
            }
            catch (Exception ex)
            {
                result.HandleAs = Result.State.Fatal;
                result.Exception = ex;
            }
            return result;
        }

        public async Task<Result> ListReferences(string dropzoneName)
        {
            var result = new Result { HandleAs = Result.State.OK };
            try
            {
                var response = await _client.GetAsync(_baseUrl + $"/api/reference/list/{dropzoneName}", HttpCompletionOption.ResponseContentRead);
                result.StatusCode = response.StatusCode;
                switch (response.StatusCode)
                {
                    case HttpStatusCode.OK:
                        result.Content = await response.Content.ReadAsStringAsync();
                        break;

                    case HttpStatusCode.Conflict:
                        result.HandleAs = Result.State.OverLimit;
                        result.Message = response.ReasonPhrase;
                        break;

                    case HttpStatusCode.InternalServerError:
                        result.HandleAs = Result.State.ServerError;
                        result.Message = response.ReasonPhrase;
                        break;

                    default:
                        result.HandleAs = Result.State.UnexpectedResponse;
                        result.Message = response.ReasonPhrase;
                        break;
                }
            }
            catch (HttpRequestException httpEx)
            {
                result.HandleAs = Result.State.ConnectionFailed;
                result.Message = httpEx.Message;
            }
            catch (Exception ex)
            {
                result.HandleAs = Result.State.Fatal;
                result.Exception = ex;
            }
            return result;
        }

        public async Task<Result> Reset()
        {
            var result = new Result { HandleAs = Result.State.OK };
            try
            {
                var response = await _client.GetAsync(_baseUrl + $"/api/reset", HttpCompletionOption.ResponseContentRead);
                result.StatusCode = response.StatusCode;
                switch (response.StatusCode)
                {
                    case HttpStatusCode.OK:
                        result.Content = await response.Content.ReadAsStringAsync();
                        break;

                    case HttpStatusCode.InternalServerError:
                        result.HandleAs = Result.State.ServerError;
                        result.Message = response.ReasonPhrase;
                        break;

                    default:
                        result.HandleAs = Result.State.UnexpectedResponse;
                        result.Message = response.ReasonPhrase;
                        break;

                }
            }
            catch (HttpRequestException httpEx)
            {
                result.HandleAs = Result.State.ConnectionFailed;
                result.Message = httpEx.Message;
            }
            catch (Exception ex)
            {
                result.HandleAs = Result.State.Fatal;
                result.Exception = ex;
            }
            return result;
        }

        public async Task<Result> Shutdown()
        {
            var result = new Result { HandleAs = Result.State.OK };
            try
            {
                var response = await _client.GetAsync(_baseUrl + $"/api/shutdown", HttpCompletionOption.ResponseContentRead);
                result.StatusCode = response.StatusCode;
                switch (response.StatusCode)
                {
                    case HttpStatusCode.OK:
                        result.Content = await response.Content.ReadAsStringAsync();
                        break;

                    case HttpStatusCode.Conflict:
                        result.HandleAs = Result.State.OverLimit;
                        result.Message = response.ReasonPhrase;
                        break;

                    case HttpStatusCode.InternalServerError:
                        result.HandleAs = Result.State.ServerError;
                        result.Message = response.ReasonPhrase;
                        break;

                    default:
                        result.HandleAs = Result.State.UnexpectedResponse;
                        result.Message = response.ReasonPhrase;
                        break;

                }
            }
            catch (HttpRequestException httpEx)
            {
                result.HandleAs = Result.State.ConnectionFailed;
                result.Message = httpEx.Message;
            }
            catch (Exception ex)
            {
                result.HandleAs = Result.State.Fatal;
                result.Exception = ex;
            }
            return result;
        }
    }
}
