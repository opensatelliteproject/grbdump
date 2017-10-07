using System;
using System.Linq;

namespace OpenSatelliteProject.GRB.Headers {
    public class GRBGenericHeader {

        public byte compressionAlgorithm;
        public uint secondsSinceEpoch;
        public uint microsecondsFromSeconds;
        public byte[] reserved;
        public uint grbPayloadCount;
        public DateTime timestamp;
        public ulong epoch;
        public string filename;
        public int apid;

        public GRBGenericHeader (int apid, byte [] headerData) {
            this.apid = apid;
            compressionAlgorithm = headerData [0];

            byte[] secondsBuffer = headerData.Skip (1).Take (4).ToArray ();
            byte[] microsseconds = headerData.Skip (5).Take (4).ToArray ();
            reserved = headerData.Skip (9).Take (8).ToArray ();
            byte[] count = headerData.Skip (17).Take (4).ToArray ();

            if (BitConverter.IsLittleEndian) {
                Array.Reverse (secondsBuffer);
                Array.Reverse (microsseconds);
                Array.Reverse (count);
            }

            secondsSinceEpoch = BitConverter.ToUInt32 (secondsBuffer, 0);
            microsecondsFromSeconds = BitConverter.ToUInt32 (microsseconds, 0);
            grbPayloadCount = BitConverter.ToUInt32 (count, 0);

            timestamp = new DateTime(2000, 1, 1, 0, 0, 0);
            timestamp = timestamp.AddSeconds(secondsSinceEpoch);
            timestamp = timestamp.AddMilliseconds(microsecondsFromSeconds / 1000f);

            epoch = (ulong) (secondsSinceEpoch * 1e6 + microsecondsFromSeconds);

            filename = $"{epoch}-{grbPayloadCount:D8}.grb";
        }
    }
}

