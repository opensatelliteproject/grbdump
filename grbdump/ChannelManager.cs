using System.Collections.Concurrent;
using System.Threading;
using OpenSatelliteProject;

namespace grbdump {
    class ChannelManager {
        const int MAX_QUEUE_LENGTH = 32768;
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
                    UIConsole.Warn("Channel Manager Queue is full!!!!");
                } else {
                    packets.Enqueue(packet);
                }
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
                if (packets.TryDequeue(out packet)) {
                    demuxer.ParseBytes(packet);
                }
                Thread.Yield();
			}
			UIConsole.Debug("Channel Thread stopped");
        }
    }
}
