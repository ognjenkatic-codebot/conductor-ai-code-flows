﻿using CodeFlows.GenAi.OpenAi.Workers;
using ConductorSharp.Engine.Extensions;
using ConductorSharp.Engine.Health;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenAI_API;
using Serilog;

await Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        var builder = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .AddJsonFile($"appsettings.json", true, true)
            .AddJsonFile($"appsettings.Development.json", true, true);

        var logger = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Build())
            .CreateLogger();

        var configuration = builder.Build();

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(logger);
        });

        services.AddScoped(ctx =>
        {
            var openAiApiKey =
                configuration.GetValue<string>("OpenAi:ApiKey")
                ?? throw new InvalidOperationException(
                    "OpenAi:ApiKey configuration value not set."
                );
            return new OpenAIAPI(new APIAuthentication(openAiApiKey));
        });

        services
            .AddConductorSharp(
                baseUrl: configuration.GetValue<string>("Conductor:BaseUrl")
                    ?? throw new InvalidOperationException(
                        "Conductor:BaseUrl configuration value not set."
                    )
            )
            .AddExecutionManager(
                maxConcurrentWorkers: configuration.GetValue("Conductor:MaxConcurrentWorkers", 10),
                sleepInterval: configuration.GetValue("Conductor:SleepInterval", 500),
                longPollInterval: configuration.GetValue("Conductor:LongPollInterval", 100),
                domain: null,
                typeof(Program).Assembly
            )
            .SetHealthCheckService<InMemoryHealthService>()
            .AddPipelines(pipelines =>
            {
                pipelines.AddExecutionTaskTracking();
                pipelines.AddContextLogging();
                pipelines.AddRequestResponseLogging();
                pipelines.AddValidation();
            });

        services.RegisterWorkerTask<RefactorProject.Handler>(opts =>
        {
            opts.TimeoutSeconds = opts.ResponseTimeoutSeconds = 600;
            opts.Description = "Refactors project files using ChatGPT";
        });
    })
    .Build()
    .RunAsync();
