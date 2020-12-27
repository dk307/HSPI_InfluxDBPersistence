using System.Diagnostics;

namespace Hspi
{
    /// <summary>
    /// Class for the main program.
    /// </summary>
    public static class Program
    {       
        private static ConsoleTraceListener consoleTracer = new ConsoleTraceListener();

        /// <summary>
        /// Defines the entry point of the application.
        /// </summary>
        /// <param name="args">Command line arguments</param>
        private static void Main(string[] args)
        {
            Trace.Listeners.Add(consoleTracer);
            Trace.WriteLine("Starting...");

            try
            {
                using (var plugin = new HSPI_InfluxDBPersistence.HSPI())
                {
                    plugin.Connect(args);
                }
            }
            finally
            {
                Trace.WriteLine("Bye!!!");
            }
        }
    }
}