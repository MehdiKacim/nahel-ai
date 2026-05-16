using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nahel.Server;
using Nahel.Server.Configuration;

namespace Nahel.Cli.Commands;

public sealed class StartCommand : ICommand
{
    public async Task<int> ExecuteAsync(string[] args)
    {
        var cliConfigPath = "nahel.json";
        var cliHost = (string?)null;
        var cliPort = (int?)null;
        var noDashboard = false;
        var lan = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--host": cliHost = args[++i]; break;
                case "--port": cliPort = int.Parse(args[++i]); break;
                case "--config": cliConfigPath = args[++i]; break;
                case "--no-dashboard": noDashboard = true; break;
                case "--lan": lan = true; break;
            }
        }

        // Resolve config file path
        var resolvedConfig = ResolveConfigPath(cliConfigPath);
        if (resolvedConfig == null)
        {
            Console.WriteLine("Warning: no nahel.json found. Using default configuration.");
            Console.WriteLine("Searched: current directory, executable directory, %USERPROFILE%\\.nahel\\nahel.json");
        }

        var builder = WebApplication.CreateBuilder();
        if (resolvedConfig != null)
        {
            builder.Configuration.AddJsonFile(resolvedConfig, optional: false, reloadOnChange: true);
        }
        builder.Configuration.AddEnvironmentVariables(prefix: "NAHEL_");

        // Read host/port from config, allow CLI override
        var hostOptions = builder.Configuration.GetSection("Nahel").Get<NahelHostOptions>() ?? new NahelHostOptions();
        var host = cliHost ?? (lan ? "0.0.0.0" : hostOptions.Host);
        var port = cliPort ?? hostOptions.Port;

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
        if (!noDashboard && hostOptions.DashboardEnabled) Console.WriteLine($"Dashboard: http://{host}:{port}/dashboard");

        if (lan && hostOptions.RequireApiKeyOnLan && !hostOptions.AllowUnauthenticatedLan)
        {
            if (string.IsNullOrEmpty(hostOptions.ApiKey) || hostOptions.ApiKey == "local")
            {
                Console.WriteLine("WARNING: LAN mode enabled but no strong API key configured. Set Nahel:ApiKey or Nahel:AllowUnauthenticatedLan=true.");
            }
        }

        await app.RunAsync();
        return 0;
    }

    private static string? ResolveConfigPath(string explicitPath)
    {
        if (File.Exists(explicitPath)) return Path.GetFullPath(explicitPath);

        var exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        if (!string.IsNullOrEmpty(exeDir))
        {
            var candidate = Path.Combine(exeDir, explicitPath);
            if (File.Exists(candidate)) return candidate;
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var userConfig = Path.Combine(userProfile, ".nahel", "nahel.json");
        if (File.Exists(userConfig)) return userConfig;

        return null;
    }
}
