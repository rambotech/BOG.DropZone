using BOG.DropZone.Client.Entity;
using BOG.DropZone.Client.Model;
using BOG.DropZone.Common.Dto;
using BOG.SwissArmyKnife;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace BOG.DropZone.Client
{
	/// <summary>
	/// C# Client for using BOG.DropZone API
	/// </summary>
	public class RestApiCalls
	{
		public HttpClient _Client = new HttpClient();

		private readonly DropZoneConfig _DropZoneConfig = new DropZoneConfig();
		private readonly CipherUtility _Cipher = new CipherUtility();

		/// <summary>
		/// Instantiate the class specifying a DropZoneConfig object.
		/// </summary>
		/// <param name="config">DropZoneConfig with items populated</param>
		public RestApiCalls(DropZoneConfig config)
		{
			if (string.IsNullOrWhiteSpace(config.Password) ^ string.IsNullOrWhiteSpace(config.Salt))
			{
				throw new ArgumentException("Both password and salt must be a non-empty string, or both must be null or empty.  You can not specify just one.");
			}

			_Client.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds);
			_Client.BaseAddress = new Uri(config.BaseUrl);
			if (!string.IsNullOrEmpty(config.AccessToken))
			{
				_Client.DefaultRequestHeaders.Add("AccessToken", config.AccessToken);
			}
			if (!string.IsNullOrEmpty(config.AdminToken))
			{
				_Client.DefaultRequestHeaders.Add("AdminToken", config.AdminToken);
			}
			_DropZoneConfig = new DropZoneConfig
			{
				BaseUrl = config.BaseUrl,
				ZoneName = config.ZoneName,
				AccessToken = config.AccessToken,
				AdminToken = config.AdminToken,
				Password = config.Password,
				Salt = config.Salt,
				UseEncryption = config.UseEncryption && !string.IsNullOrEmpty(config.Password),
				TimeoutSeconds = config.TimeoutSeconds
			};
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
			try
			{
				return Serializer<PayloadGram>.ToJson(new PayloadGram
				{
					IsEncrypted = _DropZoneConfig.UseEncryption,
					Length = _DropZoneConfig.UseEncryption
						? _Cipher.Encrypt(
							string.Format("{0},{1},{2}",
								MakeRandomCharacters(3, 5),
								payload.Length,
								MakeRandomCharacters(4, 7)),
								_DropZoneConfig.Password,
								_DropZoneConfig.Salt,
								Base64FormattingOptions.None)
						: payload.Length.ToString(),
					Payload = _DropZoneConfig.UseEncryption
						? _Cipher.Encrypt(
								payload,
								_DropZoneConfig.Password,
								_DropZoneConfig.Salt,
								Base64FormattingOptions.None)
						: payload,
					HashValidation = _DropZoneConfig.UseEncryption
						? _Cipher.Encrypt(
							Hasher.GetHashFromStringContent(payload, Encoding.UTF8, Hasher.HashMethod.SHA256),
							_DropZoneConfig.Password,
							_DropZoneConfig.Salt,
							Base64FormattingOptions.None)
						: Hasher.GetHashFromStringContent(payload, Encoding.UTF8, Hasher.HashMethod.SHA256)
				});
			}
			catch (Exception err)
			{
				throw new ArgumentOutOfRangeException("Failure to build datagram from payload", err);
			}
		}


		private string ExtractPayloadFromGram(string payloadGram)
		{
			try
			{
				var gram = Serializer<PayloadGram>.FromJson(payloadGram);
				var originalPayload = gram.Payload;
				var originalPayloadHash = gram.HashValidation;
				int originalPayloadLength = -1;
				if (gram.IsEncrypted)
				{
					originalPayload = _Cipher.Decrypt(gram.Payload, _DropZoneConfig.Password, _DropZoneConfig.Salt);
					originalPayloadHash = _Cipher.Decrypt(gram.HashValidation, _DropZoneConfig.Password, _DropZoneConfig.Salt);
					var payloadLength = _Cipher.Decrypt(gram.Length, _DropZoneConfig.Password, _DropZoneConfig.Salt);
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
			catch (Exception err)
			{
				throw new ArgumentOutOfRangeException("Failure to unpack payload from datagram", err);
			}
		}
		#endregion

		/// <summary>
		/// Checks the heartbeat of the end point
		/// </summary>
		/// <returns>
		/// Result: Content-Type = string, no payload
		/// </returns>
		public async Task<Result> CheckHeartbeat()
		{
			var result = new Result
			{
				HandleAs = Result.State.OK
			};
			try
			{
				var response = await _Client.GetAsync(_DropZoneConfig.BaseUrl + $"/api/heartbeat", HttpCompletionOption.ResponseContentRead);
				result.StatusCode = response.StatusCode;
				result.Message = response.ReasonPhrase;
				switch (response.StatusCode)
				{
					case HttpStatusCode.OK:
						result.Content = await response.Content.ReadAsStringAsync();
						break;

					case HttpStatusCode.Unauthorized:
						result.HandleAs = Result.State.InvalidAuthentication;
						break;

					default:
						result.HandleAs = Result.State.UnexpectedResponse;
						break;
				}
			}
			catch (HttpRequestException errHttp)
			{
				result.HandleAs = Result.State.ConnectionFailed;
				result.Exception = errHttp;
			}
			catch (Exception exFatal)
			{
				result.HandleAs = Result.State.Fatal;
				result.Exception = exFatal;
			}

			return result;
		}

		/// <summary>
		/// Place a payload into a drop zone's queue.
		/// </summary>
		/// <param name="payload">The content to queue as a string value</param>
		/// <returns>
		/// Result: Content-Type = string, no payload
		/// </returns>
		public async Task<Result> DropOff(string data)
		{
			return await DropOff(data, default, null);
		}

		/// <summary>
		/// Place a payload into a drop zone's queue.
		/// </summary>
		/// <param name="payload">The content to queue as a string value</param>
		/// <param name="expires">A perish time when the payload should be discarded</param>
		/// <returns>
		/// Result: Content-Type = string, no payload
		/// </returns>
		public async Task<Result> DropOff(string data, DateTime expires)
		{
			return await DropOff(data, expires, null);
		}

		/// <summary>
		/// Place a payload into a drop zone's queue.
		/// </summary>
		/// <param name="payload">The content to queue as a string value</param>
		/// <param name="recipient">Drop the payload into a specific recipient's queue</param>
		/// <returns>
		/// Result: Content-Type = string, no payload
		/// </returns>
		public async Task<Result> DropOff(string data, string recipient)
		{
			return await DropOff(data, default, recipient);
		}

		/// <summary>
		/// Place a payload into a drop zone's queue. Note: Use text/plain for the content-type.
		/// </summary>
		/// <param name="payload">The content to queue as a string value</param>
		/// <param name="expires">A perish time when the payload should be discarded</param>
		/// <param name="recipient">Drop the payload into a specific recipient's queue</param>
		/// <returns>
		/// Result: Content-Type = string, no payload
		/// </returns>
		public async Task<Result> DropOff(string data, DateTime expires, string recipient)
		{
			var result = new Result
			{
				HandleAs = Result.State.OK
			};
			try
			{
				var datagram = BuildPayloadGram(data);
				var thisRecipient = string.IsNullOrWhiteSpace(recipient) ? "*" : recipient;
				var url = string.Format("{0}/{1}/{2}",
					_DropZoneConfig.BaseUrl,
					"api/payload/dropoff",
					System.Web.HttpUtility.UrlEncode(_DropZoneConfig.ZoneName)
				);
				var builder = new UriBuilder(url);
				if (thisRecipient != "*")
				{
					var query = HttpUtility.ParseQueryString(builder.Query);
					query["recipient"] = thisRecipient;
					builder.Query = query.ToString();
				}
				if (expires != default)
				{
					var query = HttpUtility.ParseQueryString(builder.Query);
					query["expires"] = expires.ToString();
					builder.Query = query.ToString();
				}
				var response = await _Client.PostAsync(builder.ToString(),
					new StringContent(
						datagram,
						Encoding.UTF8,
						"text/plain"));
				result.StatusCode = response.StatusCode;
				result.Message = response.ReasonPhrase;
				switch (response.StatusCode)
				{
					case HttpStatusCode.Created:
						result.Content = await response.Content.ReadAsStringAsync();
						break;

					case HttpStatusCode.Unauthorized:
						result.HandleAs = Result.State.InvalidAuthentication;
						break;

					case HttpStatusCode.BadRequest:
						result.HandleAs = Result.State.UnexpectedResponse;
						break;

					case HttpStatusCode.Conflict:
						result.HandleAs = Result.State.OverLimit;
						break;

					default:
						result.HandleAs = Result.State.UnexpectedResponse;
						break;
				}
			}
			catch (DataGramException errDataGram)
			{
				result.HandleAs = Result.State.DataCompromised;
				result.Exception = errDataGram;
			}
			catch (HttpRequestException errHttp)
			{
				result.HandleAs = Result.State.ConnectionFailed;
				result.Exception = errHttp;
			}
			catch (Exception exFatal)
			{
				result.HandleAs = Result.State.Fatal;
				result.Exception = exFatal;
			}
			return result;
		}

		/// <summary>
		/// Retrieve an available payload from the specified drop zone.
		/// </summary>
		/// <returns>
		/// Result: Content-Type = string (user-defined content), success has payload
		/// </returns>
		/// <returns></returns>
		public async Task<Result> Pickup()
		{
			return await Pickup(null);
		}

		/// <summary>
		/// Retrieve an available payload from the specified drop zone, for a specific recipient.
		/// </summary>
		/// <param name="recipient">optional: use null if not intended for a specific recipient</param>
		/// <returns>
		/// Result: Content-Type = string (user-defined content), success has payload
		/// </returns>
		/// <returns></returns>
		public async Task<Result> Pickup(string recipient)
		{
			var result = new Result
			{
				HandleAs = Result.State.OK
			};
			try
			{
				var thisRecipient = string.IsNullOrWhiteSpace(recipient) ? "*" : recipient;
				var url = string.Format("{0}/{1}/{2}",
					_DropZoneConfig.BaseUrl,
					"api/payload/pickup",
					System.Web.HttpUtility.UrlEncode(_DropZoneConfig.ZoneName)
				);
				var builder = new UriBuilder(url);
				if (thisRecipient != "*")
				{
					var query = HttpUtility.ParseQueryString(builder.Query);
					query["recipient"] = thisRecipient;
					builder.Query = query.ToString();
				}
				var response = await _Client.GetAsync(builder.ToString());
				result.StatusCode = response.StatusCode;
				result.Message = response.ReasonPhrase;
				switch (response.StatusCode)
				{
					case HttpStatusCode.OK:
						result.Content = ExtractPayloadFromGram(await response.Content.ReadAsStringAsync());
						break;

					case HttpStatusCode.Unauthorized:
						result.HandleAs = Result.State.InvalidAuthentication;
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
			catch (DataGramException errDataGram)
			{
				result.HandleAs = Result.State.DataCompromised;
				result.Message = errDataGram.Message;
				result.Exception = errDataGram.InnerException;
			}
			catch (HttpRequestException errHttp)
			{
				result.HandleAs = Result.State.ConnectionFailed;
				result.Exception = errHttp;
			}
			catch (Exception exFatal)
			{
				result.HandleAs = Result.State.Fatal;
				result.Exception = exFatal;
			}
			return result;
		}

		/// <summary>
		/// Returns the statistics for the specified dropzone.
		/// </summary>
		/// <returns>
		/// Result: Content-Type = System.Collections.Generic.List<BOG.DropZone.Common.DropZoneInfo>
		/// </returns>
		public async Task<Result> GetStatistics()
		{
			var result = new Result
			{
				HandleAs = Result.State.OK
			};
			try
			{
				var response = await _Client.GetAsync(_DropZoneConfig.BaseUrl + $"/api/payload/statistics/{_DropZoneConfig.ZoneName}", HttpCompletionOption.ResponseContentRead);
				result.StatusCode = response.StatusCode;
				result.Message = response.ReasonPhrase;
				switch (response.StatusCode)
				{
					case HttpStatusCode.OK:
						result.Content = await response.Content.ReadAsStringAsync();
						result.CastType = "BOG.DropZone.Common.DropZoneInfo";
						break;

					case HttpStatusCode.Unauthorized:
						result.HandleAs = Result.State.InvalidAuthentication;
						break;

					default:
						result.HandleAs = Result.State.UnexpectedResponse;
						break;

				}
			}
			catch (HttpRequestException errHttp)
			{
				result.HandleAs = Result.State.ConnectionFailed;
				result.Exception = errHttp;
			}
			catch (Exception exFatal)
			{
				result.HandleAs = Result.State.Fatal;
				result.Exception = exFatal;
			}
			return result;
		}

		/// <summary>
		/// Returns the securty statistics for the endpoints.
		/// </summary>
		/// <returns>
		/// Result: Content-Type = System.Collections.Generic.List<BOG.DropZone.Common.ClientListWatch>
		/// </returns>
		public async Task<Result> GetSecurity()
		{
			var result = new Result
			{
				HandleAs = Result.State.OK
			};
			try
			{
				var response = await _Client.GetAsync(_DropZoneConfig.BaseUrl + $"/api/securityinfo", HttpCompletionOption.ResponseContentRead);
				result.StatusCode = response.StatusCode;
				result.Message = response.ReasonPhrase;
				switch (response.StatusCode)
				{
					case HttpStatusCode.OK:
						result.Content = await response.Content.ReadAsStringAsync();
						result.CastType = "System.Collections.Generic.List<BOG.DropZone.Common.ClientWatch>";
						break;

					case HttpStatusCode.Unauthorized:
						result.HandleAs = Result.State.InvalidAuthentication;
						break;

					case HttpStatusCode.InternalServerError:
						result.HandleAs = Result.State.ServerError;
						break;

					default:
						result.HandleAs = Result.State.UnexpectedResponse;
						break;

				}
			}
			catch (HttpRequestException errHttp)
			{
				result.HandleAs = Result.State.ConnectionFailed;
				result.Exception = errHttp;
			}
			catch (Exception exFatal)
			{
				result.HandleAs = Result.State.Fatal;
				result.Exception = exFatal;
			}
			return result;
		}

		/// <summary>
		/// Create/Update a reference into a drop zone's key/value set.
		/// </summary>
		/// <param name="key">The string to identify this key</param>
		/// <param name="value">The value for the key</param>
		/// <returns>
		/// Result: Content-Type = string, no payload
		/// </returns>
		public async Task<Result> SetReference(string key, string value)
		{
			return await SetReference(key, value, DateTime.MaxValue);
		}

		/// <summary>
		/// Create/Update a reference into a drop zone's key/value set.
		/// </summary>
		/// <param name="key">The string to identify this key</param>
		/// <param name="value">The value for the key</param>
		/// <param name="expires">(Optional) A perish time when the reference should be discarded</param>
		/// <returns>
		/// Result: Content-Type = string, no payload
		/// </returns>
		public async Task<Result> SetReference(string key, string value, DateTime expires)
		{
			var result = new Result
			{
				HandleAs = Result.State.OK
			};
			try
			{
				var datagram = BuildPayloadGram(value);
				var l = datagram.Length;
				var builder = new UriBuilder(_DropZoneConfig.BaseUrl + $"/api/reference/set/{_DropZoneConfig.ZoneName}/{key}");
				if (expires != DateTime.MaxValue)
				{
					var query = HttpUtility.ParseQueryString(builder.Query);
					query["expires"] = expires.ToString();
					builder.Query = query.ToString();
				}
				var response = await _Client.PostAsync(builder.ToString(), new StringContent(datagram, Encoding.UTF8, "text/plain"));
				result.StatusCode = response.StatusCode;
				result.Message = response.ReasonPhrase;

				switch (response.StatusCode)
				{
					case HttpStatusCode.Created:
						result.Content = await response.Content.ReadAsStringAsync();
						break;

					case HttpStatusCode.Unauthorized:
						result.HandleAs = Result.State.InvalidAuthentication;
						break;

					case HttpStatusCode.Conflict:
						result.HandleAs = Result.State.OverLimit;
						break;

					case HttpStatusCode.InternalServerError:
						result.HandleAs = Result.State.ServerError;
						break;

					default:
						result.HandleAs = Result.State.UnexpectedResponse;
						break;
				}
			}
			catch (DataGramException errDataGram)
			{
				result.HandleAs = Result.State.DataCompromised;
				result.Message = errDataGram.Message;
				result.Exception = errDataGram.InnerException;
			}
			catch (HttpRequestException errHttp)
			{
				result.HandleAs = Result.State.ConnectionFailed;
				result.Exception = errHttp;
			}
			catch (Exception exFatal)
			{
				result.HandleAs = Result.State.Fatal;
				result.Exception = exFatal;
			}
			return result;
		}

		/// <summary>
		/// Retrieve a value from the key/value pair set in the drop zone.
		/// </summary>
		/// <param name="key">The key for the value to retrieve</param>
		/// <returns>
		/// Result: Content-Type = string (user-defined content), success has payload
		/// </returns>
		public async Task<Result> GetReference(string key)
		{
			var result = new Result
			{
				HandleAs = Result.State.OK
			};
			try
			{
				var response = await _Client.GetAsync(_DropZoneConfig.BaseUrl + $"/api/reference/get/{_DropZoneConfig.ZoneName}/{key}", HttpCompletionOption.ResponseContentRead);
				result.StatusCode = response.StatusCode;
				result.Message = response.ReasonPhrase;

				switch (response.StatusCode)
				{
					case HttpStatusCode.OK:
						result.Content = ExtractPayloadFromGram(await response.Content.ReadAsStringAsync());
						break;

					case HttpStatusCode.Unauthorized:
						result.HandleAs = Result.State.InvalidAuthentication;
						break;

					case HttpStatusCode.Conflict:
						result.HandleAs = Result.State.OverLimit;
						break;

					case HttpStatusCode.NoContent:
						result.HandleAs = Result.State.NoDataAvailable;
						break;

					case HttpStatusCode.InternalServerError:
						result.HandleAs = Result.State.ServerError;
						break;

					default:
						result.HandleAs = Result.State.UnexpectedResponse;
						break;

				}
			}
			catch (DataGramException errDataGram)
			{
				result.HandleAs = Result.State.DataCompromised;
				result.Message = errDataGram.Message;
				result.Exception = errDataGram.InnerException;
			}
			catch (HttpRequestException errHttp)
			{
				result.HandleAs = Result.State.ConnectionFailed;
				result.Exception = errHttp;
			}
			catch (Exception exFatal)
			{
				result.HandleAs = Result.State.Fatal;
				result.Exception = exFatal;
			}
			return result;
		}

		/// <summary>
		/// Returns a list of key name in the reference key/value set.
		/// </summary>
		/// <returns>
		/// Result: Content-Type = string (List<string></string>), success has payload
		/// </returns>
		public async Task<Result> ListReferences()
		{
			var result = new Result
			{
				HandleAs = Result.State.OK
			};
			try
			{
				var response = await _Client.GetAsync(_DropZoneConfig.BaseUrl + $"/api/reference/list/{_DropZoneConfig.ZoneName}", HttpCompletionOption.ResponseContentRead);
				result.StatusCode = response.StatusCode;
				result.Message = response.ReasonPhrase;

				switch (response.StatusCode)
				{
					case HttpStatusCode.OK:
						result.Content = await response.Content.ReadAsStringAsync();
						result.CastType = "System.Collections.Generic.List<System.String>";
						break;

					case HttpStatusCode.Unauthorized:
						result.HandleAs = Result.State.InvalidAuthentication;
						break;

					case HttpStatusCode.Conflict:
						result.HandleAs = Result.State.OverLimit;
						break;

					case HttpStatusCode.InternalServerError:
						result.HandleAs = Result.State.ServerError;
						break;

					default:
						result.HandleAs = Result.State.UnexpectedResponse;
						break;
				}
			}
			catch (HttpRequestException errHttp)
			{
				result.HandleAs = Result.State.ConnectionFailed;
				result.Exception = errHttp;
			}
			catch (Exception exFatal)
			{
				result.HandleAs = Result.State.Fatal;
				result.Exception = exFatal;
			}
			return result;
		}

		/// <summary>
		/// Clears out all payloads, references and statistics for a sigle dropzone.
		/// </summary>
		/// <returns></returns>
		public async Task<Result> Clear()
		{
			var result = new Result
			{
				HandleAs = Result.State.OK
			};
			try
			{
				var response = await _Client.GetAsync(_DropZoneConfig.BaseUrl + $"/api/clear/{_DropZoneConfig.ZoneName}", HttpCompletionOption.ResponseContentRead);
				result.StatusCode = response.StatusCode;
				result.Message = response.ReasonPhrase;

				switch (response.StatusCode)
				{
					case HttpStatusCode.OK:
						result.Content = await response.Content.ReadAsStringAsync();
						break;

					case HttpStatusCode.Unauthorized:
						result.HandleAs = Result.State.InvalidAuthentication;
						break;

					case HttpStatusCode.InternalServerError:
						result.HandleAs = Result.State.ServerError;
						break;

					default:
						result.HandleAs = Result.State.UnexpectedResponse;
						break;
				}
			}
			catch (HttpRequestException errHttp)
			{
				result.HandleAs = Result.State.ConnectionFailed;
				result.Exception = errHttp;
			}
			catch (Exception exFatal)
			{
				result.HandleAs = Result.State.Fatal;
				result.Exception = exFatal;
			}
			return result;
		}

		/// <summary>
		/// Drops all dropzones.  Resets to site.
		/// </summary>
		/// <returns></returns>
		public async Task<Result> Reset()
		{
			var result = new Result
			{
				HandleAs = Result.State.OK
			};
			try
			{
				var response = await _Client.GetAsync(_DropZoneConfig.BaseUrl + $"/api/reset", HttpCompletionOption.ResponseContentRead);
				result.StatusCode = response.StatusCode;
				result.Message = response.ReasonPhrase;

				switch (response.StatusCode)
				{
					case HttpStatusCode.OK:
						result.Content = await response.Content.ReadAsStringAsync();
						break;

					case HttpStatusCode.Unauthorized:
						result.HandleAs = Result.State.InvalidAuthentication;
						break;

					case HttpStatusCode.InternalServerError:
						result.HandleAs = Result.State.ServerError;
						break;

					default:
						result.HandleAs = Result.State.UnexpectedResponse;
						break;
				}
			}
			catch (HttpRequestException errHttp)
			{
				result.HandleAs = Result.State.ConnectionFailed;
				result.Exception = errHttp;
			}
			catch (Exception exFatal)
			{
				result.HandleAs = Result.State.Fatal;
				result.Exception = exFatal;
			}
			return result;
		}

		/// <summary>
		/// Stops the web application (system exit).  In an Azure environment, this may not stop the web application from running,
		/// but is the equivalent of a site reboot.
		/// </summary>
		/// <returns></returns>
		public async Task<Result> Shutdown()
		{
			var result = new Result
			{
				HandleAs = Result.State.OK
			};
			try
			{
				var response = await _Client.GetAsync(_DropZoneConfig.BaseUrl + $"/api/shutdown", HttpCompletionOption.ResponseContentRead);
				result.StatusCode = response.StatusCode;
				result.Message = response.ReasonPhrase;

				switch (response.StatusCode)
				{
					case HttpStatusCode.OK:
						result.Content = await response.Content.ReadAsStringAsync();
						break;

					case HttpStatusCode.Unauthorized:
						result.HandleAs = Result.State.InvalidAuthentication;
						break;

					case HttpStatusCode.InternalServerError:
						result.HandleAs = Result.State.ServerError;
						break;

					default:
						result.HandleAs = Result.State.UnexpectedResponse;
						break;
				}
			}
			catch (HttpRequestException errHttp)
			{
				result.HandleAs = Result.State.ConnectionFailed;
				result.Exception = errHttp;
			}
			catch (Exception exFatal)
			{
				result.HandleAs = Result.State.Fatal;
				result.Exception = exFatal;
			}
			return result;
		}

		/// <summary>
		/// For GetStatistics, this method can be called to generic the object from Result.Content
		/// </summary>
		/// <param name="getStatisticsResponse">the string of json</param>
		/// <returns></returns>
		public DropZoneInfo GetStatisticsObject(string getStatisticsResponse)
		{
			return Serializer<DropZoneInfo>.FromJson(getStatisticsResponse);
		}

		/// <summary>
		/// For GetSecurity, this method can be called to generic the object from Result.Content
		/// </summary>
		/// <param name="getClientWatchListResponse">the string of json</param>
		/// <returns></returns>
		public List<ClientWatch> GetClientWatchListObject(string getClientWatchListResponse)
		{
			return Serializer<List<ClientWatch>>.FromJson(getClientWatchListResponse);
		}
	}
}
