using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BOG.DropZone.Interface;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.PlatformAbstractions;
using Swashbuckle.AspNetCore.Swagger;

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
        /// Configuration for services.
        /// </summary>
        public IConfiguration Configuration { get; }

        /// <summary>
        /// This method gets called by the runtime. Use this method to add services to the container.
        /// </summary>
        /// <param name="services">(injected)</param>
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();
            services.AddMvc(o => o.InputFormatters.Insert(0, new RawRequestBodyFormatter()));

            // static across controllers and calls.
            services.AddSingleton<IStorage, MemoryStorage>();

            // Register the Swagger generator, defining one or more Swagger documents
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new Swashbuckle.AspNetCore.Swagger.Info
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

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseMvc();

            app.UseSwagger();

            // Enable middleware to serve swagger-ui (HTML, JS, CSS, etc.), specifying the Swagger JSON endpoint.
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
                    template: "{controller}/{action=Index}/{id?}");
            });
        }
    }
}
