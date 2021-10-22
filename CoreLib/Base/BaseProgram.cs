using System;
using CoreLib.Base;
using CoreLib.Utils;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace CoreLib.Base {
    /// <summary>
    /// Program class.
    /// </summary>
    public static class BaseProgram {
        /// <summary>
        /// Setup configuration.
        /// </summary>
        /// <param name="appSettingsName"></param>
        /// <returns></returns>
        public static IConfiguration SetupConfiguration(string appSettingsName) => new ConfigurationBuilder().SetBasePath(Tools.GetExecutableRootPath()).AddJsonFile(appSettingsName, false, true).AddEnvironmentVariables().Build();

        /// <summary>
        /// The host builder.
        /// </summary>
        /// <param name="args"></param>
        /// <returns>The created builder</returns>
        static IHostBuilder CreateHostBuilder<T>(string[] args, IConfiguration configuration) where T : BaseStartup {
            return Host.CreateDefaultBuilder(args).ConfigureWebHostDefaults(webBuilder => webBuilder.UseConfiguration(configuration).ConfigureLogging(log => log.AddSerilog(Log.Logger)).UseStartup<T>().UseKestrel());
        }

        /// <summary>
        /// Base main program.
        /// </summary>
        /// <param name="args"></param>
        public static int BaseMain<T>(string[] args, IConfiguration configuration) where T : BaseStartup {
            // Create the Serilog logger, and configure the sinks.
            Log.Logger = new LoggerConfiguration().Enrich.WithCaller().ReadFrom.Configuration(configuration).CreateLogger();

            // Wrap creating and running the host in a try-catch block.
            try {
                Log.Information("Starting host");
                CreateHostBuilder<T>(args, configuration).Build().Run();
                return 0;
            }
            catch (Exception e) {
                Log.Fatal(e, "Host terminated unexpectedly");
                return 1;
            }
            finally {
                Log.CloseAndFlush();
            }
        }
    };
}