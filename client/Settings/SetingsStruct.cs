using System.Xml.Serialization;

namespace UDP_client
{
    [XmlRoot("Setings")]
    public class SetingsStruct
    {
        public struct ConnectionParms
        {
            [XmlAttribute] public string CastGroup;
            [XmlAttribute] public int Port;
            [XmlAttribute] public int DelayMiliSec;
            [XmlAttribute] public int DelayPerod;
        }

        public struct StatisticParms
        {
            [XmlAttribute] public int ValueDigits;

            [XmlAttribute] public int ValuesPerPage;
            //	[XmlAttribute] public double ValuesMinStep;
        }

        public ConnectionParms Connection;
        public StatisticParms StatCore;
    }
}