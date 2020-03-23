// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace TestServer
{
    // Used for E2E tests that verify different overloads of MapFallbackToClientSideBlazor.
    public class StartupWithMapFallbackToClientSideBlazor
    {
        public StartupWithMapFallbackToClientSideBlazor(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            // The calls to `Map` allow us to test each of these overloads, while keeping them isolated.
            app.Map("/subdir/filepath", app =>
            {
                app.UseRouting();

                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapFallbackToFile("index.html");
                });
            });

            app.Map("/subdir/pattern_filepath", app =>
            {
                app.UseRouting();

                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapFallbackToFile("test/{*path:nonfile}", "index.html");
                });
            });

            app.Map("/subdir/assemblypath_filepath", app =>
            {
                app.UseRouting();

                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapFallbackToFile("index.html");
                });
            });

            app.Map("/subdir/assemblypath_pattern_filepath", app =>
            {
                app.UseRouting();

                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapFallbackToFile("test/{*path:nonfile}", "index.html");
                });
            });

            // The client-side files middleware needs to be here because the base href in hardcoded to /subdir/
            app.Map("/subdir", app =>
            {
                app.UseBlazorFrameworkFiles();
                app.UseStaticFiles();
            });
        }
    }
}
