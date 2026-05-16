using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nahel.Server;

namespace Nahel.Cli.Commands;

public sealed class StartCommand : ICommand
{
    public async Task<int> ExecuteAsync(string[] args)
    {
        var host = "127.0.0.1";
        var port = 11435;
        var configPath = "nahel.json";
        var noDashboard = false;
        var lan = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--host": host = args[++i]; break;
                case "--port": port = int.Parse(args[++i]); break;
                case "--config": configPath = args[++i]; break;
                case "--no-dashboard": noDashboard = true; break;
                case "--lan": lan = true; break;
            }
        }

        if (lan) host = "0.0.0.0";

        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddJsonFile(configPath, optional: true, reloadOnChange: true);
        builder.Configuration.AddEnvironmentVariables(prefix: "NAHEL_");
        builder.WebHost.UseUrls($"http://{host}:{port}");
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.SetMinimumLevel(LogLevel.Information);
        builder.Services.AddNahelServer(builder.Configuration);

        var app = builder.Build();
        app.UseRouting();
        app.UseNahelServer();
        app.MapNahelRoutes();

        Console.WriteLine($"Nahel running on http://{host}:{port}");
        if (!noDashboard) Console.WriteLine($"Dashboard: http://{host}:{port}/dashboard");
        await app.RunAsync();
        return 0;
    }
}
