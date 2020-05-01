using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace WebServer
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddRazorPages();
            services.AddSignalR();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
            }

            Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider provider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();

            provider.Mappings[".data"] = "";

            app.UseStaticFiles(new StaticFileOptions 
            {
                FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")),
                RequestPath = "",
                ContentTypeProvider = provider
            });

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapRazorPages();
                endpoints.MapHub<ServerlistHub>("/serverlistHub");
                endpoints.MapHub<ServerHub>("/serverHub");
            });
        }
    }
}
