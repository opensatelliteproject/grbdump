using System.Collections.Generic;
using OpenSatelliteProject.GRB.Enum;
using System.IO;

namespace OpenSatelliteProject.GRB.Product {
    public static class Products {

        static readonly Dictionary<int, Product> productsByAPID;

        public static Product GetProductByAPID(int APID) {
            return (productsByAPID.ContainsKey (APID)) ? productsByAPID [APID] : null;
        }

        public static string GetProductStringByAPID(int APID) {
            var p = GetProductByAPID(APID);
            if (p == null) {
                return $"UNK-{APID:X3}";
            }

            return p.ToProductString();
        }

        public static string GetFolderByAPID(int APID) {
            var p = GetProductByAPID (APID);
            if (p == null) {
                return $"Unknown {APID:X3}";
            }

            return p.FolderName == null ? p.Instrument.ToString () : Path.Combine (p.Instrument.ToString (), p.FolderName);
        }

        public static string GetNameByAPID(int APID) {
            var p = GetProductByAPID (APID);
            return p == null ? $"Unknown Product {APID:X3}" : p.Name;
        }

        public static bool IsMetadata(int APID) {
            var p = GetProductByAPID(APID);
            if (p == null) {
                return false;
            }

            return p.IsMetadata;
        }

        static Products() {
            productsByAPID = new Dictionary<int, Product>();
            var allProducts = new List<Product> ();
            #region ABI
            int abiAPID = 0x100;
            for (int z = 0; z < ABIMeta.ABINames.Length; z++) {
                string name = ABIMeta.ABINames[z];
                string folderPrefix = ABIMeta.FoldersPrefix[z];
                for (int i = 0; i < 16; i++) {
                    var abimeta = ABIMeta.ChannelMetadata[i+1];
                    var imsize = ABIMeta.GetExpectedImageSize(ABIMeta.Regions[z], abimeta.Resolution);
                    allProducts.Add(
                        new Product(
                            abiAPID, 
                            $"{name} Band {i+1:D2}", 
                            Instrument.ABI, 
                            Path.Combine(folderPrefix, $"BAND{i+1:D2}"), 
                            name.IndexOf("Metadata") > -1,
                            new object[] { abimeta, imsize }
                        )
                    );
                    abiAPID++;
                }
            }
			#region Reserved for Ground Station
			for (int i = 0x220; i < 0x298; i++) {
				allProducts.Add(
					new Product(
						i,
						$"ABI Reserved {i:X3}",
						Instrument.ABI,
						$"Reserved {i:X3}",
						true,
						new object[] { null, ImageSize.New(10, 10) }
					)
				);
			}
			#endregion
			#region Unmapped
			for (int i = 0x1D1; i < 0x220; i++) {
				allProducts.Add(
					new Product(
						i,
						$"ABI Unmapped {i:X3}",
						Instrument.ABI,
						$"Unmapped {i:X3}",
						true,
						new object[] { null, ImageSize.New(10, 10) }
					)
				);
			}
			for (int i = 0x298; i < 0x300; i++) {
				allProducts.Add(
					new Product(
						i,
						$"ABI Unmapped {i:X3}",
						Instrument.ABI,
						$"Unmapped {i:X3}",
						true,
						new object[] { null, ImageSize.New(10, 10) }
					)
				);
			}
            #endregion
            #endregion
            #region GLM
            allProducts.Add(new Product(0x300, "GLM Lightning Detection Metadata", Instrument.GLM, null, true));
            allProducts.Add(new Product(0x301, "GLM Flash Data", Instrument.GLM, null, false));
            allProducts.Add(new Product(0x302, "GLM Group Data", Instrument.GLM, null, false));
            allProducts.Add(new Product(0x303, "GLM Event Data", Instrument.GLM, null, false));
            #endregion
            #region EXIS
            allProducts.Add(new Product(0x380, "EXIS Solar Flux: EUV Metadata", Instrument.EXIS, "EUV", true));
            allProducts.Add(new Product(0x381, "EXIS Solar Flux: EUV Data", Instrument.EXIS, "EUV", false));
            allProducts.Add(new Product(0x382, "EXIS Solar Flux: X-Ray Metadata", Instrument.EXIS, "X-Ray", true));
            allProducts.Add(new Product(0x383, "EXIS Solar Flux: X-Ray Data", Instrument.EXIS, "X-Ray", false));
            #endregion
            #region SEISS
            allProducts.Add(new Product(0x400, "SEISS Energetic Heavy Ions Metadata", Instrument.SEISS, "Energetic Heavy Ions", true));
            allProducts.Add(new Product(0x401, "SEISS Energetic Heavy Ions Data", Instrument.SEISS, "Energetic Heavy Ions", false));
            allProducts.Add(new Product(0x410, "SEISS Magnetospheric Electrons and Protons: Low Energy Metadata", Instrument.SEISS, "Magnetospheric Electrons and Protons", true));
            allProducts.Add(new Product(0x411, "SEISS Magnetospheric Electrons and Protons: Low Energy Data", Instrument.SEISS, "Magnetospheric Electrons and Protons", false));
            allProducts.Add(new Product(0x420, "SEISS Magnetospheric Electrons and Protons: Medium and High Energy Metadata", Instrument.SEISS, "Magnetospheric Electrons and Protons", true));
            allProducts.Add(new Product(0x421, "SEISS Magnetospheric Electrons and Protons: Medium and High Energy Data", Instrument.SEISS, "Magnetospheric Electrons and Protons", false));
            allProducts.Add(new Product(0x430, "SEISS Solar and Galactic Protons Metadata", Instrument.SEISS, "Solar and Galactic Protons", true));
            allProducts.Add(new Product(0x431, "SEISS Solar and Galactic Protons Data", Instrument.SEISS, "Solar and Galactic Protons", false));
            #endregion
            #region SUVI
            allProducts.Add(new Product(0x480, "SUVI Solar Imagery: X-Ray Metadata Band Fe094", Instrument.SUVI, "Fe094", true));
            allProducts.Add(new Product(0x481, "SUVI Solar Imagery: X-Ray Metadata Band Fe132", Instrument.SUVI, "Fe132", true));
            allProducts.Add(new Product(0x482, "SUVI Solar Imagery: X-Ray Metadata Band Fe171", Instrument.SUVI, "Fe171", true));
            allProducts.Add(new Product(0x483, "SUVI Solar Imagery: X-Ray Metadata Band Fe195", Instrument.SUVI, "Fe195", true));
            allProducts.Add(new Product(0x484, "SUVI Solar Imagery: X-Ray Metadata Band Fe284", Instrument.SUVI, "Fe284", true));
            allProducts.Add(new Product(0x485, "SUVI Solar Imagery: X-Ray Metadata Band He304", Instrument.SUVI, "He304", true));
            allProducts.Add(new Product(0x486, "SUVI Solar Imagery: X-Ray Data Band Fe094", Instrument.SUVI, "Fe094", false, new object[] { null, new ImageSize(1280, 1280) }));
            allProducts.Add(new Product(0x487, "SUVI Solar Imagery: X-Ray Data Band Fe132", Instrument.SUVI, "Fe132", false, new object[] { null, new ImageSize(1280, 1280) }));
            allProducts.Add(new Product(0x488, "SUVI Solar Imagery: X-Ray Data Band Fe171", Instrument.SUVI, "Fe171", false, new object[] { null, new ImageSize(1280, 1280) }));
            allProducts.Add(new Product(0x489, "SUVI Solar Imagery: X-Ray Data Band Fe195", Instrument.SUVI, "Fe195", false, new object[] { null, new ImageSize(1280, 1280) }));
            allProducts.Add(new Product(0x48a, "SUVI Solar Imagery: X-Ray Data Band Fe284", Instrument.SUVI, "Fe284", false, new object[] { null, new ImageSize(1280, 1280) }));
            allProducts.Add(new Product(0x48b, "SUVI Solar Imagery: X-Ray Data Band He304", Instrument.SUVI, "He304", false, new object[] { null, new ImageSize(1280, 1280) }));
            #endregion
            #region MAG
            allProducts.Add(new Product(0x500, "MAG Metadata", Instrument.MAG, null, true));
            allProducts.Add(new Product(0x501, "MAG Product Data", Instrument.MAG, null, false));
            #endregion
            #region INFO
            allProducts.Add(new Product(0x580, "GRB Information", Instrument.INFO, null, false));
            #endregion
            allProducts.ForEach (p => productsByAPID.Add (p.APID, p));
        }
    }
}

