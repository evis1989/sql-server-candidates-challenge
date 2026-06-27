using System.ServiceProcess;
using System.Threading;

namespace SyncAgent
{
    /// <summary>Windows Service host for the sync loop.</summary>
    public class SyncAgentService : ServiceBase
    {
        private readonly SyncLoop _loop;
        private Thread _worker;

        public SyncAgentService(SyncLoop loop)
        {
            ServiceName = "KabilioSyncAgent";
            _loop = loop;
        }

        protected override void OnStart(string[] args)
        {
            // Run the loop off the SCM thread so OnStart returns promptly.
            _worker = new Thread(_loop.Run) { IsBackground = true, Name = "SyncLoop" };
            _worker.Start();
        }

        protected override void OnStop()
        {
            _loop.Stop();
            _worker?.Join();
        }
    }
}
