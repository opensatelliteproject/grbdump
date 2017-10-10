using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using OpenSatelliteProject;
using OpenSatelliteProject.GRB;
using OpenSatelliteProject.GRB.Enum;
using OpenSatelliteProject.GRB.Headers;
using OpenSatelliteProject.PacketData;
using OpenSatelliteProject.Tools;

namespace grbdump {
    class MSDUManager {
		const int MAX_QUEUE_LENGTH = 0xFFFF;
		readonly ConcurrentQueue<OpenSatelliteProject.GRB.MSDU> packets;

		bool running;
		Thread channelThread;
        readonly FileHandlerManager fileHandleManager;

        public MSDUManager(FileHandlerManager fileHandleManager) {
			packets = new ConcurrentQueue<OpenSatelliteProject.GRB.MSDU>();
			running = false;
            this.fileHandleManager = fileHandleManager;
		}

		public void FinishMSDU(OpenSatelliteProject.GRB.MSDU msdu) {
			if (running) {
				if (packets.Count >= MAX_QUEUE_LENGTH) {
					UIConsole.Warn("MSDU Manager Queue is full!!!!");
				} else {
					packets.Enqueue(msdu);
				}
			}
		}

		public void Start() {
			if (!running) {
				UIConsole.Log("Starting MSDU thread");
				running = true;
				channelThread = new Thread(new ThreadStart(ThreadLoop)) {
					IsBackground = true,
                    Priority = ThreadPriority.Highest,
				};
				channelThread.Start();
			} else {
				UIConsole.Error("MSDU Manager already running!");
			}
		}

		public void Stop() {
			if (running) {
				UIConsole.Log("Stopping Thread");
				running = false;
				channelThread.Join();
			} else {
				UIConsole.Error("MSDU Manager already stopped!");
			}
		}

		void ThreadLoop() {
			UIConsole.Debug("MSDU Thread started");
			while (running) {
				OpenSatelliteProject.GRB.MSDU msdu;
				if (packets.TryDequeue(out msdu)) {
                    ProcessMSDU(msdu);
				}

                List<int> keys = msduCache.Keys.ToList();
                keys.ForEach(k => {
                    var minfo = msduCache[k];
                    if (minfo.Expired) {
                        UIConsole.Warn($"Product {k:X3} expired. Dumping...");
						string msduFile = Path.Combine(FileHandler.TemporaryFileFolder, minfo.FileName);
						string target = Path.Combine(FileHandler.TemporaryFileFolder, $"{k:X3}-{LLTools.TimestampMS()}-{Tools.RandomString(8)}");
						File.Move(msduFile, target);
						if (EnumHelpers.APID2Type(k) == PayloadType.Generic) {
							fileHandleManager.NewFile(new Tuple<string, object>(target, minfo.GenericHeader));
						} else {
							fileHandleManager.NewFile(new Tuple<string, object>(target, minfo.ImageHeader));
						}
						msduCache.Remove(k);
                    }
                });
				
                Thread.Yield();
			}
			UIConsole.Debug("Channel Thread stopped");
		}

        #region MSDU Processing Variables
        uint Packets = 0;
        uint CRCFails = 0;
        readonly Dictionary<int, MSDUInfo> msduCache = new Dictionary<int, MSDUInfo>();
        #endregion

        void ProcessMSDU(OpenSatelliteProject.GRB.MSDU msdu) {
			try {
				if (msdu.APID == 2047) {
					// Skip fill packet
					return;
				}

				bool firstOrSinglePacket = msdu.Sequence == SequenceType.FIRST_SEGMENT || msdu.Sequence == SequenceType.SINGLE_DATA;

				Packets++;

				if (!msdu.Valid) {
					CRCFails++;
				}

				if (!msdu.Valid || !msdu.Full) {
					if (msdu.FrameLost) {
						UIConsole.Error($"Lost some frames on MSDU, the file will be corrupted. CRC Match: {msdu.Valid} - Size Match: {msdu.Full}");
					} else {
						UIConsole.Error($"Corrupted MSDU. CRC Match: {msdu.Valid} - Size Match: {msdu.Full}");
					}
				}

                var payloadType = EnumHelpers.APID2Type(msdu.APID);

                if (msdu.Sequence == SequenceType.FIRST_SEGMENT || msdu.Sequence == SequenceType.SINGLE_DATA) {
                    if (msduCache.ContainsKey(msdu.APID)) {
                        var minfo = msduCache[msdu.APID];
                        UIConsole.Warn($"Received First Segment for {msdu.APID:X3} but last data wasn't saved to disk yet! Forcing dump.");
						// This can only happen for multi-segment file.
                        string msduFile = Path.Combine(FileHandler.TemporaryFileFolder, minfo.FileName);
						string target = Path.Combine(FileHandler.TemporaryFileFolder, $"{msdu.APID:X3}-{LLTools.TimestampMS()}-{Tools.RandomString(8)}");
						File.Move(msduFile, target);
                        if (payloadType == PayloadType.Generic) {
							fileHandleManager.NewFile(new Tuple<string, object>(target, minfo.GenericHeader));
						} else {
                            fileHandleManager.NewFile(new Tuple<string, object>(target, minfo.ImageHeader));
						}
                        msduCache.Remove(msdu.APID);
					}

                    var msInfo = new MSDUInfo() {
                        APID = msdu.APID,
						FileName = msdu.TemporaryFilename,
						GenericHeader = payloadType == PayloadType.Generic ? new GRBGenericHeader(msdu.APID, msdu.Data.Skip(8).Take(21).ToArray()) : null,
                        ImageHeader = payloadType == PayloadType.ImageData ? new GRBImageHeader(msdu.APID, msdu.Data.Skip(8).Take(34).ToArray()) : null,
                    };

                    msduCache.Add(msdu.APID, msInfo);
                } else if (msdu.Sequence == SequenceType.LAST_SEGMENT || msdu.Sequence == SequenceType.CONTINUED_SEGMENT) {
                    if (!msduCache.ContainsKey(msdu.APID)) {
                        UIConsole.Warn("Orphan Packet!");
                        return;
                    }
				}

                var msduInfo = msduCache[msdu.APID];
                msduInfo.Refresh();

                string path = FileHandler.TemporaryFileFolder;
				if (!Directory.Exists(path)) {
					Directory.CreateDirectory(path);
				}

                string filename = Path.Combine(FileHandler.TemporaryFileFolder, msduInfo.FileName);

				int totalOffset;

				if (firstOrSinglePacket) {
					totalOffset = 8;
                    if (payloadType == PayloadType.Generic) {
						totalOffset += 21;
					} else {
						totalOffset += 34;
					}
				} else {
					totalOffset = 8;
				}

				byte[] dataToSave = msdu.Data.Skip(totalOffset).ToArray();
				dataToSave = dataToSave.Take(dataToSave.Length - 4).ToArray(); // Remove CRC

				using (FileStream fs = new FileStream(filename, firstOrSinglePacket ? FileMode.Create : FileMode.Append, FileAccess.Write)) {
					using (BinaryWriter sw = new BinaryWriter(fs)) {
						sw.Write(dataToSave);
						sw.Flush();
					}
				}

				if (msdu.Sequence == SequenceType.LAST_SEGMENT || msdu.Sequence == SequenceType.SINGLE_DATA) {
                    string target = Path.Combine(FileHandler.TemporaryFileFolder, $"{msdu.APID:X3}-{LLTools.TimestampMS()}-{Tools.RandomString(8)}");
					File.Move(filename, target);
                    if (payloadType == PayloadType.Generic) {
                        fileHandleManager.NewFile(new Tuple<string, object>(target, msduInfo.GenericHeader));
					} else {
                        fileHandleManager.NewFile(new Tuple<string, object>(target, msduInfo.ImageHeader));
					}
                    msduCache.Remove(msdu.APID);
				}
			} catch (Exception e) {
				UIConsole.Error(String.Format("Exception on FinishMSDU: {0}", e));
			}
        }
    }
}
