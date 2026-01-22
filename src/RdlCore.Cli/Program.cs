namespace RdlCore.Cli;

/// <summary>
/// CLI entry point
/// </summary>
public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Axiom RDL-Core - Document to RDLC Conversion Tool")
        {
            new ConvertCommand(),
            new ValidateCommand()
        };

        return await rootCommand.InvokeAsync(args);
    }

    /// <summary>
    /// Creates the DI service provider
    /// </summary>
    public static IServiceProvider CreateServices(bool verbose = false, bool strictFidelity = false)
    {
        var services = new ServiceCollection();

        // Configuration
        var configBuilder = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true);

        if (strictFidelity)
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AxiomRdlCore:Generation:StrictFidelity"] = "true"
            });
        }

        var configuration = configBuilder.Build();

        services.Configure<AxiomRdlCoreOptions>(configuration.GetSection("AxiomRdlCore"));

        // Logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(verbose ? LogLevel.Debug : LogLevel.Warning);
        });

        // RDL Core services
        services.AddRdlCoreParsing();
        services.AddRdlCoreLogic();
        services.AddRdlCoreGeneration();
        services.AddRdlCoreRendering();
        services.AddRdlCoreAgent();

        return services.BuildServiceProvider();
    }
}
