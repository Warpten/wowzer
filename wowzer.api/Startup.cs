using System.Reflection;

using Microsoft.AspNetCore.StaticFiles;
using Microsoft.OpenApi.Models;

using wowzer.api.Services;

namespace wowzer.api
{
    public class Startup(IConfiguration configuration)
    {
        public IConfiguration Configuration { get; } = configuration;

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();

            services.AddSingleton<IKeyStore, KeyStore>();

            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo()
                {
                    Version = "v1",
                    Title = "wowzer API",
                    Description = "An ASP.NET Web Core API for interfacing with multiple World of Warcraft instances for dataminers",
                });

                var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseRouting();
            app.UseEndpoints(endpoints => endpoints.MapControllers());
            app.UseDefaultFiles();

            var extensionProvider = new FileExtensionContentTypeProvider();
            extensionProvider.Mappings.Add(".data", "application/octet-stream");

            app.UseStaticFiles(new StaticFileOptions()
            {
                ContentTypeProvider = extensionProvider,
                OnPrepareResponse = ctx =>
                {
                    ctx.Context.Response.Headers.TryAdd("Expires", "-1");
                    ctx.Context.Response.Headers.TryAdd("Cache-Control", "no-cache, no-store");
                }
            });
        }
    }
}
