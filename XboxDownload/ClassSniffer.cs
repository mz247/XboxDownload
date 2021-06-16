using System;
using System.Collections.Generic;

namespace XboxDownload
{
    class Market
    {
        public String name;
        public String code;
        public String lang;

        public Market(String name, String code, String lang)
        {
            this.name = name;
            this.code = code;
            this.lang = lang;
        }

        public override string ToString()
        {
            return this.name;
        }
    }

    class ClassSniffer
    {
        public class Sniffer
        {
            public Product Product { get; set; }
        }

        public class Product
        {
            public List<LocalizedProperties> LocalizedProperties { get; set; }
            public List<DisplaySkuAvailabilities> DisplaySkuAvailabilities { get; set; }
        }

        public class LocalizedProperties
        {
            public List<Images> Images { get; set; }
            public string ProductDescription { get; set; }
            public string ProductTitle { get; set; }
        }

        public class Images
        {
            public int Height { get; set; }
            public int Width { get; set; }
            public string Uri { get; set; }
        }

        public class DisplaySkuAvailabilities
        {
            public Sku Sku { get; set; }
            public List<Availabilities> Availabilities { get; set; }
        }

        public class Sku
        {
            public Properties Properties { get; set; }
            public string SkuType { get; set; }
        }

        public class Properties
        {
            public List<Packages> Packages { get; set; }
            public List<BundledSkus> BundledSkus { get; set; }
        }

        public class Packages
        {
            public ulong MaxDownloadSizeInBytes { get; set; }
            public List<PlatformDependencies> PlatformDependencies { get; set; }
            public List<PackageDownloadUris> PackageDownloadUris { get; set; }
        }

        public class BundledSkus
        {
            public string BigId { get; set; }
        }

        public class PlatformDependencies
        {
            public string PlatformName { get; set; }
        }

        public class PackageDownloadUris
        {
            public string Uri { get; set; }
        }


        public class Availabilities
        {
           public OrderManagementData OrderManagementData { get; set; }
        }

        public class OrderManagementData
        {
            public Price Price { get; set; }
        }

        public class Price
        {
            public string CurrencyCode { get; set; }
            public double MSRP { get; set; }
            public double ListPrice { get; set; }
            public double WholesalePrice { get; set; }
        }
    }
}
