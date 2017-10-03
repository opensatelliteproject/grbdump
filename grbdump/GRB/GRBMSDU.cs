using System;
using System.Linq;
using OpenSatelliteProject.PacketData;
using OpenSatelliteProject;
using System.IO;

namespace grbdump.GRB {
    public class GRBMSDU {
        #region Properties

        public int Version { get; set; }

        public int SHF { get; set; }

        public int APID { get; set; }

        public int Type { get; set; }

        public bool HasSecondHeader { get; set; }

        public byte[] PrimaryHeader { get; set; }
        public byte[] SecondHeader { get; set; }

        public SequenceType Sequence { get; set; }

        public int PacketNumber { get; set; }

        public int PacketLength { get; set; }

        public byte[] Data { get; set; }

        public byte[] RemainingData { get; set; }

        public bool FrameLost { get; set; }

        public bool Full { 
            get {
                return Data.Length == PacketLength + 4;
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
                    var gotCRC = PrimaryHeader.Concat(Data.Take (Data.Length - 4)).ToArray ().CRC32 ();
                    return gotCRC == CRC;
                } else {
                    // Not enough data
                    return false;
                }
            }
        }

        public bool FillPacket {
            get {
                return APID == 2047;
            }
        }

        #endregion

        #region Constructor / Destructor

        private GRBMSDU() {
        }

        #endregion

        #region Methods

        public void addDataBytes(byte[] data) {            
            /*if (data.Length + Data.Length > PacketLength + 2) {
                Console.WriteLine("Overflow in MSDU!");
            }*/
            byte[] newData = new byte[Data.Length + data.Length];
            Array.Copy(Data, newData, Data.Length);
            Array.Copy(data, 0, newData, Data.Length, data.Length);
            if (newData.Length > PacketLength + 4) {
                Data = newData.Take(PacketLength + 4).ToArray();
            } else {
                Data = newData;
            }

        }

        #endregion


        #region Builders / Parsers

        public static GRBMSDU parseMSDU(byte[] data) {
            GRBMSDU msdu = new GRBMSDU();

            msdu.PrimaryHeader = data.Take (6).ToArray ();

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

            ob = data.Skip(4).Take(2).ToArray();
            if (BitConverter.IsLittleEndian) {
                Array.Reverse(ob);
            }

            msdu.PacketLength = BitConverter.ToUInt16(ob, 0) - 3;
            data = data.Skip(6).ToArray();
            /*if (msdu.HasSecondHeader) {
                msdu.SecondHeader = data.Take (8).ToArray ();
                // data = data.Skip (8).ToArray ();
            }
            */

            if (msdu.HasSecondHeader) {
                msdu.SecondHeader = data.Take (8).ToArray ();
                var dseb = msdu.SecondHeader.Take (2).ToArray ();
                var mseb = msdu.SecondHeader.Skip (2).Take (4).ToArray ();
                var grbS = msdu.SecondHeader.Skip (6).Take (2).ToArray ();

                if (BitConverter.IsLittleEndian) {
                    Array.Reverse (dseb);
                    Array.Reverse (mseb);
                    //Array.Reverse (grbS);
                }

                var daysSinceEpoch = BitConverter.ToUInt16(dseb, 0);
                var msOfDay = BitConverter.ToUInt32 (mseb, 0);
                var dt = new DateTime (2000, 1, 1, 0, 0, 0);
                dt = dt.AddDays (daysSinceEpoch);
                dt = dt.AddMilliseconds (msOfDay);

                var grbSU = BitConverter.ToUInt16 (grbS, 0);

                var grbVersion = grbSU & 0x1F;
                var grbPayloadVariant = (grbSU & 0x3E0) >> 5;
                var grbPayloadVariant2 = ((grbS [1] & 3) << 3) + ((grbS [0] & 0xE0) >> 5);
                var assemblerIdentifier = (grbSU & 0xC00) >> 10;
                var systemEnvironment = (grbSU & 0x9000) >> 12;
                /*
                Console.WriteLine ($"GRB Version: {grbVersion}");
                Console.WriteLine ($"GRB Payload: {grbPayloadVariant} {grbPayloadVariant2}");
                Console.WriteLine ($"Assembler Identifier: {assemblerIdentifier}");
                Console.WriteLine ($"System Environment: {systemEnvironment}");
                */
            }
            if (data.Length > msdu.PacketLength + 4) {
                msdu.RemainingData = data.Skip(msdu.PacketLength+ 4).ToArray();
                data = data.Take(msdu.PacketLength+ 4).ToArray();
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

