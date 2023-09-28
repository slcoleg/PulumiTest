using System;
using System.Collections.Generic;
using System.Linq;
//using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
//using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace infra_web
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILogger<Startup> logger)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
      
            app.UseRouting();
            
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/", async context =>
                {
                    await context.Response.WriteAsync("healthy");
                });

                endpoints.MapGet("/api", async context =>
                {
                    var apiAddress = Environment.GetEnvironmentVariable("ApiAddress");
                    var apiPort = Environment.GetEnvironmentVariable("ApiPort");
                    var apiMethod = Environment.GetEnvironmentVariable("ApiMethod");
                    await context.Response.WriteAsync($"Api Connection: {apiAddress}:{apiPort}/{apiMethod}");
                });
                
                endpoints.MapGet("/weather", async context =>
                {
                    var apiAddress = Environment.GetEnvironmentVariable("ApiAddress") ?? "n/a";
                    var apiPort = Environment.GetEnvironmentVariable("ApiPort") ?? "n/a";
                    var apiMethod = Environment.GetEnvironmentVariable("ApiMethod") ?? "n/a";
                    Console.WriteLine($"Api Connection: {apiAddress}:{apiPort}/{apiMethod}");
                    if (string.IsNullOrEmpty(apiAddress) || string.IsNullOrEmpty(apiAddress) || string.IsNullOrEmpty(apiAddress))
                    {
                      logger.LogError($"Cannot connect to: {apiAddress}:{apiPort}/{apiMethod}");
                      await context.Response.WriteAsync($"Empty address error: {apiAddress}:{apiPort}/{apiMethod}");
                      return;
                    }
                    using var hc = new HttpClient();
                    try
                    {
                      logger.LogInformation($"Trying connect to: {apiAddress}:{apiPort}/{apiMethod}");
                      using var apiResponse = await hc.GetAsync($"{apiAddress}:{apiPort}/{apiMethod}");
                      var apiResult = await apiResponse.Content.ReadAsStringAsync();
                      await context.Response.WriteAsync(apiResult);
                    }
                    catch (Exception ex)
                    {
                      logger.LogError($"Cannot connect to: {apiAddress}:{apiPort}/{apiMethod}");
                      await context.Response.WriteAsync($"Error connecting to: {apiAddress}:{apiPort}/{apiMethod}<br>{ex.ToString()}");
                    }
                });
            });
        }
    }
}
