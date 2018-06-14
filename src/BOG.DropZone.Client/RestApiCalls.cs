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
using BOG.DropZone.Client.Entity;

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
        private bool _UseEncryption = false;
        private BOG.SwissArmyKnife.CipherUtility _cipher = new CipherUtility();

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
            _UseEncryption = true;
        }

        #region Payload Helper Methods
        private string MakeRandomCharacters(int minLength, int maxLength)
        {
            var result = new StringBuilder();
            var rnd = new Random(DateTime.Now.Millisecond);
            int length = rnd.Next(minLength, maxLength);
            for (int index = 0; index < length; index++)
            {
                result.Append((char)rnd.Next(48, 57));
            }
            return result.ToString();
        }

        private string BuildPayloadGram(string payload)
        {
            return Serializer<PayloadGram>.ToJson(new PayloadGram
            {
                IsEncrypted = _UseEncryption,
                Length = _UseEncryption
                    ? _cipher.Encrypt(
                        string.Format("{0},{1},{2}",
                            MakeRandomCharacters(3, 5),
                            payload.Length,
                            MakeRandomCharacters(4, 7)),
                            _password,
                            _salt,
                            Base64FormattingOptions.None)
                    : payload.Length.ToString(),
                Payload = _UseEncryption
                    ? _cipher.Encrypt(
                            payload,
                            _password,
                            _salt,
                            Base64FormattingOptions.None)
                    : payload,
                HashValidation = _UseEncryption
                    ? _cipher.Encrypt(
                        Hasher.GetHashFromStringContent(payload, Encoding.UTF8, Hasher.HashMethod.SHA256),
                        _password,
                        _salt,
                        Base64FormattingOptions.None)
                    : Hasher.GetHashFromStringContent(payload, Encoding.UTF8, Hasher.HashMethod.SHA256)
            });
        }

        private string ExtractPayloadFromGram(string payloadGram)
        {
            var gram = Serializer<PayloadGram>.FromJson(payloadGram);
            var originalPayload = gram.Payload;
            var originalPayloadHash = gram.HashValidation;
            int originalPayloadLength = -1;
            if (gram.IsEncrypted)
            {
                originalPayload = _cipher.Decrypt(gram.Payload, _password, _salt);
                originalPayloadHash = _cipher.Decrypt(gram.HashValidation, _password, _salt);
                var payloadLength = _cipher.Decrypt(gram.Length, _password, _salt);
                int.TryParse(payloadLength.Split(new char[] { ',' })[1], out originalPayloadLength);
            }
            else
            {
                int.TryParse(gram.Length, out originalPayloadLength);
            }
            if (originalPayloadLength != originalPayload.Length)
            {
                throw new ArgumentOutOfRangeException($"Expected length of {originalPayload.Length}, but got {originalPayloadLength}");
            }
            if (string.Compare(
                Hasher.GetHashFromStringContent(originalPayload, Encoding.UTF8, Hasher.HashMethod.SHA256),
                originalPayloadHash, false) != 0)
            {
                throw new ArgumentOutOfRangeException($"Expected hash validation value of {gram.HashValidation}, but got {originalPayloadHash}");
            }
            return originalPayload;
        }
        #endregion

        /// <summary>
        /// Place a payload into the drop zone's queue
        /// </summary>
        /// <param name="dropzoneName">The name of the drop zone</param>
        /// <param name="payload">The content to queue as a string value</param>
        /// <returns></returns>
        public async Task<Result> DropOff(string dropzoneName, string data)
        {
            var result = new Result { HandleAs = Result.State.OK };
            var datagram = BuildPayloadGram(data);
            try
            {
                var response = await _client.PostAsync(_baseUrl + $"/api/payload/dropoff/{dropzoneName}",
                    new StringContent(
                        datagram,
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
                        try
                        {
                            result.Message = ExtractPayloadFromGram(await response.Content.ReadAsStringAsync());
                        }
                        catch (ArgumentOutOfRangeException err)
                        {
                            result.HandleAs = Result.State.DataCompromised;
                            result.Message = err.Message;
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
            var datagram = BuildPayloadGram(value);
            try
            {
                var response = await _client.PostAsync(_baseUrl + $"/api/reference/set/{dropzoneName}/{key}",
                    new StringContent(datagram, Encoding.UTF8, "text/plain"));
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
                        try
                        {
                            var body = await response.Content.ReadAsStringAsync();
                            result.Message = body.Length == 0 ? string.Empty : ExtractPayloadFromGram(body);
                        }
                        catch (ArgumentOutOfRangeException err)
                        {
                            result.HandleAs = Result.State.DataCompromised;
                            result.Message = err.Message;
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
