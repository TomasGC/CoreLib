using System.Diagnostics;
using System.Reflection;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;

namespace CoreLib.Utils {
    /// <summary>
    /// Class to help to show namespace, classname, methodname and line number in the logs.
    /// </summary>
    public sealed class LogEnricher : ILogEventEnricher {
        /// <summary>
        /// Enrich the log with namespace, classname, methodname and line number.
        /// </summary>
        /// <param name="logEvent"></param>
        /// <param name="propertyFactory"></param>
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory) {
            int skip = 3;
            while (true) {
                StackFrame stack = new StackFrame(skip, true);
                if (!stack.HasMethod()) {
                    logEvent.AddPropertyIfAbsent(new LogEventProperty("Caller", new ScalarValue("<unknown method>")));
                    return;
                }

                MethodBase method = stack.GetMethod();
                if (method != null && method.DeclaringType.Assembly != typeof(Log).Assembly) {
                    string caller = $"{method.DeclaringType.FullName}->{method.Name}():{stack.GetFileLineNumber()}";
                    logEvent.AddPropertyIfAbsent(new LogEventProperty("Caller", new ScalarValue(caller)));
                    return;
                }

                ++skip;
            }
        }
    };

    /// <summary>
    /// Setup the configuration with CallerEnchicher.
    /// </summary>
    public static class LoggerCallerEnrichmentConfiguration {
		/// <summary>
		/// Setup the caller.
		/// </summary>
		/// <param name="enrichmentConfiguration"></param>
		/// <returns></returns>
		public static LoggerConfiguration WithCaller(this LoggerEnrichmentConfiguration enrichmentConfiguration) => enrichmentConfiguration.With<LogEnricher>();
	};
}
