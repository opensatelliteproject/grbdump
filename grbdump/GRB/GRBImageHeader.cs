using System;
using System.Linq;
using OpenSatelliteProject;
using OpenSatelliteProject.Tools;

namespace grbdump.GRB {
    public class GRBImageHeader {
        public byte compressionAlgorithm;
        public uint secondsSinceEpoch;
        public uint microsecondsFromSeconds;
        public byte[] reserved;
        public uint sequence;
        public DateTime timestamp;
        public string filename;
        public int apid;
        public ulong epoch;

        public uint rowOffset;
        public uint rowOffsetB;
        public uint ulX;
        public uint ulY;
        public uint height;
        public uint width;
        public uint dqfOffset;

        public GRBImageHeader (int apid, byte[] headerData) {
            this.apid = apid;
            compressionAlgorithm = headerData [0];

            byte[] secondsBuffer = headerData.Skip (1).Take (4).ToArray ();
            byte[] microsseconds = headerData.Skip (5).Take (4).ToArray ();
            byte[] count = headerData.Skip (9).Take (2).ToArray ();
            byte[] rOff = headerData.Skip (11).Take (3).ToArray ();
            byte[] ulXB = headerData.Skip (14).Take (4).ToArray ();
            byte[] ulYB = headerData.Skip (18).Take (4).ToArray ();
            byte[] heightB = headerData.Skip (22).Take (4).ToArray ();
            byte[] widthB = headerData.Skip (26).Take (4).ToArray ();
            byte[] dqOff = headerData.Skip (30).Take (4).ToArray ();

            if (BitConverter.IsLittleEndian) {
                Array.Reverse (secondsBuffer);
                Array.Reverse (microsseconds);
                Array.Reverse (count);
                Array.Reverse (rOff);
                Array.Reverse (ulXB);
                Array.Reverse (ulYB);
                Array.Reverse (heightB);
                Array.Reverse (widthB);
                Array.Reverse (dqOff);
            }

            secondsSinceEpoch = BitConverter.ToUInt32 (secondsBuffer, 0);
            microsecondsFromSeconds = BitConverter.ToUInt32 (microsseconds, 0);
            sequence = BitConverter.ToUInt16 (count, 0);

            epoch = (ulong)(secondsSinceEpoch * 1e6 + microsecondsFromSeconds);

            rowOffset = (uint) (rOff [2] << 16 + rOff [1] << 8 + rOff [0]) & 0xFFFFFFFF;

            ulX = BitConverter.ToUInt32 (ulXB, 0);
            ulY = BitConverter.ToUInt32 (ulYB, 0);
            height = BitConverter.ToUInt32 (heightB, 0);
            width = BitConverter.ToUInt32 (widthB, 0);
            dqfOffset = BitConverter.ToUInt32 (dqOff, 0);

            timestamp = new DateTime(2000, 1, 1, 0, 0, 0);
            timestamp = timestamp.AddSeconds(secondsSinceEpoch);
            timestamp = timestamp.AddMilliseconds(microsecondsFromSeconds / 1000f);

            filename = compressionAlgorithm == 1 ? $"{epoch}-{sequence:D8}.j2k" : $"{epoch}-{sequence:D8}.img";
            UIConsole.Log ($"File: {filename} - DateTime: {timestamp.ToString()} - Compression: {compressionAlgorithm}");
        }

        public override string ToString() {
            string o = "";
            o += $"DateTime: {timestamp.ToString()}\n";
            o += $"Sequence: {sequence}\n";
            o += $"Epoch: {epoch}\n";
            o += $"APID: {apid}\n";
            o += $"rowOffset: {rowOffset}\n";
            o += $"upperLeftX: {ulX}\n";
            o += $"upperLeftY: {ulY}\n";
            o += $"width: {width}\n";
            o += $"height: {height}\n";
            o += $"dqfOffset: {dqfOffset}\n";
            return o;
        }
    }
}

