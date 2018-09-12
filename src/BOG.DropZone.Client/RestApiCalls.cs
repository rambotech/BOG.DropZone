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
using BOG.DropZone.Common.Dto;

namespace BOG.DropZone.Client
{
    /// <summary>
    /// C# Client for using BOG.DropZone API
    /// </summary>
    public class RestApiCalls
    {
        private DropZoneConfig _Config;
        private HttpClient _Client;
        private bool _UseEncryption = false;
        private BOG.SwissArmyKnife.CipherUtility _Cipher = new CipherUtility();

        /// <summary>
        /// Instantiate the class with the base Url
        /// </summary>
        /// <param name="baseUrl">The schema://server:port portion of the base URL.  Do net end with a slash.</param>
        public RestApiCalls(string baseUrl)
        {
            RestApiCallsSetup(new DropZoneConfig
            {
                BaseUrl = baseUrl
            });
        }

        /// <summary>
        /// Instantiate the class with the base Url and access token.
        /// </summary>
        /// <param name="baseUrl">The schema://server:port portion of the base URL.  Do net end with a slash.</param>
        /// <param name="accessToken"></param>
        public RestApiCalls(string baseUrl, string accessToken)
        {
            RestApiCallsSetup(new DropZoneConfig
            {
                BaseUrl = baseUrl,
                AccessToken = accessToken
            });
        }

        /// <summary>
        /// Instantiate the class with all arguments explicit
        /// </summary>
        /// <param name="baseUrl"></param>
        /// <param name="accessToken"></param>
        /// <param name="password"></param>
        /// <param name="salt"></param>
        public RestApiCalls(string baseUrl, string accessToken, string password, string salt)
        {
            RestApiCallsSetup(new DropZoneConfig
            {
                BaseUrl = baseUrl,
                AccessToken = accessToken,
                Password = password,
                Salt = salt
            });
        }

        /// <summary>
        /// Instantiate the class with all arguments except access token
        /// </summary>
        /// <param name="baseUrl"></param>
        /// <param name="password"></param>
        /// <param name="salt"></param>
        public RestApiCalls(string baseUrl, string password, string salt)
        {
            RestApiCallsSetup(new DropZoneConfig
            {
                BaseUrl = baseUrl,
                Password = password,
                Salt = salt
            });
        }

        /// <summary>
        /// Instantiate the class with a DropZoneConfig object
        /// </summary>
        /// <param name="config">DropZoneConfig </param>
        public void RestApiCallsSetup(DropZoneConfig config)
        {
            _Config = config;
            _Client = new HttpClient();
            if (string.IsNullOrWhiteSpace(config.Password) ^ string.IsNullOrWhiteSpace(config.Salt))
            {
                throw new ArgumentException("Both password and salt must be a non-empty string, or both must be null or empty.  You can not specify just one.");
            }
            if (!string.IsNullOrEmpty(config.AccessToken))
            {
                _Client.DefaultRequestHeaders.Add("AccessToken", config.AccessToken);
            }
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
                    ? _Cipher.Encrypt(
                        string.Format("{0},{1},{2}",
                            MakeRandomCharacters(3, 5),
                            payload.Length,
                            MakeRandomCharacters(4, 7)),
                            _Config.Password,
                            _Config.Salt,
                            Base64FormattingOptions.None)
                    : payload.Length.ToString(),
                Payload = _UseEncryption
                    ? _Cipher.Encrypt(
                            payload,
                            _Config.Password,
                            _Config.Salt,
                            Base64FormattingOptions.None)
                    : payload,
                HashValidation = _UseEncryption
                    ? _Cipher.Encrypt(
                        Hasher.GetHashFromStringContent(payload, Encoding.UTF8, Hasher.HashMethod.SHA256),
                        _Config.Password,
                        _Config.Salt,
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
                originalPayload = _Cipher.Decrypt(gram.Payload, _Config.Password, _Config.Salt);
                originalPayloadHash = _Cipher.Decrypt(gram.HashValidation, _Config.Password, _Config.Salt);
                var payloadLength = _Cipher.Decrypt(gram.Length, _Config.Password, _Config.Salt);
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

        public async Task<Result> CheckHeartbeat()
        {
            var result = new Result { HandleAs = Result.State.OK };
            try
            {
                var response = await _Client.GetAsync(_Config.BaseUrl + $"/api/heartbeat", HttpCompletionOption.ResponseContentRead);
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
                var response = await _Client.PostAsync(_Config.BaseUrl + $"/api/payload/dropoff/{dropzoneName}",
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
                var response = await _Client.GetAsync(_Config.BaseUrl + $"/api/payload/pickup/{dropzoneName}", HttpCompletionOption.ResponseContentRead);
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
                var response = await _Client.GetAsync(_Config.BaseUrl + $"/api/payload/statistics/{dropzoneName}", HttpCompletionOption.ResponseContentRead);
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

        public async Task<Result> GetSecurity()
        {
            var result = new Result { HandleAs = Result.State.OK };
            try
            {
                var response = await _Client.GetAsync(_Config.BaseUrl + $"/api/securityinfo", HttpCompletionOption.ResponseContentRead);
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
            var l = datagram.Length;
            try
            {
                var response = await _Client.PostAsync(_Config.BaseUrl + $"/api/reference/set/{dropzoneName}/{key}",
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
                var response = await _Client.GetAsync(_Config.BaseUrl + $"/api/reference/get/{dropzoneName}/{key}", HttpCompletionOption.ResponseContentRead);
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

                    case HttpStatusCode.NoContent:
                        result.HandleAs = Result.State.NoDataAvailable;
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
                var response = await _Client.GetAsync(_Config.BaseUrl + $"/api/reference/list/{dropzoneName}", HttpCompletionOption.ResponseContentRead);
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

        public async Task<Result> Clear(string dropzoneName)
        {
            var result = new Result { HandleAs = Result.State.OK };
            try
            {
                var response = await _Client.GetAsync(_Config.BaseUrl + $"/api/clear/{dropzoneName}", HttpCompletionOption.ResponseContentRead);
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

        public async Task<Result> Reset()
        {
            var result = new Result { HandleAs = Result.State.OK };
            try
            {
                var response = await _Client.GetAsync(_Config.BaseUrl + $"/api/reset", HttpCompletionOption.ResponseContentRead);
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
                var response = await _Client.GetAsync(_Config.BaseUrl + $"/api/shutdown", HttpCompletionOption.ResponseContentRead);
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

        public DropZoneInfo GetStatisticsObject(string getStatisticsResponse)
        {
            return Serializer<DropZoneInfo>.FromJson(getStatisticsResponse);
        }

        public List<ClientWatch> GetClientWatchListObject(string getClientWatchListResponse)
        {
            return Serializer<List<ClientWatch>>.FromJson(getClientWatchListResponse);
        }
    }
}
