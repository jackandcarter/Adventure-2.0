using System.Threading;
using System.Threading.Tasks;
using Adventure.Server.Simulation;
using Microsoft.Extensions.Hosting;

namespace Adventure.Server.Host
{
    public class SimulationLoopHostedService : IHostedService
    {
        private readonly SimulationLoop loop;

        public SimulationLoopHostedService(SimulationLoop loop)
        {
            this.loop = loop;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return loop.StartAsync(cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return loop.StopAsync();
        }
    }
}
