using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace BOG.DropZone
{
	/// <summary>
	/// Startup location
	/// </summary>
	public class Program
	{
		/// <summary>
		/// Entry point
		/// </summary>
		/// <param name="args"></param>
		public static void Main(string[] args)
		{
			BuildWebHost(args).Run();
		}

		/// <summary>
		/// Construct the site
		/// </summary>
		/// <param name="args"></param>
		/// <returns></returns>
		public static IWebHost BuildWebHost(string[] args)
		{
			var config = new ConfigurationBuilder()
				.SetBasePath(Directory.GetCurrentDirectory())
				.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
				.AddEnvironmentVariables()
				.AddCommandLine(args)
		   .Build();

			var host = WebHost
				.CreateDefaultBuilder(args)
				.UseConfiguration(config);

			var useUrls = new List<string>();
			var valueHttp = config.GetValue<int>("HttpPort");
			useUrls.Add($"http://*:{valueHttp}");

			var valueHttps = config.GetValue<int>("HttpsPort");
			if (valueHttps > 0)
			{
				useUrls.Add($"https://*:{valueHttps}");
				host.UseSetting("https_port", valueHttps.ToString());
				host.UseKestrel();
			}

			host
				.UseUrls(useUrls.ToArray())
				.UseStartup<Startup>();

			return host.Build();
		}
	}
}
