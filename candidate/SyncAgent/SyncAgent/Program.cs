using System;
using System.ServiceProcess;
using System.Threading;
using SyncAgent.Configuration;
using SyncAgent.Database;
using SyncAgent.Http;
using SyncAgent.Tasks;

namespace SyncAgent
{
    /// <summary>Entry point: composes dependencies and runs as a service or console app.</summary>
    public static class Program
    {
        public static void Main()
        {
            var settings = AppSettings.Load();
            var client = SyncPlatformClient.Create(settings);

            // No handlers registered yet — the dispatcher fails soft on unknown task types.
            var dispatcher = new TaskDispatcher(new ITaskHandler[0]);
            var loop = new SyncLoop(client, dispatcher, settings);

            // DB seam wired here for handlers added in a later change.
            IDbConnectionFactory dbFactory = new DbConnectionFactory(settings);
            _ = dbFactory;

            if (Environment.UserInteractive)
            {
                Console.WriteLine("Running in console mode. Press Enter to stop.");
                var thread = new Thread(loop.Run);
                thread.Start();
                Console.ReadLine();
                loop.Stop();
                thread.Join();
            }
            else
            {
                ServiceBase.Run(new SyncAgentService(loop));
            }
        }
    }
}
