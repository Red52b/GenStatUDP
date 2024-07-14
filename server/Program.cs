using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace udp_server
{
	class Program
	{
		static SetingsStruct mySettings = null;

		static void Main(string[] args)
		{
			Console.WriteLine("Server");
			mySettings = SettingsXmlHelper.Load();
			
			if(!SettingsXmlHelper.Check(mySettings))
			{
				Console.ReadKey();
				return;
			}

			try
			{
				Thread receiveThread = new Thread(new ThreadStart(GenrateMessages));
                receiveThread.Start();
			}
            catch(Exception ex){
                Console.WriteLine(ex.Message);
            }

			Console.ReadKey();
		}

		private static void GenrateMessages()
		{
			byte[] dataArr = new byte[20];
			int dataSize;

			IPAddress remoteAddress = IPAddress.Parse(mySettings.Connection.CastGroup);
			IPEndPoint endPoint = new IPEndPoint(remoteAddress, mySettings.Connection.Port);
			UdpClient sender = new UdpClient();

			DataGenerator generator = new DataGenerator(mySettings.StatCore);
			
			while(true)
			{
				try
				{
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

	}

}
