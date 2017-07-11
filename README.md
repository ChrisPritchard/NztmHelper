# NztmHelper
A small set of classes for the conversion of New Zealand Transverse Mercator coordinates to Geodesic (Latitude/Longitude), and back again. Entry point is the static class NztmHelper.

A C# conversion of C code from http://www.linz.govt.nz/system/files_force/media/file-attachments/nztm.zip?download=1 on page http://www.linz.govt.nz/data/geodetic-services/download-geodetic-software

While the code is packaged as NetStandard 1.4, the classes should be fully compatible with .NET Framework 4.6 (or earlier, if you convert some of the C# 6 statements to their older equivalents).

Also can support other 'projections' than NZTM, if you come up with the right projection parameters and change the exposed methods on the helper.