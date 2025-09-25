using BOG.DropZone.Interface;
//using Certes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.PlatformAbstractions;
using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BOG.DropZone
{
	/// <summary>
	/// 
	/// </summary>
	public class Startup
	{
		/// <summary>
		/// The entry point for startup.
		/// </summary>
		/// <param name="configuration"></param>
		public Startup(IConfiguration configuration)
		{
			Configuration = configuration;
		}

		/// <summary>
		/// The Configuration object.
		/// </summary>
		public IConfiguration Configuration { get; }

		/// <summary>
		/// This method gets called by the runtime. Use this method to add services to the container.
		/// </summary>
		/// <param name="services">(injected)</param>
		public void ConfigureServices(IServiceCollection services)
		{
			// static across controllers and calls.
			services.AddSingleton<IStorage, MemoryStorage>();
			services.AddSingleton<IAssemblyVersion, AssemblyVersion>();
			services.AddRouting();

			services.Configure<CookiePolicyOptions>(options =>
			{
				// This lambda determines whether user consent for non-essential cookies is needed for a given request.
				options.CheckConsentNeeded = context => false;
				options.MinimumSameSitePolicy = SameSiteMode.None;
			});
			services.AddMvc(options =>
				{
					options.InputFormatters.Insert(0, new RawRequestBodyFormatter());
					options.EnableEndpointRouting = false;
				})
				.AddJsonOptions(options =>
				{
					options.JsonSerializerOptions.WriteIndented = true;
					options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault;
					options.JsonSerializerOptions.IgnoreReadOnlyProperties = false;
					options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
					options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
				});
			services.AddHttpContextAccessor();

			var valueHttp = Configuration.GetValue<int>("HttpPort", 5005);
			var valueHttps = Configuration.GetValue<int>("HttpsPort", 5445);

			var valueUseReverseProxy = Configuration.GetValue<bool>("UseReverseProxy", false);
			var knownProxies = Configuration.GetValue<string>("KnownProxies", String.Empty);
			if (valueUseReverseProxy && !string.IsNullOrWhiteSpace(knownProxies))
			{
				string[] ipAddresses = knownProxies.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
				services.Configure<ForwardedHeadersOptions>(options =>
				{
					foreach (var ip in ipAddresses) options.KnownProxies.Add(IPAddress.Parse(ip));
				});
			}

			var useLetsEncrypt = Configuration.GetValue<bool>("UseLetsEncrypt", false);
			if (valueHttp == 80 && valueHttps == 443 && useLetsEncrypt)
			{
				// Register Let's Encrypt for SSL, if enabled in the config.
				services.AddLettuceEncrypt(o =>
				{
					o.AcceptTermsOfService = Configuration.GetValue<bool>("LettuceEncrypt:AcceptTermsOfService");
					o.EmailAddress = Configuration.GetValue<string>("LettuceEncrypt:EmailAddress");
					o.DomainNames = Configuration.GetValue<string[]>("LettuceEncrypt:Domains");
					o.RenewalCheckPeriod = TimeSpan.FromHours(Configuration.GetValue<double>("LettuceEncrypt:RenewalCheckPeriodHours", 6));
					o.RenewDaysInAdvance = TimeSpan.FromDays(Configuration.GetValue<double>("LettuceEncrypt:RenewDaysInAdvance", 2));
					o.UseStagingServer = Configuration.GetValue<bool>("LettuceEncrypt:UseStagingServer");
				});
			}

			// Register the Swagger generator, defining one or more Swagger documents
			services.AddSwaggerGen(c =>
			{
				c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
				{
					Version = $"v{this.GetType().Assembly.GetName().Version}",
					Title = "BOG.DropZone API",
					Description = "A non-secure, volatile drop-off and pickup location for quick, inter-application data handoff",
					Contact = new Microsoft.OpenApi.Models.OpenApiContact { Name = "John J Schultz", Email = "", Url = new Uri("https://github.com/rambotech") },
					License = new Microsoft.OpenApi.Models.OpenApiLicense { Name = "MIT", Url = new Uri("https://opensource.org/licenses/MIT") }
				});
				// Set the comments path for the Swagger JSON and UI.
				var xmlPath = Path.Combine(PlatformServices.Default.Application.ApplicationBasePath, "BOG.DropZone.xml");
				c.IncludeXmlComments(xmlPath);
			});
		}

		/// <summary>
		/// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
		/// </summary>
		/// <param name="app">(injected)</param>
		/// <param name="env">(injected)</param>
		/// <param name="serviceProvider">(injected)</param>
		public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IServiceProvider serviceProvider)
		{
			var storageArea = serviceProvider.GetService<IStorage>();

			storageArea.AccessToken = Configuration.GetValue<string>("AccessToken");
			Console.WriteLine($"AccessToken: {storageArea.AccessToken}");

			storageArea.AdminToken = Configuration.GetValue<string>("AdminToken");
			Console.WriteLine($"AdminToken: {storageArea.AdminToken}");

			var configValue = Configuration.GetValue<string>("MaxDropzones");
			if (!string.IsNullOrWhiteSpace(configValue))
			{
				storageArea.MaxDropzones = int.Parse(configValue);
			}
			Console.WriteLine($"MaxDropzones: {storageArea.MaxDropzones}");

			configValue = Configuration.GetValue<string>("MaximumFailedAttemptsBeforeLockout");
			if (!string.IsNullOrWhiteSpace(configValue))
			{
				storageArea.MaximumFailedAttemptsBeforeLockout = int.Parse(configValue);
			}
			Console.WriteLine($"MaximumFailedAttemptsBeforeLockout: {storageArea.MaximumFailedAttemptsBeforeLockout}");

			configValue = Configuration.GetValue<string>("LockoutSeconds");
			if (!string.IsNullOrWhiteSpace(configValue))
			{
				storageArea.LockoutSeconds = int.Parse(configValue);
			}
			Console.WriteLine($"LockoutSeconds: {storageArea.LockoutSeconds}");

			if (env.EnvironmentName == "development")
			{
				app.UseDeveloperExceptionPage();
			}
			else
			{
				app.UseExceptionHandler("/Home/Error");
			}

			var valueUseReverseProxy = Configuration.GetValue<bool>("UseReverseProxy", false);
			if (valueUseReverseProxy)
			{
				app.UseForwardedHeaders(new ForwardedHeadersOptions
				{
					ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
				});
			}

			app.UseMiddleware<ExceptionMiddleware>();
			app.UseStaticFiles();
			app.UseCookiePolicy();
			app.UseSwagger();

			app.UseSwaggerUI(c =>
			{
				c.SwaggerEndpoint("/swagger/v1/swagger.json", "BOG.DropZone Server API v1");
			});

			app.Use((context, next) =>
			{
				context.Response.Headers.Append("X-Server-App", $"BOG.DropZone v{new AssemblyVersion().Version}");
				return next();
			});

			app.UseMvc(routes =>
			{
				routes.MapRoute(
					name: "default",
					template: "{controller}/{action=Index}/{id?}"
				);

				routes.MapRoute(
					name: "root",
					template: "",
					defaults: new { controller = "Home", action = "Index" }
				);
			});
		}
	}
}
