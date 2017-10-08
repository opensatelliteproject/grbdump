using System;
using System.Collections.Generic;

namespace OpenSatelliteProject.GRB.Product {
    public enum ABIRegion {
        FullDisk,
        CONUS,
        Mesoscale,
        Other,
    }

    public class ABIMeta {
        public int Band { get; private set; }
        public float WaveLength { get; private set; }
        public float Resolution { get; private set; }
        public int BitDepth { get; private set; }

        public ABIMeta (int band, float wavelength, float resolution, int bitdepth) {
            Band = band;
            WaveLength = wavelength;
            Resolution = resolution;
            BitDepth = bitdepth;
        }

        public static Dictionary<int, ABIMeta> ChannelMetadata = new Dictionary<int, ABIMeta> {
            {1, new ABIMeta(1, 0.47f, 1f, 10)},
            {2, new ABIMeta(2, 0.64f, 0.5f, 12)},
            {3, new ABIMeta(3, 0.865f, 1f, 10)},
            {4, new ABIMeta(4, 1.378f, 2f, 11)},
            {5, new ABIMeta(5, 1.61f, 2f, 10)},
            {6, new ABIMeta(6, 2.25f, 2f, 10)},
            {7, new ABIMeta(7, 3.9f, 2f, 14)},
            {8, new ABIMeta(8, 6.185f, 2f, 12)},
            {9, new ABIMeta(9, 6.95f, 2f, 11)},
            {10, new ABIMeta(10, 7.34f, 2f, 12)},
            {11, new ABIMeta(11, 8.5f, 2f, 12)},
            {12, new ABIMeta(12, 9.61f, 2f, 11)},
            {13, new ABIMeta(13, 10.35f, 2f, 12)},
            {14, new ABIMeta(14, 11.2f, 2f, 12)},
            {15, new ABIMeta(15, 12.3f, 2f, 12)},
            {16, new ABIMeta(16, 13.3f, 2f, 10)},
        };

        public static string[] ABINames = {
            "ABI Full Disk Metadata (Mode 3)",
            "ABI Full Disk Radiance Image Data (Mode 3)",
            "ABI Continental United States Metadata (Mode 3)",
            "ABI Continental United States Radiance Image (Mode 3)",
            "ABI Mesoscale #1 Metadata (Mode 3)",
            "ABI Mesoscale #1 Radiance Image (Mode 3)",
            "ABI Mesoscale #2 Metadata (Mode 3)",
            "ABI Mesoscale #2 Radiance Image (Mode 3)",
            "ABI Full Disk Metadata (Mode 4)",
            "ABI Full Disk Radiance Image Data (Mode 4)",

            "ABI CONUS Metadata (Extracted from Full Disk Mode 4)",
            "ABI Non-Standard ABI Image Metadata",
            "ABI Non-Standard ABI Radiance Image Data",
        };

        public static ABIRegion[] Regions = {
            ABIRegion.FullDisk,
            ABIRegion.FullDisk,
            ABIRegion.CONUS,
            ABIRegion.CONUS,
            ABIRegion.Mesoscale,
            ABIRegion.Mesoscale,
            ABIRegion.Mesoscale,
            ABIRegion.Mesoscale,
            ABIRegion.FullDisk,
            ABIRegion.FullDisk,

            ABIRegion.CONUS,
            ABIRegion.Other,
            ABIRegion.Other,
        };

        public static string[] FoldersPrefix = {
            "Full Disk",
            "Full Disk",
            "CONUS",
            "CONUS",
            "Mesoscale1",
            "Mesoscale1",
            "Mesoscale2",
            "Mesoscale2",
            "Full Disk",
            "Full Disk",
            "CONUS",

            "Other",
            "Other",
        };

        static readonly Dictionary<string, ImageSize> region2size = new Dictionary<string, ImageSize> {
            { GenRegionResString(ABIRegion.FullDisk, 0.5f), ImageSize.New(21696, 21696) },
            { GenRegionResString(ABIRegion.FullDisk, 1.0f), ImageSize.New(10848, 10848) },
            { GenRegionResString(ABIRegion.FullDisk, 2.0f), ImageSize.New(5424, 5424) },
            { GenRegionResString(ABIRegion.CONUS, 0.5f), ImageSize.New(10000, 6000) },
            { GenRegionResString(ABIRegion.CONUS, 1.0f), ImageSize.New(5000, 3000) },
            { GenRegionResString(ABIRegion.CONUS, 2.0f), ImageSize.New(2500, 1500) },
            { GenRegionResString(ABIRegion.Mesoscale, 0.5f), ImageSize.New(2000, 2000) },
            { GenRegionResString(ABIRegion.Mesoscale, 1.0f), ImageSize.New(1000, 1000) },
            { GenRegionResString(ABIRegion.Mesoscale, 2.0f), ImageSize.New(500, 500) },
        };

        public static ImageSize GetExpectedImageSize(ABIRegion region, float scale) {
            if (region == ABIRegion.Other) {
                return ImageSize.New(100, 100);
            }
            string regionResString = GenRegionResString(region, scale);
            return (region2size.ContainsKey(regionResString)) ? region2size[regionResString] : ImageSize.New(100, 100);
        }

        static string GenRegionResString(ABIRegion r, float res) {
            return $"{r.ToString()}-{res.ToString()}";
        }
    }
}

