using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace UDP_client
{
	class Program
	{
		static SetingsStruct mySettings;
		static DataProcessor dp;
		static private Random rnd = new Random();

		static void Main(string[] args)
		{
			Console.WriteLine("Client");
			mySettings = SettingsXmlHelper.Load();
			
			if(!SettingsXmlHelper.Check(mySettings))
			{
				Console.ReadKey();
				return;
			}

			dp = new DataProcessor(mySettings.StatCore);

			try
            {
				Thread receiveThread = new Thread(new ThreadStart(RecieveMessages));
                receiveThread.Start();

				Thread calcThread = new Thread(new ThreadStart(dp.WorkThread));
                calcThread.Start();


				while (true)
                {
                    Console.ReadKey();
					dp.CalcDataParam();
                }
			}
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

			Console.ReadKey();
		}

		private static void RecieveMessages()
		{
			byte[] data;
			int tLastSec = 0;
			bool resievePackets = true;

			IPAddress remoteAddress = IPAddress.Parse(mySettings.Connection.CastGroup);
			UdpClient receiver = new UdpClient(mySettings.Connection.Port);
			IPEndPoint remoteIp = null;
			bool addressIsOK = false;
			IPEndPoint testEndPoint = new IPEndPoint(remoteAddress, mySettings.Connection.Port);
			
			byte[] adrBytes = remoteAddress.GetAddressBytes();
			if(adrBytes[0] >= 224 && adrBytes[0]<240)
			{
				addressIsOK = true;
				receiver.JoinMulticastGroup(remoteAddress);
			}
			else
			{
				Console.WriteLine("Адрес 'CastGroup' не является адресом Broadcast-группы. Подключение не выполнено");
			}
			
            try
            {
                while (true)
                {
					if(receiver.Available>0)
					{
						data = receiver.Receive(ref remoteIp); // получаем данные
						resievePackets = true;
						dp.PushData(ref data);

						if(mySettings.Connection.DelayPerod>10 && rnd.Next(mySettings.Connection.DelayPerod)<1)
						{
							Console.WriteLine("Sleep " + mySettings.Connection.DelayMiliSec);
							Thread.Sleep(mySettings.Connection.DelayMiliSec);
						}
					}
					if(DateTime.Now.Second != tLastSec && addressIsOK)
					{
						if(!resievePackets)
						{
							Console.WriteLine("Connection reinit");
							receiver.Close();
							receiver = new UdpClient(mySettings.Connection.Port);
							receiver.JoinMulticastGroup(remoteAddress);
						}
						tLastSec = DateTime.Now.Second;
						data = new byte[2];
						receiver.Send(data, 2, testEndPoint);
						resievePackets = false;
					}
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                receiver.Close();
            }
		}
		
	}

}
