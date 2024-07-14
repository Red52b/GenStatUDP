using System.Xml.Serialization;

namespace udp_server
{
    [XmlRoot("Setings")]
    public class SetingsStruct{
        public struct ConnectionParms{
            [XmlAttribute] public string CastGroup;
            [XmlAttribute] public int Port;
            [XmlAttribute] public int DelayBetvenPackets;
        }
        public struct StatisticParms{
            [XmlAttribute] public double FirstValue;
            [XmlAttribute] public int ValueDigits;
            [XmlAttribute] public int RndParam;
            [XmlAttribute] public int SinFreq;
            [XmlAttribute] public double SinAmplitude;
        }

        public ConnectionParms Connection;
        public StatisticParms StatCore;
    }
}