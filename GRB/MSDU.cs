using System;
using System.Linq;
using OpenSatelliteProject.PacketData;

namespace OpenSatelliteProject.GRB {
    public class MSDU {
        #region Properties

        public int Version { get; set; }

        public int SHF { get; set; }

        public int APID { get; set; }

        public int Type { get; set; }

        public bool HasSecondHeader { get; set; }

        public byte[] PrimaryHeader { get; set; }

        public SequenceType Sequence { get; set; }

        public int PacketNumber { get; set; }

        public int PacketLength { get; set; }

        public byte[] Data { get; set; }

        public byte[] RemainingData { get; set; }

        public bool FrameLost { get; set; }

        public string TemporaryFilename { get; set; }

        public bool Full { 
            get {
                return Data.Length == PacketLength;
            } 
        }

        public UInt32 CRC {
            get {
                byte[] o = Data.Skip(Data.Length - 4).ToArray();
                if (BitConverter.IsLittleEndian) {
                    Array.Reverse(o);
                }
                return BitConverter.ToUInt32(o, 0);
            }
        }

        public bool Valid {
            get {
                if (Data.Length > 4) {
                    var gotCRC = PrimaryHeader.Concat(Data.Take(Data.Length - 4)).ToArray().CRC32();
                    return gotCRC == CRC;
                }                     
                // Not enough data
                return false;
            }
        }

        public bool FillPacket {
            get {
                return APID == 2047;
            }
        }

        #endregion

        #region Constructor / Destructor

        MSDU() {}

        #endregion

        #region Methods

        public void AddDataBytes(byte[] data) {            
            byte[] newData = new byte[Data.Length + data.Length];
            Array.Copy(Data, newData, Data.Length);
            Array.Copy(data, 0, newData, Data.Length, data.Length);
            if (newData.Length > PacketLength) {
                Data = newData.Take(PacketLength).ToArray();
            } else {
                Data = newData;
            }
        }

        #endregion


        #region Builders / Parsers

        public static MSDU ParseMSDU(byte[] data) {
            MSDU msdu = new MSDU {
                PrimaryHeader = data.Take(6).ToArray()
            };

            byte[] ob = data.Take(2).ToArray();
            if (BitConverter.IsLittleEndian) {
                Array.Reverse(ob);
            }

            UInt16 o = BitConverter.ToUInt16(ob, 0);

            msdu.Version = (o & 0xE000) >> 13;
            msdu.Type = (o & 0x1000) >> 12;
            msdu.HasSecondHeader = ((o & 0x800) >> 11) > 0;
            msdu.APID = o & 0x7FF;

            ob = data.Skip(2).Take(2).ToArray();
            if (BitConverter.IsLittleEndian) {
                Array.Reverse(ob);
            }

            o = BitConverter.ToUInt16(ob, 0);

            msdu.Sequence = (SequenceType)((o & 0xC000) >> 14);
			msdu.PacketNumber = (o & 0x3FFF);

			msdu.TemporaryFilename = $"{msdu.APID}.grbtmp";

            ob = data.Skip(4).Take(2).ToArray();
            if (BitConverter.IsLittleEndian) {
                Array.Reverse(ob);
            }

            msdu.PacketLength = BitConverter.ToUInt16(ob, 0) + 1;
            data = data.Skip(6).ToArray();
            if (data.Length > msdu.PacketLength) {
                msdu.RemainingData = data.Skip(msdu.PacketLength).ToArray();
                data = data.Take(msdu.PacketLength).ToArray();
            } else {
                msdu.RemainingData = new byte[0];
            }

            msdu.Data = data.ToArray();

            msdu.FrameLost = false;

            return msdu;
        }

        #endregion
    }
}

