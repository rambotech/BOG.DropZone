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
        private CipherUtility _cipher;

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
            _cipher = new CipherUtility(new AesManaged());
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
            try
            {
                var payload = _password == null ? data : _cipher.Encrypt(data, _password, _salt, Base64FormattingOptions.InsertLineBreaks);
                var response = await _client.PostAsync(_baseUrl + $"/api/payload/dropoff/{dropzoneName}",
                    new StringContent(
                        JsonConvert.SerializeObject(
                            new Lockbox
                            {
                                Content = payload,
                                MD5 = Hasher.GetHashFromStringContent(payload, Encoding.UTF8, Hasher.HashMethod.MD5)
                            }),
                        Encoding.UTF8,
                        "application/json"));
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
                        Lockbox lockbox = null;
                        lockbox = Serializer<Lockbox>.FromJson(await response.Content.ReadAsStringAsync());
                        if (string.Compare(
                            lockbox.MD5,
                            Hasher.GetHashFromStringContent(lockbox.Content, Encoding.UTF8, Hasher.HashMethod.MD5),
                            true) != 0)
                        {
                            result.HandleAs = Result.State.DataCompromised;
                            result.Message = "MD5 integrity check failed";
                        }
                        else
                        {
                            result.Content = _password == null ? lockbox.Content : _cipher.Decrypt(lockbox.Content, _password, _salt);
                        }
                        break;

                    case HttpStatusCode.Conflict:
                        result.HandleAs = Result.State.OverLimit;
                        result.Message = response.ReasonPhrase;
                        break;

                    case HttpStatusCode.NoContent:
                        result.HandleAs = Result.State.NoDataAvailable;
                        break;

                    case HttpStatusCode.Gone:
                        result.HandleAs = Result.State.DataCompromised;
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
            try
            {
                var payload = _password == null ? value : _cipher.Encrypt(value, _password, _salt, Base64FormattingOptions.InsertLineBreaks);
                var response = await _client.PostAsync(_baseUrl + $"/api/reference/set/{dropzoneName}/{key}",
                    new StringContent(
                        JsonConvert.SerializeObject(
                            new Lockbox
                            {
                                Content = payload,
                                MD5 = Hasher.GetHashFromStringContent(payload, Encoding.UTF8, Hasher.HashMethod.MD5)
                            }),
                        Encoding.UTF8,
                        "application/json"));
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
                        Lockbox lockbox = null;
                        lockbox = Serializer<Lockbox>.FromJson(await response.Content.ReadAsStringAsync());
                        if (string.Compare(
                                lockbox.MD5,
                                Hasher.GetHashFromStringContent(lockbox.Content, Encoding.UTF8, Hasher.HashMethod.MD5),
                                true) != 0)
                        {
                            result.HandleAs = Result.State.DataCompromised;
                            result.Message = "MD5 integrity check failed";
                        }
                        else
                        {
                            result.Content = _password == null ? lockbox.Content : _cipher.Decrypt(lockbox.Content, _password, _salt);
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

        public async Task <Result> Shutdown()
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
