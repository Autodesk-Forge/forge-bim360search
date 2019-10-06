/////////////////////////////////////////////////////////////////////
// Copyright (c) Autodesk, Inc. All rights reserved
// Written by Forge Partner Development
//
// Permission to use, copy, modify, and distribute this software in
// object code form for any purpose and without fee is hereby granted,
// provided that the above copyright notice appears in all copies and
// that both that copyright notice and the limited warranty and
// restricted rights notice below appear in all supporting
// documentation.
//
// AUTODESK PROVIDES THIS PROGRAM "AS IS" AND WITH ALL FAULTS.
// AUTODESK SPECIFICALLY DISCLAIMS ANY IMPLIED WARRANTY OF
// MERCHANTABILITY OR FITNESS FOR A PARTICULAR USE.  AUTODESK, INC.
// DOES NOT WARRANT THAT THE OPERATION OF THE PROGRAM WILL BE
// UNINTERRUPTED OR ERROR FREE.
/////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.Console;
using Hangfire.Mongo;
using Hangfire.Mongo.Database;
using MongoDB.Driver;
using Hangfire.MemoryStorage;
using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;

namespace forgeSample
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
            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
            {
                // memory storage of jobs
                services.AddHangfire(config =>
                {
                    config.UseConsole();
                    config.UseMemoryStorage(); // for local testing
                });
            }
            else
            {
                // Mongodb storage of jobs
                Console.WriteLine(string.Format("Connecting to {0}", Config.DatabaseName));
                services.AddHangfire(config =>
                {
                    config.UseConsole();
                    config.UseMongoStorage(
                        Config.ConnectionString,
                        Config.DatabaseName,
                        new MongoStorageOptions()
                        {
                            CheckConnection = false,
                            MigrationOptions = new MongoMigrationOptions()
                            {
                                Strategy = MongoMigrationStrategy.Drop
                            }
                        });
                });
            }

            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);
            services.AddSignalR();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseHsts();
            }

            app.UseDefaultFiles();
            app.UseStaticFiles();
            app.UseHttpsRedirection();
            app.UseMvc();

             // Hangfire
            app.UseHangfireDashboard("/dashboard", new DashboardOptions
            {
                Authorization = new[] { new MyAuthorizationFilter() },
                DisplayStorageConnectionString = false
                //IsReadOnlyFunc = (DashboardContext context) => true
            });
            var options = new BackgroundJobServerOptions
            {
                ServerName = "Metadata",
                Queues = new[] { "metadata" },
                WorkerCount = Config.ParallelJobs
            };
            app.UseHangfireServer(options);

            options = new BackgroundJobServerOptions
            {
                ServerName = "Index",
                Queues = new[] { "index" },
                WorkerCount = 3,
            };
            app.UseHangfireServer(options);
            GlobalConfiguration.Configuration.UseFilter(new PreserveOriginalQueueAttribute());

            app.UseSignalR(routes =>
            {
                routes.MapHub<Controllers.ModelDerivativeHub>("/api/signalr/modelderivative");
            });
        }
    }

    public class MyAuthorizationFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize(DashboardContext context)
        {
            return true; // open for now, wait until 1.7
        }
    }

    public class PreserveOriginalQueueAttribute : JobFilterAttribute, IApplyStateFilter
    {
        public void OnStateApplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
        {
            var enqueuedState = context.NewState as EnqueuedState;

            // Activating only when enqueueing a background job
            if (enqueuedState != null)
            {
                // Checking if an original queue is already set
                var originalQueue = JobHelper.FromJson<string>(context.Connection.GetJobParameter(
                    context.BackgroundJob.Id,
                    "OriginalQueue"));

                if (originalQueue != null)
                {
                    // Override any other queue value that is currently set (by other filters, for example)
                    enqueuedState.Queue = originalQueue;
                }
                else
                {
                    // Queueing for the first time, we should set the original queue
                    context.Connection.SetJobParameter(
                        context.BackgroundJob.Id,
                        "OriginalQueue",
                        JobHelper.ToJson(enqueuedState.Queue));
                }
            }
        }

        public void OnStateUnapplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
        {
        }
    }
}
