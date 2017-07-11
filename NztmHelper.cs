using static System.Math;

namespace NztmHelper
{
    /// <summary>
    /// Conversion of C code from http://www.linz.govt.nz/system/files_force/media/file-attachments/nztm.zip?download=1
    /// On page http://www.linz.govt.nz/data/geodetic-services/download-geodetic-software
    /// </summary>
    public static class NztmHelper
    {
        public static GeodesicCoords ConvertNztm(double easting, double northing)
        {
            var projection = NZTMProjection();
            double latitudeRad, longitudeRad;
            TransverseMercatorToLatLong(projection, easting, northing, out latitudeRad, out longitudeRad);

            return new GeodesicCoords { Latitude = latitudeRad * rad2deg, Longitude = longitudeRad * rad2deg };
        }

        public static GeodesicCoords ConvertNztm(TransverseMercatorCoords coords)
            => ConvertNztm(coords.Easting, coords.Northing);

        public static TransverseMercatorCoords ConvertLatLong(double latitudeDeg, double longitudeDeg)
        {
            var projection = NZTMProjection();
            double easting, northing;
            LatLongToTransverseMercator(projection, latitudeDeg * deg2rad, longitudeDeg * deg2rad, out easting, out northing);

            return new TransverseMercatorCoords { Easting = easting, Northing = northing };
        }

        public static TransverseMercatorCoords ConvertLatLong(GeodesicCoords coords)
            => ConvertLatLong(coords.Latitude, coords.Longitude);

        #region C Conversion code

        class TMProjection
        {
            public double CentralMeridian { get; set; }
            public double ScaleFactor { get; set; }
            public double OriginLatitude { get; set; }
            public double FalseEasting { get; set; }
            public double FalseNorthing { get; set; }
            public double UnitToMetreConversion { get; set; }

            public EllipsoidParams EllipsoidParams { get; set; }

            public double IntermediateCalculation { get; set; }

            public TMProjection(double a, double rf, double centralMeridian, double scaleFactor, double originLatitude, double falseEasting, double falseNorthing, double unitToMetreConversion)
            {
                CentralMeridian = centralMeridian;
                ScaleFactor = scaleFactor;
                OriginLatitude = originLatitude;
                FalseEasting = falseEasting;
                FalseNorthing = falseNorthing;
                UnitToMetreConversion = unitToMetreConversion;

                EllipsoidParams = new EllipsoidParams(a, rf);
                IntermediateCalculation = MeridianArc(this, originLatitude);
            }
        }

        class EllipsoidParams
        {
            public double A { get; set; }
            public double RF { get; set; }
            public double F { get; set; }
            public double E2 { get; set; }
            public double EP2 { get; set; }

            public EllipsoidParams(double a, double rf)
            {
                if (rf != 0.0)
                    F = 1.0 / rf;
                A = a;
                RF = rf;
                E2 = (2 * F) - (F * F);
                EP2 = E2 / (1 - E2);
            }
        }

        /// <summary>
        /// Returns the length of meridional arc (Helmert formula)
        /// Method based on Redfearn's formulation as expressed in GDA technical
        /// manual at http://www.anzlic.org.au/icsm/gdatm/index.html
        /// </summary>
        private static double MeridianArc(TMProjection projection, double latitudeRad)
        {
            var e2 = projection.EllipsoidParams.E2;
            var a = projection.EllipsoidParams.A;
            var lt = latitudeRad;

            double e4;
            double e6;
            double A0;
            double A2;
            double A4;
            double A6;

            e4 = e2 * e2;
            e6 = e4 * e2;

            A0 = 1 - (e2 / 4.0) - (3.0 * e4 / 64.0) - (5.0 * e6 / 256.0);
            A2 = (3.0 / 8.0) * (e2 + e4 / 4.0 + 15.0 * e6 / 128.0);
            A4 = (15.0 / 256.0) * (e4 + 3.0 * e6 / 4.0);
            A6 = 35.0 * e6 / 3072.0;

            return a * (A0 * lt - A2 * Sin(2 * lt) + A4 * Sin(4 * lt) - A6 * Sin(6 * lt));
        }

        /// <summary>
        /// Calculates the foot point latitude from the meridional arc
        /// Method based on Redfearn's formulation as expressed in GDA technical
        /// manual at http://www.anzlic.org.au/icsm/gdatm/index.html
        /// </summary>
        private static double FootPointLatitude(TMProjection projection, double meridianArc)
        {
            var f = projection.EllipsoidParams.F;
            var a = projection.EllipsoidParams.A;

            double n;
            double n2;
            double n3;
            double n4;
            double g;
            double sig;
            double phio;

            n = f / (2.0 - f);
            n2 = n * n;
            n3 = n2 * n;
            n4 = n2 * n2;

            g = a * (1.0 - n) * (1.0 - n2) * (1 + 9.0 * n2 / 4.0 + 225.0 * n4 / 64.0);
            sig = meridianArc / g;

            phio = sig + (3.0 * n / 2.0 - 27.0 * n3 / 32.0) * Sin(2.0 * sig)
                            + (21.0 * n2 / 16.0 - 55.0 * n4 / 32.0) * Sin(4.0 * sig)
                            + (151.0 * n3 / 96.0) * Sin(6.0 * sig)
                            + (1097.0 * n4 / 512.0) * Sin(8.0 * sig);

            return phio;
        }

        /// <summary>
        /// Routine to convert from Tranverse Mercator to latitude and longitude.
        /// Method based on Redfearn's formulation as expressed in GDA technical
        /// manual at http://www.anzlic.org.au/icsm/gdatm/index.html
        /// </summary>
        private static void TransverseMercatorToLatLong(TMProjection projection, double easting, double northing, out double latitudeRad, out double longitudeRad)
        {
            var fn = projection.FalseNorthing;
            var fe = projection.FalseEasting;
            var sf = projection.ScaleFactor;
            var e2 = projection.EllipsoidParams.E2;
            var a = projection.EllipsoidParams.A;
            var cm = projection.CentralMeridian;
            var om = projection.IntermediateCalculation;
            var utom = projection.UnitToMetreConversion;

            double cn1;
            double fphi;
            double slt;
            double clt;
            double eslt;
            double eta;
            double rho;
            double psi;
            double E;
            double x;
            double x2;
            double t;
            double t2;
            double t4;
            double trm1;
            double trm2;
            double trm3;
            double trm4;

            cn1 = (northing - fn) * utom / sf + om;
            fphi = FootPointLatitude(projection, cn1);
            slt = Sin(fphi);
            clt = Cos(fphi);

            eslt = (1.0 - e2 * slt * slt);
            eta = a / Sqrt(eslt);
            rho = eta * (1.0 - e2) / eslt;
            psi = eta / rho;

            E = (easting - fe) * utom;
            x = E / (eta * sf);
            x2 = x * x;


            t = slt / clt;
            t2 = t * t;
            t4 = t2 * t2;

            trm1 = 1.0 / 2.0;

            trm2 = ((-4.0 * psi
                         + 9.0 * (1 - t2)) * psi
                         + 12.0 * t2) / 24.0;

            trm3 = ((((8.0 * (11.0 - 24.0 * t2) * psi
                          - 12.0 * (21.0 - 71.0 * t2)) * psi
                          + 15.0 * ((15.0 * t2 - 98.0) * t2 + 15)) * psi
                          + 180.0 * ((-3.0 * t2 + 5.0) * t2)) * psi + 360.0 * t4) / 720.0;

            trm4 = (((1575.0 * t2 + 4095.0) * t2 + 3633.0) * t2 + 1385.0) / 40320.0;

            // out
            latitudeRad = fphi + (t * x * E / (sf * rho)) * (((trm4 * x2 - trm3) * x2 + trm2) * x2 - trm1);

            trm1 = 1.0;

            trm2 = (psi + 2.0 * t2) / 6.0;

            trm3 = (((-4.0 * (1.0 - 6.0 * t2) * psi
                       + (9.0 - 68.0 * t2)) * psi
                       + 72.0 * t2) * psi
                       + 24.0 * t4) / 120.0;

            trm4 = (((720.0 * t2 + 1320.0) * t2 + 662.0) * t2 + 61.0) / 5040.0;

            // out
            longitudeRad = cm - (x / clt) * (((trm4 * x2 - trm3) * x2 + trm2) * x2 - trm1);
        }

        /// <summary>
        /// Routine to convert from latitude and longitude to Transverse Mercator.
        /// Method based on Redfearn's formulation as expressed in GDA technical
        /// manual at http://www.anzlic.org.au/icsm/gdatm/index.html
        /// Loosely based on FORTRAN source code by J.Hannah and A.Broadhurst.
        /// </summary>
        private static void LatLongToTransverseMercator(TMProjection projection, double latitudeRad, double longitudeRad, out double easting, out double northing)
        {
            var fn = projection.FalseNorthing;
            var fe = projection.FalseEasting;
            var sf = projection.ScaleFactor;
            var e2 = projection.EllipsoidParams.E2;
            var a = projection.EllipsoidParams.A;
            var cm = projection.CentralMeridian;
            var om = projection.IntermediateCalculation;
            var utom = projection.UnitToMetreConversion;

            double dlon;
            double m;
            double slt;
            double eslt;
            double eta;
            double rho;
            double psi;
            double clt;
            double w;
            double wc;
            double wc2;
            double t;
            double t2;
            double t4;
            double t6;
            double trm1;
            double trm2;
            double trm3;
            double gce;
            double trm4;
            double gcn;

            dlon = longitudeRad - cm;
            while (dlon > PI) dlon -= (PI * 2);
            while (dlon < -PI) dlon += (PI * 2);

            m = MeridianArc(projection, latitudeRad);

            slt = Sin(latitudeRad);

            eslt = (1.0 - e2 * slt * slt);
            eta = a / Sqrt(eslt);
            rho = eta * (1.0 - e2) / eslt;
            psi = eta / rho;

            clt = Cos(latitudeRad);
            w = dlon;

            wc = clt * w;
            wc2 = wc * wc;

            t = slt / clt;
            t2 = t * t;
            t4 = t2 * t2;
            t6 = t2 * t4;

            trm1 = (psi - t2) / 6.0;

            trm2 = (((4.0 * (1.0 - 6.0 * t2) * psi
                          + (1.0 + 8.0 * t2)) * psi
                          - 2.0 * t2) * psi + t4) / 120.0;

            trm3 = (61 - 479.0 * t2 + 179.0 * t4 - t6) / 5040.0;

            gce = (sf * eta * dlon * clt) * (((trm3 * wc2 + trm2) * wc2 + trm1) * wc2 + 1.0);
            // out
            easting = gce / utom + fe;

            trm1 = 1.0 / 2.0;

            trm2 = ((4.0 * psi + 1) * psi - t2) / 24.0;

            trm3 = ((((8.0 * (11.0 - 24.0 * t2) * psi
                        - 28.0 * (1.0 - 6.0 * t2)) * psi
                        + (1.0 - 32.0 * t2)) * psi
                        - 2.0 * t2) * psi
                        + t4) / 720.0;

            trm4 = (1385.0 - 3111.0 * t2 + 543.0 * t4 - t6) / 40320.0;

            gcn = (eta * t) * ((((trm4 * wc2 + trm3) * wc2 + trm2) * wc2 + trm1) * wc2);
            // out
            northing = (gcn + m - om) * sf / utom + fn;
        }

        private static double rad2deg = 180 / PI;
        private static double deg2rad = PI / 180;

        private static double NZTM_A = 6378137;
        private static double NZTM_RF = 298.257222101;

        private static double NZTM_CM = 173.0;
        private static double NZTM_OLAT = 0.0;
        private static double NZTM_SF = 0.9996;
        private static double NZTM_FE = 1600000.0;
        private static double NZTM_FN = 10000000.0;

        private static TMProjection NZTMProjection()
            => new TMProjection(NZTM_A, NZTM_RF, NZTM_CM / rad2deg, NZTM_SF, NZTM_OLAT / rad2deg, NZTM_FE, NZTM_FN, 1.0);

        #endregion
    }
}
