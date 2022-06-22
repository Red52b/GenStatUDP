using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace UDP_client
{
	class Program
	{
		static IPAddress remoteAddress;

		static void Main(string[] args)
		{
			remoteAddress = IPAddress.Parse("192.168.0.102");
			
			try
            {
				Thread receiveThread = new Thread(new ThreadStart(RecieveMessages));
                receiveThread.Start();
				Console.WriteLine("Client");
			}
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
		}

		private static void RecieveMessages(){
			
			DataProcessor dp = new DataProcessor();
			
			UdpClient receiver = new UdpClient(9531);
			IPEndPoint remoteIp = null;
            string localAddress = "192.168.0.102";

            try
            {
                while (true)
                {
                    byte[] data = receiver.Receive(ref remoteIp); // получаем данные
					dp.PrintData(ref data);
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

		class DataProcessor{
			public void PrintData(ref byte[] dataArr){
				ulong timeStamp = BitConverter.ToUInt64(dataArr, 0);
				double value = BitConverter.ToDouble(dataArr, 8);

				string message = timeStamp.ToString("0000")+"   ---   value = "+value.ToString();
                Console.WriteLine(message);
			}
		}
	}
}
