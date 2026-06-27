using System;
using System.ServiceProcess;
using System.Threading;
using SyncAgent.Configuration;
using SyncAgent.Database;
using SyncAgent.Http;
using SyncAgent.Tasks;
using SyncAgent.Tasks.Handlers;

namespace SyncAgent
{
    /// <summary>Entry point: composes dependencies and runs as a service or console app.</summary>
    public static class Program
    {
        public static void Main()
        {
            var settings = AppSettings.Load();
            var client = SyncPlatformClient.Create(settings);

            IDbConnectionFactory dbFactory = new DbConnectionFactory(settings);

            // Register a handler per task type. Unknown types still fail soft.
            var dispatcher = new TaskDispatcher(new ITaskHandler[]
            {
                new GetCustomersHandler(dbFactory),
                new GetProductsHandler(dbFactory),
                new GetOrdersHandler(dbFactory),
                new GetProductInventoryHandler(dbFactory)
            });
            var loop = new SyncLoop(client, dispatcher, settings);

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
