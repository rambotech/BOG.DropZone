using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using BOG.DropZone.Client.Entity;
using BOG.DropZone.Client.Model;
using BOG.DropZone.Common.Dto;
using BOG.SwissArmyKnife;

namespace BOG.DropZone.Client
{
	/// <summary>
	/// C# Client for using BOG.DropZone API
	/// </summary>
	public class RestApiCalls
	{
		public HttpClient _Client;
		private readonly HttpClientHandler httpClientHandler = new HttpClientHandler();
		private readonly CipherUtility _Cipher = new CipherUtility();
		private static readonly DropZoneConfig _DropZoneConfig = new DropZoneConfig();

		/// <summary>
		/// Instantiate the class specifying a DropZoneConfig object.
		/// </summary>
		/// <param name="config">DropZoneConfig with items populated</param>
		public RestApiCalls(DropZoneConfig config)
		{
			if (config.UseEncryption)
			{
				if (string.IsNullOrWhiteSpace(config.Password) ^ string.IsNullOrWhiteSpace(config.Salt))
				{
					throw new ArgumentException("Both password and salt must be a non-empty string, or both must be null or empty.  You can not specify just one.");
				}
			}

			RestApiCalls._DropZoneConfig.BaseUrl = config.BaseUrl;
			RestApiCalls._DropZoneConfig.IgnoreSslCertProblems = config.IgnoreSslCertProblems;
			RestApiCalls._DropZoneConfig.ZoneName = config.ZoneName;
			RestApiCalls._DropZoneConfig.AccessToken = config.AccessToken;
			RestApiCalls._DropZoneConfig.AdminToken = config.AdminToken;
			RestApiCalls._DropZoneConfig.Password = config.UseEncryption ? config.Password : string.Empty;
			RestApiCalls._DropZoneConfig.Salt = config.UseEncryption ? config.Salt : string.Empty;
			RestApiCalls._DropZoneConfig.UseEncryption = config.UseEncryption && !string.IsNullOrEmpty(config.Password);
			RestApiCalls._DropZoneConfig.TimeoutSeconds = config.TimeoutSeconds;

			httpClientHandler.ServerCertificateCustomValidationCallback = ServerCertificateCustomValidation;

			_Client = new HttpClient(httpClientHandler)
			{
				Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds),
				BaseAddress = new Uri(config.BaseUrl)
			};
			if (!string.IsNullOrEmpty(config.AccessToken))
			{
				_Client.DefaultRequestHeaders.Add("AccessToken", config.AccessToken);
			}
			if (!string.IsNullOrEmpty(config.AdminToken))
			{
				_Client.DefaultRequestHeaders.Add("AdminToken", config.AdminToken);
			}

		}

		private static bool ServerCertificateCustomValidation(HttpRequestMessage requestMessage, X509Certificate2 certificate, X509Chain chain, SslPolicyErrors sslErrors)
		{
#if DEBUG
			// It is possible to inpect the certificate provided by server
			Debug.WriteLine($"Requested URI: {requestMessage.RequestUri}");
			Debug.WriteLine($"Effective date: {certificate.GetEffectiveDateString()}");
			Debug.WriteLine($"Expiration date: {certificate.GetExpirationDateString()}");
			Debug.WriteLine($"Issuer: {certificate.Issuer}");
			Debug.WriteLine($"Subject: {certificate.Subject}");
			Debug.WriteLine($"Errors: {sslErrors}");
			Debug.WriteLine($"DropZoneConfig.AllowSSL: {RestApiCalls._DropZoneConfig.IgnoreSslCertProblems}");
#endif
			var result = false;
			switch (sslErrors)
			{
				case SslPolicyErrors.None:
					result = true;
					break;

				case SslPolicyErrors.RemoteCertificateChainErrors:
				case SslPolicyErrors.RemoteCertificateNameMismatch:
				case SslPolicyErrors.RemoteCertificateNotAvailable:
					result = RestApiCalls._DropZoneConfig.IgnoreSslCertProblems;
					break;

				default:
					break;
			}
			return result;
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
		/// Set a a drop zone's metrics values.
		/// </summary>
		/// <param name="DropZoneMetrics">The values to set.</param>
		/// <returns>
		/// Result: Content-Type = string, no payload
		/// </returns>
		public async Task<Result> SetMetrics(DropZoneMetrics metrics)
		{
			var result = new Result
			{
				HandleAs = metrics.IsValid() ? Result.State.OK : Result.State.InvalidRequest
			};
			if (result.HandleAs == Result.State.OK)
			{
				try
				{
					var url = string.Format("{0}/{1}/{2}",
						_DropZoneConfig.BaseUrl,
						"api/metrics",
						System.Web.HttpUtility.UrlEncode(_DropZoneConfig.ZoneName)
					);
					var builder = new UriBuilder(url);
					var response = await _Client.PostAsync(builder.ToString(),
						new StringContent(
							Serializer<DropZoneMetrics>.ToJson(metrics),
							Encoding.UTF8,
							"application/json"));
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

						case HttpStatusCode.TooManyRequests:
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
			}
			return result;
		}

		/// <summary>
		/// Place a payload into a drop zone's queue.
		/// </summary>
		/// <param name="payload">The content to queue as a string value</param>
		/// <param name="metadata">Hanling for the metadatae.</param>
		/// <returns>
		/// Result: Content-Type = string, no payload
		/// </returns>
		public async Task<Result> DropOff(string data)
		{
			return await DropOff(data, new PayloadMetadata());
		}

		public async Task<Result> DropOff(string data, PayloadMetadata metadata)
		{
			var result = new Result
			{
				HandleAs = Result.State.OK
			};
			try
			{
				var datagram = BuildPayloadGram(data);
				var thisRecipient = string.IsNullOrWhiteSpace(metadata.Recipient) ? string.Empty : metadata.Recipient;
				var url = string.Format("{0}/{1}/{2}",
					_DropZoneConfig.BaseUrl,
					"api/payload/dropoff",
					System.Web.HttpUtility.UrlEncode(_DropZoneConfig.ZoneName)
				);
				var builder = new UriBuilder(url);
				var query = HttpUtility.ParseQueryString(builder.Query);
				if (thisRecipient != "*")
				{
					query["recipient"] = thisRecipient;
					builder.Query = query.ToString();
				}
				query["expiresOn"] = metadata.ExpiresOn.ToString("s");
				if (!string.IsNullOrWhiteSpace(metadata.Tracking))
				{
					var tracking = HttpUtility.ParseQueryString(builder.Query);
					query["tracking"] = metadata.Tracking;
				}
				builder.Query = query.ToString();
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
					case HttpStatusCode.TooManyRequests:
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
				var thisRecipient = string.IsNullOrWhiteSpace(recipient) ? string.Empty : recipient;
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
		/// Queries a payload with the tracking reference from the specified drop zone. A specific recipient may be spcified for an AND match.
		/// </summary>
		/// <param name="tracking">The tracking reference to locate in the payloads of this dropzone (i.e. dropped off but awating pickup).</param>
		/// <param name="recipient">optional: the tracking number AND recipient must match.</param>
		/// <param name="expireOn">optional: a new expiration time for the payload (null or empty string for no change).</param>
		/// <returns>
		/// Result: Content-Type = string (user-defined content), success has payload
		/// </returns>
		/// <returns></returns>
		public async Task<Result> Inquiry(string tracking, string recipient, string expireOn)
		{
			var result = new Result
			{
				HandleAs = Result.State.OK
			};
			try
			{
				var thisRecipient = string.IsNullOrWhiteSpace(recipient) ? string.Empty : recipient;
				var url = string.Format("{0}/{1}/{2}",
					_DropZoneConfig.BaseUrl,
					"api/payload/inquiry",
					System.Web.HttpUtility.UrlEncode(_DropZoneConfig.ZoneName)
				);
				var builder = new UriBuilder(url);
				var query = HttpUtility.ParseQueryString(builder.Query);
				if (thisRecipient != "*")
				{
					query["recipient"] = thisRecipient;
				}
				if (!string.IsNullOrWhiteSpace(tracking))
				{
					query["tracking"] = tracking;
				}
				if (!string.IsNullOrWhiteSpace(expireOn))
				{
					query["expireOn"] = expireOn;
				}
				builder.Query = query.ToString();
				var response = await _Client.GetAsync(builder.ToString());
				result.StatusCode = response.StatusCode;
				result.Message = response.ReasonPhrase;
				switch (response.StatusCode)
				{
					case HttpStatusCode.OK:
						result.Content = "true";
						result.HandleAs = Result.State.OK;
						result.Content = await response.Content.ReadAsStringAsync();
						result.CastType = "BOG.DropZone.Client.Entity.DropZoneInfo";
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
				var response = await _Client.GetAsync(_DropZoneConfig.BaseUrl + $"/api/statistics/{_DropZoneConfig.ZoneName}", HttpCompletionOption.ResponseContentRead);
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
					case HttpStatusCode.RequestEntityTooLarge:
					case HttpStatusCode.TooManyRequests:
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
		/// Drop a reference from the drop zone's key/value set.
		/// </summary>
		/// <param name="key">The string to identify this key</param>
		/// <returns>
		/// Result: Content-Type = string, no payload
		/// </returns>
		public async Task<Result> DropReference(string key)
		{
			var result = new Result
			{
				HandleAs = Result.State.OK
			};
			try
			{
				var builder = new UriBuilder(_DropZoneConfig.BaseUrl + $"/api/reference/drop/{_DropZoneConfig.ZoneName}/{key}");
				var response = await _Client.DeleteAsync(builder.ToString());
				result.StatusCode = response.StatusCode;
				result.Message = response.ReasonPhrase;

				switch (response.StatusCode)
				{
					case HttpStatusCode.OK:
						break;

					case HttpStatusCode.Unauthorized:
						result.HandleAs = Result.State.InvalidAuthentication;
						break;

					case HttpStatusCode.NotFound:
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
