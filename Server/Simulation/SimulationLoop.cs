using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Adventure.Server.Simulation
{
    public class SimulationLoop
    {
        private readonly ConcurrentDictionary<string, SimulationRoom> rooms = new();
        private readonly TimeSpan tickInterval;
        private CancellationTokenSource? loopCts;
        private Task? loopTask;

        public SimulationLoop(int tickRateHz = 20)
        {
            if (tickRateHz < 1 || tickRateHz > 60)
            {
                throw new ArgumentOutOfRangeException(nameof(tickRateHz), "Tick rate must be between 1 and 60 Hz.");
            }

            tickInterval = TimeSpan.FromSeconds(1.0 / tickRateHz);
        }

        public void RegisterRoom(SimulationRoom room)
        {
            rooms[room.RoomId] = room;
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            loopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            loopTask = Task.Run(() => RunAsync(loopCts.Token), loopCts.Token);
            return loopTask;
        }

        public async Task StopAsync()
        {
            if (loopCts == null || loopTask == null)
            {
                return;
            }

            loopCts.Cancel();
            try
            {
                await loopTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            var lastTick = stopwatch.Elapsed;

            while (!cancellationToken.IsCancellationRequested)
            {
                var now = stopwatch.Elapsed;
                var delta = now - lastTick;
                lastTick = now;

                Tick(delta);

                var sleepFor = tickInterval - delta;
                if (sleepFor > TimeSpan.Zero)
                {
                    try
                    {
                        await Task.Delay(sleepFor, cancellationToken).ConfigureAwait(false);
                    }
                    catch (TaskCanceledException)
                    {
                    }
                }
            }
        }

        private void Tick(TimeSpan delta)
        {
            var now = DateTime.UtcNow;
            foreach (var room in rooms.Values)
            {
                room.Tick(delta, now);
            }
        }
    }
}
