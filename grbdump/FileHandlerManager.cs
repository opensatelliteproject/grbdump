using OpenSatelliteProject;
using System.Collections.Concurrent;
using System.Threading;
using System;
using OpenSatelliteProject.GRB.Headers;

namespace grbdump {
    public class FileHandlerManager {
		const int MAX_QUEUE_LENGTH = 32768;
		readonly ConcurrentQueue<Tuple<string, object>> packets;

		bool running;
		Thread channelThread;

		public FileHandlerManager() {
			packets = new ConcurrentQueue<Tuple<string, object>>();
			running = false;
		}

		public void NewFile(Tuple<string, object> file) {
			if (running) {
				if (packets.Count >= MAX_QUEUE_LENGTH) {
					UIConsole.Warn("File Handler Queue is full!!!!");
				} else {
					packets.Enqueue(file);
				}
			}
		}

		public void Start() {
			if (!running) {
				UIConsole.Log("Starting channel thread");
				running = true;
				channelThread = new Thread(new ThreadStart(ThreadLoop)) {
					IsBackground = true,
                    Priority = ThreadPriority.Normal,
				};
				channelThread.Start();
			} else {
				UIConsole.Error("File Handler already running!");
			}
		}

		public void Stop() {
			if (running) {
				UIConsole.Log("Stopping Thread");
				running = false;
				channelThread.Join();
			} else {
				UIConsole.Error("File Handler already stopped!");
			}
		}

		void ThreadLoop() {
			UIConsole.Debug("File Handler started");
			while (running) {
                Tuple<string, object> fileToHandle;
				if (packets.TryDequeue(out fileToHandle)) {
                    string filename = fileToHandle.Item1;
                    object obj = fileToHandle.Item2;
                    if (obj.GetType() == typeof(GRBImageHeader)) {
                        HandleImage(filename, (GRBImageHeader)obj);
					} else if (obj.GetType() == typeof(GRBGenericHeader)) {
                        HandleGeneric(filename, (GRBGenericHeader)obj);
                    } else {
                        UIConsole.Error($"Invalid Type: {obj.GetType().Name}");
                    }
				}
				Thread.Yield();
			}
			UIConsole.Debug("File Handler stopped");
		}

        void HandleImage(string filename, GRBImageHeader header) {
            GRBFileHandler.HandleFile(filename, header);
        }

		void HandleGeneric(string filename, GRBGenericHeader header) {
			GRBFileHandler.HandleFile(filename, header);
        }
	}
}
