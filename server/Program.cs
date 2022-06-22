using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace udp_server
{
	class Program
	{
		static IPAddress remoteAddress;

		static void Main(string[] args)
		{
			remoteAddress = IPAddress.Parse("192.168.0.102");
			try
            {
				Thread receiveThread = new Thread(new ThreadStart(GenrateMessages));
                receiveThread.Start();
				Console.WriteLine("Server");
			}
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
		}

		private static void GenrateMessages(){
			byte[] dataArr = new byte[20];
			int dataSize;
			DataGenerator generator = new DataGenerator();
			
			UdpClient sender = new UdpClient();
			IPEndPoint endPoint = new IPEndPoint(remoteAddress, 9531);
			
			while(true){
				dataSize = generator.GetData(ref dataArr);
				sender.Send(dataArr, dataSize, endPoint); 
				Thread.Sleep(1000);
			}
		}
	}

	class DataGenerator{
		private const double stepCoef = 1e-4;
		private double value;
		private ulong timeStamp;
		private Random rnd;

		public DataGenerator(){
			rnd = new Random();
			value = 6.5;
			timeStamp = 0;
		}
		public int GetData(ref byte[] dataArr){
			int dataSize=0;
			byte[] bitData;

			value += (rnd.Next(2000)-1000) * stepCoef;
			timeStamp++;
			
			bitData = BitConverter.GetBytes(timeStamp);
			Array.Copy(bitData, dataArr, bitData.Length);
			dataSize += bitData.Length;

			bitData = BitConverter.GetBytes(value);
			Array.Copy(bitData, 0, dataArr, dataSize, bitData.Length);
			dataSize += bitData.Length;

			return dataSize;
		}
	}
}
