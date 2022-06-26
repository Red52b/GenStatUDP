using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace udp_server
{
	class Program
	{
		static SetingsStruct mySettings = new SetingsStruct();

		static void Main(string[] args)
		{
			Console.WriteLine("Server");
			SettingsXmlHelper.Load();
			
			if(!SettingsXmlHelper.Check()){
				Console.ReadKey();
				return;
			}

			try{
				Thread receiveThread = new Thread(new ThreadStart(GenrateMessages));
                receiveThread.Start();
			}
            catch(Exception ex){
                Console.WriteLine(ex.Message);
            }

			Console.ReadKey();
		}

		private static void GenrateMessages(){
			byte[] dataArr = new byte[20];
			int dataSize;

			IPAddress remoteAddress = IPAddress.Parse(mySettings.Connection.CastGroup);
			IPEndPoint endPoint = new IPEndPoint(remoteAddress, mySettings.Connection.Port);
			UdpClient sender = new UdpClient();

			DataGenerator generator = new DataGenerator(mySettings.StatCore);
			
			while(true){
				try{
					dataSize = generator.GetData(ref dataArr);
					sender.Send(dataArr, dataSize, endPoint); 
					Thread.Sleep(mySettings.Connection.DelayBetvenPackets);
				}
				catch(Exception ex){
					Console.WriteLine(ex.Message);
					Thread.Sleep(1000);
				}
				
			}
		}

		static public class SettingsXmlHelper{
			const string fileName = "server_config.xml";
			static public void Save(){
				XmlSerializer serializer = new XmlSerializer(typeof(SetingsStruct));
				TextWriter writer = new StreamWriter(fileName);
				mySettings.Connection.CastGroup="235.35.35.0";
				
				serializer.Serialize(writer, mySettings);
				writer.Close();
			}
			static public void Load(){
				FileStream fs = null;
				XmlSerializer serializer;

				try{
					fs = new FileStream(fileName, FileMode.Open);
					serializer = new XmlSerializer(typeof(SetingsStruct));

					mySettings = (SetingsStruct) serializer.Deserialize(fs);
				}
				catch (FileNotFoundException ex){
					Console.WriteLine("Файл настроек не найден. Проверьте наличие '"+fileName+"' в папке прогрммы");
					if(ex != null){		ex = null;		}	// затычка чтоб Warning`а не было
				}
				catch (Exception ex){
					Console.WriteLine(ex.Message);
				}
				finally{
					//	fs?.Close();	// ?. еще не добавили
					if(fs!=null){
						fs.Close();
					}
				}
				
			}
			static public bool Check(){
				if(mySettings.Connection.CastGroup==null || mySettings.Connection.CastGroup.Length==0){
					Console.WriteLine("Не удалось загрузить файл параметров");
					return false;
				}
				if(mySettings.Connection.Port==0){
					Console.WriteLine("Не указан 'Port' подключения");
					return false;
				}
				if(mySettings.StatCore.ValueDigits <= 0){
					Console.WriteLine("Параметр 'ValueDigits' должен быть больше нуля");
					return false;
				}
				if(mySettings.StatCore.RndParam < 10){
					mySettings.StatCore.RndParam = 10;
					Console.WriteLine("Параметр 'RndParam' должен быть не меньше 10. Спользуем 'RndParam' = 10");
				}
				return true;
			}
		}
	}

	class DataGenerator{
		private double stepCoef = 1e-4;
		private int digits = 4;
		private double firstValue, sinAmplitude;
		private int rndParam, sinFreq;
		private long timeStamp;	// на самом деле номер генерирумеого числа в последовательности, но имя лучше не придумал
		private Random rnd;

		public DataGenerator(SetingsStruct.StatisticParms inParm){
			rnd = new Random();
			timeStamp = 100;

			firstValue = inParm.FirstValue;
			rndParam = inParm.RndParam;
			if(inParm.ValueDigits<=10 && inParm.ValueDigits>=0){
				digits = inParm.ValueDigits;
				stepCoef = Math.Pow(10, -digits);
			}
			else{
				Console.WriteLine("'ValueDigits' должен быть в пределах [0, 10].   Используем 'ValueDigits' = "+digits);
			}
			sinFreq = inParm.SinFreq * 1000;
			sinAmplitude = inParm.SinAmplitude;
		}
		public int GetData(ref byte[] dataArr){
			int dataSize=0;
			byte[] bitData;

			double sinShift=0;
			if(sinAmplitude>0 && sinFreq>0){
				int sinPos = (int)((timeStamp-100)%sinFreq);
				sinShift = Math.Sin(Math.PI*2*sinPos/sinFreq);
			}
			
			double gaussRand = (rnd.Next(rndParam) + rnd.Next(rndParam) + rnd.Next(rndParam)) * stepCoef;
			if(rnd.Next(2)>0){		gaussRand=-gaussRand;	}
			
			double valToSend = Math.Round(Math.Abs(firstValue + sinShift + gaussRand), digits);
			timeStamp++;
			
			bitData = BitConverter.GetBytes(timeStamp);
			Array.Copy(bitData, dataArr, bitData.Length);
			dataSize += bitData.Length;

			bitData = BitConverter.GetBytes(valToSend);
			Array.Copy(bitData, 0, dataArr, dataSize, bitData.Length);
			dataSize += bitData.Length;

			return dataSize;
		}
	}

	[XmlRootAttribute("Setings")]
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
