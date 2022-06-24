using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace UDP_client
{
	class Program
	{
		static IPAddress remoteAddress;
		static DataProcessor dp = new DataProcessor();

		static void Main(string[] args)
		{
			remoteAddress = IPAddress.Parse("192.168.0.102");
			
			try
            {
				Thread receiveThread = new Thread(new ThreadStart(RecieveMessages));
                receiveThread.Start();

				Thread calcThread = new Thread(new ThreadStart(dp.WorkThread));
                calcThread.Start();

				Console.WriteLine("Client");

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
		}

		private static void RecieveMessages(){
			
			
			UdpClient receiver = new UdpClient(9531);
			IPEndPoint remoteIp = null;
            string localAddress = "192.168.0.102";

            try
            {
                while (true)
                {
                    byte[] data = receiver.Receive(ref remoteIp); // получаем данные
					dp.PushData(ref data);
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
			private long missedPacketCount=0;
			private ulong lastTimeStamp=0;

			private StaisticCore core = new StaisticCore();

			public void PrintData(ref byte[] dataArr){
				ulong timeStamp = BitConverter.ToUInt64(dataArr, 0);
				double value = BitConverter.ToDouble(dataArr, 8);

				string message = timeStamp.ToString("0000")+"   ---   value = "+value.ToString();
                Console.WriteLine(message);

			}
			public void PushData(ref byte[] dataArr){
				ulong timeStamp = BitConverter.ToUInt64(dataArr, 0);
				double value = BitConverter.ToDouble(dataArr, 8);

				core.PushValue(value);
			}
			public void CalcDataParam(){
				core.CalcStatParam();
			}

			public void WorkThread(){
				while (true)
                {
                    core.ThreadProcess();
                }
			}

		}

		class StaisticCore{
			private const int cValPerPage = 100;
			private const double cValMinStep = 1e-4;

			private ConcurrentQueue<double> inputBuffer = new ConcurrentQueue<double>();
			private double valMax=-1e100, valMin=1e100;
			private ulong valuesInStore=0;
			
			private SortedList<long, StatisticDatapage> pages = new SortedList<long,StatisticDatapage>();

			private void DatastoreUpdate(){
				long minPage, maxPage, currPage;
				StatisticDatapage currSDP;
				minPage = (int)(valMin/cValMinStep)/cValPerPage;
				maxPage = (int)(valMax/cValMinStep)/cValPerPage;

				if(pages.Count==0){
					for(currPage=minPage; currPage<=maxPage; currPage++){
						currSDP = new StatisticDatapage(cValPerPage);
						pages.Add(currPage,currSDP);
					}
				}
				else{
					for(currPage = pages.Keys[0]-1; currPage>=minPage; currPage--){
						currSDP = new StatisticDatapage(cValPerPage);
						pages.Add(currPage, currSDP);
					}
					for(currPage = pages.Keys[pages.Count-1]+1; currPage<=maxPage; currPage++){
						currSDP = new StatisticDatapage(cValPerPage);
						pages.Add(currPage, currSDP);
					}
				}
			}
			private void DataPutToStore(int inCountToProcess){
				double currVal;
				long pageIndex;
				int cellIndex;
				while(inCountToProcess>0){
					inCountToProcess--;
					if(inputBuffer.TryDequeue(out currVal)){
						pageIndex = (long)(currVal/cValMinStep)/cValPerPage;
						cellIndex = (int)((long)(currVal/cValMinStep)-pageIndex*cValPerPage);
						pages[pageIndex].IncreaseCell(cellIndex);
						valuesInStore++;
					}
				}
			}
			public void CalcStatParam(){
				Stopwatch timer = new Stopwatch();
				timer.Start();
				int i,j;
				decimal ma=0, median=0, pageVal, pgCoef, pgValStep;
				pgCoef = (decimal) (cValMinStep*cValPerPage);
				pgValStep = (decimal) cValMinStep;

				for(i=pages.Count-1; i>=0; i--){
					pageVal = pages.Keys[i]*pgCoef;
					ma += pageVal*pages.Values[i].Count + pages.Values[i].Summ*pgValStep;
				}
				ma /= valuesInStore;

				ulong halfCount = valuesInStore/2;
				ulong currCount=0;
				for(i=pages.Count-1; i>=0; i--){
					currCount += pages.Values[i].Count;
					if(currCount>=halfCount){		break;		}
				}
				if(i>0){
					StatisticDatapage interestinPage = pages.Values[i];
					for(j=0; j<cValPerPage; j++){
						currCount -= interestinPage.data[j];
						if(currCount<=halfCount){		break;		}
					}
					median = pages.Keys[i]*pgCoef + interestinPage.data[j]*pgValStep;
				}

				decimal modaNumber=-1;
				uint currModaVal=0;
				for(i=pages.Count-1; i>=0; i--){
					if(pages.Values[i].ModaVal > currModaVal){
						currModaVal = pages.Values[i].ModaVal;
						modaNumber = pages.Keys[i]*pgCoef + pages.Values[i].ModaCrd*pgValStep;
					}
				}



				timer.Stop();
				TimeSpan ts = timer.Elapsed;
				Console.WriteLine();
				Console.WriteLine("dT = "+ ts.TotalMilliseconds);
				Console.WriteLine("    median = " + median + "   moda = " + modaNumber +"   MA = " + ma );
			}

			public StaisticCore(){
				
			}
			public void PushValue(double inValue){
				if(inValue>valMax){
					valMax = inValue;
				}
				if(inValue<valMin){
					valMin = inValue;
				}

				inputBuffer.Enqueue(inValue);
			}

			public void ThreadProcess(){
				if(inputBuffer.Count<100){		return;		}

				DatastoreUpdate();
				DataPutToStore(100);
			}

		}

		class StatisticDatapage{
			public uint [] data;
			private int modaCrd;
			private uint modaVal;
			private ulong valuesCount;
			private decimal valuesSumm;

			public StatisticDatapage(int inSize){
				data = new uint[inSize];
				modaCrd = -1;
				modaVal = 0;
				valuesCount = 0;
				valuesSumm = 0;
			}

			public void IncreaseCell(int inIndex){
				data[inIndex]++;
				valuesCount++;
				valuesSumm += inIndex;

				if(data[inIndex]>modaVal){
					modaVal = data[inIndex];
					modaCrd = inIndex;
				}
			}
			
			public int ModaCrd{get{ return modaCrd; }}
			public uint ModaVal{get{ return modaVal; }}
			public ulong Count{get{ return valuesCount; }}
			public decimal Summ{get{ return valuesSumm; }}
		}
	}
}
