using System.Collections.Concurrent;
using System.Threading;
using OpenSatelliteProject;

namespace grbdump {
    class ChannelManager {
        const int MAX_QUEUE_LENGTH = 0x2FFFF; // Memory usage will be this * 2048
        readonly ConcurrentQueue<byte[]> packets;

        bool running;
        Thread channelThread;
        readonly Demuxer demuxer;

        public ChannelManager(MSDUManager msduManager) {
            packets = new ConcurrentQueue<byte[]>();
			running = false;
            demuxer = new Demuxer(msduManager);
        }

        public void NewPacket(byte[] packet) {
            if (running) {
                if (packets.Count >= MAX_QUEUE_LENGTH) {
                    byte[] drop;
                    UIConsole.Warn("Channel Manager Queue is full!!!! Samples might be dropped.");
                    packets.TryDequeue(out drop);
                }
                packets.Enqueue(packet);
            }
        }

        public void Start() {
			if (!running) {
				UIConsole.Log("Starting channel thread");
                running = true;
                channelThread = new Thread(new ThreadStart(ThreadLoop)) {
                    IsBackground = true,
                    Priority = ThreadPriority.AboveNormal,
                };
                channelThread.Start();
            } else {
                UIConsole.Error("Channel Manager already running!");
            }
        }

        public void Stop() {
            if (running) {
                UIConsole.Log("Stopping Thread");
                running = false;
                channelThread.Join();
            } else {
                UIConsole.Error("Channel Manager already stopped!");
            }
        }

        void ThreadLoop() {
            UIConsole.Debug("Channel Thread started");
            while (running) {
                byte[] packet;
                int c = 0;
                while (c < 8 && packets.TryDequeue(out packet)) {
                    demuxer.ParseBytes(packet);
                    c++;
                }
                Thread.Sleep(1);
                // Thread.yield(); // That might be better. Not sure
			}
			UIConsole.Debug("Channel Thread stopped");
        }
    }
}
