using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace TelegramRAT
{
    [Serializable, XmlRoot(ElementName = "query")]
    public class NetworkInfo
    {
		[XmlElement(ElementName = "status")]
		public string Status { get; set; }

		[XmlElement(ElementName = "country")]
		public string Country { get; set; }

		[XmlElement(ElementName = "countryCode")]
		public string CountryCode { get; set; }

		[XmlElement(ElementName = "region")]
		public string Region { get; set; }

		[XmlElement(ElementName = "regionName")]
		public string RegionName { get; set; }

		[XmlElement(ElementName = "city")]
		public string City { get; set; }

		[XmlElement(ElementName = "zip")]
		public string Zip { get; set; }

		[XmlElement(ElementName = "lat")]
		public string Lat { get; set; }

		[XmlElement(ElementName = "lon")]
		public string Lon { get; set; }

		[XmlElement(ElementName = "timezone")]
		public string Timezone { get; set; }

		[XmlElement(ElementName = "isp")]
		public string Isp { get; set; }

		[XmlElement(ElementName = "org")]
		public string Org { get; set; }

		[XmlElement(ElementName = "as")]
		public string As { get; set; }

		[XmlElement(ElementName = "query")]
		public string Query { get; set; }
	}
}
