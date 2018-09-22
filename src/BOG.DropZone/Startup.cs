using BOG.DropZone.Interface;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.PlatformAbstractions;
using Swashbuckle.AspNetCore.Swagger;
using System;
using System.IO;

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
            services.AddRouting();

            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => false;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });
            services.AddMvc(
                o => o.InputFormatters.Insert(0, new RawRequestBodyFormatter())
                ).SetCompatibilityVersion(CompatibilityVersion.Version_2_1);
            services.AddHttpContextAccessor();

            // Register the Swagger generator, defining one or more Swagger documents
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new Info
                {
                    Version = $"v{this.GetType().Assembly.GetName().Version.ToString()}",
                    Title = "BOG.DropZone API",
                    Description = "A non-secure, volatile drop-off and pickup location for quick, inter-application data transfer",
                    TermsOfService = "None",
                    Contact = new Contact { Name = "John J Schultz", Email = "", Url = "https://github.com/rambotech" },
                    License = new License { Name = "MIT", Url = "https://opensource.org/licenses/MIT" }
                });
                // Set the comments path for the Swagger JSON and UI.
                var xmlPath = Path.Combine(PlatformServices.Default.Application.ApplicationBasePath, "BOG.DropZone.xml");
                c.IncludeXmlComments(xmlPath);
                c.DescribeAllEnumsAsStrings();
                c.DescribeStringEnumsInCamelCase();
            });
        }

        /// <summary>
        /// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        /// </summary>
        /// <param name="app">(injected)</param>
        /// <param name="env">(injected)</param>
        /// <param name="serviceProvider">(injected)</param>
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, IServiceProvider serviceProvider)
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

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
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
                context.Response.Headers.Add("X-Server-App", "BOG.DropZone");
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
