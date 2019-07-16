using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

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
            var value = config.GetValue<string>("HttpPort") ?? "5000";
            useUrls.Add($"http://*:{value}");

            value = config.GetValue<string>("HttpsPort");
            if (!string.IsNullOrWhiteSpace(value))
            {
                useUrls.Add($"https://*:{value}");
            }

            host
                .UseUrls(useUrls.ToArray())
                .UseStartup<Startup>();
                
            return host.Build();
        }
    }
}
