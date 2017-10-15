using System;
using OpenSatelliteProject.GRB.Enum;

namespace OpenSatelliteProject.GRB.Product {
    public class Product {
        public int APID { get; private set; }
        public string Name { get; private set; }
        public Instrument Instrument { get; private set; }
        public string FolderName { get; private set; }
        public bool IsMetadata { get; private set; }
        public object[] Meta { get; private set; }

        public Product (int apid, string name, Instrument instrument, string folder, bool isMetadata, object[] meta = null) {
            this.APID = apid;
            this.Name = name;
            this.Instrument = instrument;
            this.FolderName = folder;
            this.IsMetadata = isMetadata;
            this.Meta = meta;
        }

        public string ToProductString() {
            return $"{Instrument.ToString()}-{FolderName}";
        }

        override
        public string ToString() {
            return $"{Name} - {(IsMetadata ? "Metadata" : "Data")}";
        }
    }
}

