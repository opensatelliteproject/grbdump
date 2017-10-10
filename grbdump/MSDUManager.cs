using System;
using System.Collections.Concurrent;
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
    public class MSDUManager {
		const int MAX_QUEUE_LENGTH = 1024;
		readonly ConcurrentQueue<OpenSatelliteProject.GRB.MSDU> packets;

		bool running;
		Thread channelThread;
        FileHandlerManager fileHandleManager;

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
                    Priority = ThreadPriority.AboveNormal,
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
				Thread.Yield();
			}
			UIConsole.Debug("Channel Thread stopped");
		}

        #region MSDU Processing Variables
        uint Packets = 0;
        uint CRCFails = 0;
        int startNum = -1;
        int endNum = -1;
        string filename;
		OpenSatelliteProject.GRB.MSDU lastMSDU;
		GRBGenericHeader genericFileHeader;
		GRBImageHeader imageFileHeader;
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

				if (msdu.Sequence == SequenceType.FIRST_SEGMENT || msdu.Sequence == SequenceType.SINGLE_DATA) {
					if (startNum != -1) {
						// UIConsole.Warn("Received First Segment but last data wasn't finished! Forcing dump.");
						// This can only happen for multi-segment file.
						filename = Path.Combine(FileHandler.TemporaryFileFolder, lastMSDU.TemporaryFilename);
						string target = Path.Combine(FileHandler.TemporaryFileFolder, $"{lastMSDU.APID}-{LLTools.TimestampMS()}-{Tools.RandomString(8)}");
						File.Move(filename, target);
						if (EnumHelpers.APID2Type(msdu.APID) == PayloadType.Generic) {
							fileHandleManager.NewFile(new Tuple<string, object>(target, genericFileHeader));
						} else {
                            fileHandleManager.NewFile(new Tuple<string, object>(target, imageFileHeader));
						}
						startNum = -1;
						endNum = -1;
					}

					if (EnumHelpers.APID2Type(msdu.APID) == PayloadType.Generic) {
						genericFileHeader = new GRBGenericHeader(msdu.APID, msdu.Data.Skip(8).Take(21).ToArray());
					} else {
						imageFileHeader = new GRBImageHeader(msdu.APID, msdu.Data.Skip(8).Take(34).ToArray());
					}

					if (msdu.Sequence == SequenceType.FIRST_SEGMENT) {
						startNum = msdu.PacketNumber;
					}
				} else if (msdu.Sequence == SequenceType.LAST_SEGMENT) {
					endNum = msdu.PacketNumber;

					if (startNum == -1) {
						// Orphan Packet
						endNum = -1;
						return;
					}
				} else if (msdu.Sequence != SequenceType.SINGLE_DATA && startNum == -1) {
					// Orphan Packet
					return;
				}

				string path = FileHandler.TemporaryFileFolder;
				if (!Directory.Exists(path)) {
					Directory.CreateDirectory(path);
				}

				filename = Path.Combine(FileHandler.TemporaryFileFolder, msdu.TemporaryFilename);

				int totalOffset;

				if (firstOrSinglePacket) {
					totalOffset = 8;
					if (EnumHelpers.APID2Type(msdu.APID) == PayloadType.Generic) {
						totalOffset += 21;
					} else {
						totalOffset += 34;
					}
				} else {
					totalOffset = 8;
				}

				byte[] dataToSave = msdu.Data.Skip(totalOffset).ToArray();
				dataToSave = dataToSave.Take(dataToSave.Length - 4).ToArray(); // Remove CRC
				lastMSDU = msdu;

				using (FileStream fs = new FileStream(filename, firstOrSinglePacket ? FileMode.Create : FileMode.Append, FileAccess.Write)) {
					using (BinaryWriter sw = new BinaryWriter(fs)) {
						sw.Write(dataToSave);
						sw.Flush();
					}
				}

				if (msdu.Sequence == SequenceType.LAST_SEGMENT || msdu.Sequence == SequenceType.SINGLE_DATA) {
                    string target = Path.Combine(FileHandler.TemporaryFileFolder, $"{lastMSDU.APID}-{LLTools.TimestampMS()}-{Tools.RandomString(8)}");
					File.Move(filename, target);
					if (EnumHelpers.APID2Type(msdu.APID) == PayloadType.Generic) {
						fileHandleManager.NewFile(new Tuple<string, object>(target, genericFileHeader));
					} else {
						fileHandleManager.NewFile(new Tuple<string, object>(target, imageFileHeader));
					}
					startNum = -1;
					endNum = -1;
				}
			} catch (Exception e) {
				UIConsole.Error(String.Format("Exception on FinishMSDU: {0}", e));
			}
        }
    }
}
