using BOG.DropZone.Common.Dto;
using System;

namespace BOG.DropZone.Client
{
	/// <summary>
	/// Defines the parameters to connect to and use a drop zone.
	/// </summary>
	public class DropZoneConfig : ICloneable
	{
		/// <summary>
		/// The URI of the drop zone ( e.g. http://localhost:5000, https://localhost:5001, https://io.mydomain.com:5001)
		/// </summary>
		public string BaseUrl { get; set; } = null;
		/// <summary>
		/// Whether a self-signed certificate should be allowed.  Defaults to false.  Only use true for internal (outside internet) testing.
		/// Internally, this will override an SslPolicyErrors.RemoteCertificateChainErrors state to be ignored since self-signed has no root authority
		/// outside of the local server.
		/// </summary>
		public bool IgnoreSslCertProblems { get; set; } = false;
		/// <summary>
		/// The unique name of the drop zone
		/// </summary>
		public string ZoneName { get; set; } = null;
		/// <summary>
		/// The token value for non-admin access (read / write, create zone, drop zone).
		/// </summary>
		public string AccessToken { get; set; } = null;
		/// <summary>
		/// The token value for admin access (drop all zones, restart service).
		/// </summary>
		public string AdminToken { get; set; } = null;
		/// <summary>
		/// If encrypting the password value
		/// </summary>
		public string Password { get; set; } = null;
		/// <summary>
		/// If encrypting the salt value
		/// </summary>
		public string Salt { get; set; } = null;
		/// <summary>
		/// Whether to use encryption. Usually not needed for a secure Http (https://) listener.
		/// </summary>
		public bool UseEncryption { get; set; } = false;
		/// <summary>
		/// The timeout value for connections.  Increase this if payloads require more than the existing value to arrive.
		/// </summary>
		public int TimeoutSeconds { get; set; } = 15;
		/// <summary>
		/// An optional override object for the default zone metrics.
		/// </summary>
		public DropZoneMetrics ZoneMetricsDefault { get; set; } = new DropZoneMetrics();

		public object Clone()
		{
			return new DropZoneConfig
			{
				BaseUrl = this.BaseUrl,
				IgnoreSslCertProblems = this.IgnoreSslCertProblems,
				ZoneName = this.ZoneName,
				AccessToken = this.AccessToken,
				AdminToken = this.AdminToken,
				Password = this.Password,
				Salt = this.Salt,
				UseEncryption = this.UseEncryption,
				TimeoutSeconds = this.TimeoutSeconds,
				ZoneMetricsDefault = (DropZoneMetrics)this.ZoneMetricsDefault.Clone()
			};
		}
	}
}
