using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace UDP_client
{
	class Program
	{
		static SetingsStruct mySettings = new SetingsStruct();
		static DataProcessor dp;
		static private Random rnd = new Random();

		static void Main(string[] args)
		{
			Console.WriteLine("Client");
			SettingsXmlHelper.Load();
			
			if(!SettingsXmlHelper.Check()){
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

		private static void RecieveMessages(){
			byte[] data;
			int tLastSec = 0;
			bool resievePackets = true;

			IPAddress remoteAddress = IPAddress.Parse(mySettings.Connection.CastGroup);
			UdpClient receiver = new UdpClient(mySettings.Connection.Port);
			IPEndPoint remoteIp = null;
			bool adressIsOK = false;
			IPEndPoint testEndPoint = new IPEndPoint(remoteAddress, mySettings.Connection.Port);
			
			byte[] adrBytes = remoteAddress.GetAddressBytes();
			if(adrBytes[0] >= 224 && adrBytes[0]<240){
				adressIsOK = true;
				receiver.JoinMulticastGroup(remoteAddress);
			}
			else{
				Console.WriteLine("Адрес 'CastGroup' не является адресом Broadcast-группы. Подключение не выполнено");
			}
			/**/
			
            try{
                while (true)
                {
					if(receiver.Available>0){
						data = receiver.Receive(ref remoteIp); // получаем данные
						resievePackets = true;
						dp.PushData(ref data);

						if(mySettings.Connection.DelayPerod>10 && rnd.Next(mySettings.Connection.DelayPerod)<1){
							Console.WriteLine("Sleep " + mySettings.Connection.DelayMiliSec);
							Thread.Sleep(mySettings.Connection.DelayMiliSec);
						}
					}
					if(DateTime.Now.Second != tLastSec && adressIsOK){
						if(!resievePackets){
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

		class DataProcessor{
			private ulong firstTimeStamp=1, lastTimeStamp=0, recievedPackets=0;
			private object packetStatisticLocker = new object();

			private StatisticCore core;

			public DataProcessor(SetingsStruct.StatisticParms inPrams){
				core = new StatisticCore(inPrams.ValuesPerPage, inPrams.ValuesMinStep);
			}
			public void PushData(ref byte[] dataArr){
				if(dataArr.Length<12){			return;			}
				ulong timeStamp = BitConverter.ToUInt64(dataArr, 0);
				double value = BitConverter.ToDouble(dataArr, 8);

				lock(packetStatisticLocker){
					if(firstTimeStamp==1){
						firstTimeStamp = timeStamp;
					}
					if(lastTimeStamp<timeStamp){
						lastTimeStamp = timeStamp;
					}
					recievedPackets++;
				}
				core.PushValue(value);
			}
			public void CalcDataParam(){
				ulong missedPackets = (lastTimeStamp-firstTimeStamp+1) - recievedPackets;
				lock(packetStatisticLocker){
					Console.WriteLine();
					Console.WriteLine("Packets recieved = " + recievedPackets + "   Packets missed = " + missedPackets);
				}
				core.CalcStatistic();
				
			}

			public void WorkThread(){
				while (true)
                {
                    core.ThreadProcess();
                }
			}

		}

		class StatisticCore{
			private int cValPerPage;
			private double cValMinStep;
			private decimal pgCoef, pgValStep;

			private object locker = new object();
			private ConcurrentQueue<double> inputBuffer = new ConcurrentQueue<double>();
			private double valMax=-1e-100, valMin=1e100;
			private ulong valuesInStore=0;
			private decimal summOfStore=0;
			private Random rnd;
			
			private SortedList<long, StatisticDatapage> pages = new SortedList<long,StatisticDatapage>();

			private void DatastoreUpdate(){
				if(valMax<valMin){		return;		}

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
				long pageIndex, valInPip;
				int cellIndex;
				while(inCountToProcess>0){
					inCountToProcess--;
					if(inputBuffer.TryDequeue(out currVal)){
						valInPip = (long)(currVal/cValMinStep);
						pageIndex = valInPip/cValPerPage;
						cellIndex = (int)(valInPip-pageIndex*cValPerPage);
						pages[pageIndex].IncreaseCell(cellIndex);
						valuesInStore++;
						summOfStore += (decimal)valInPip;	
					}
				}
			}
			
			private decimal CalcMA_Slow(){
				int i;
				decimal ma=0, pageVal;
				for(i=pages.Count-1; i>=0; i--){
					pageVal = pages.Keys[i]*pgCoef;
					ma += pageVal*pages.Values[i].Count + pages.Values[i].Summ*pgValStep;
				}
				
				ma /= valuesInStore;
				return ma;
			}
			private decimal CalcMA_Fast(){
				return summOfStore/valuesInStore*(decimal)cValMinStep;
			}
			private decimal CalcSigma_Slow(decimal inMA){
				int i,j;
				decimal sigma=0, pgValPart, currDeltaVal;
				StatisticDatapage currPage;
				for(i=pages.Count-1; i>=0; i--){
					currPage = pages.Values[i];
					pgValPart = pages.Keys[i]*pgCoef;
					for(j=0; j<cValPerPage; j++){
						currDeltaVal = (pgValPart + j*pgValStep) - inMA;		// ( X - M(X) )
						sigma += currDeltaVal*currDeltaVal * currPage.data[j];	// квадрат разности умножаем на кол-во повторений
					}
				}

				sigma = (decimal)Math.Pow((double)sigma/valuesInStore, 0.5);
				return sigma;
			}
			private decimal CalcSigma_Fast(decimal inMA){
				int i;
				decimal sigma=0, currDeltaVal;
				for(i=pages.Count-1; i>=0; i--){
					currDeltaVal = (pages.Keys[i]*pgCoef + pgValStep/2) - inMA;
					sigma += currDeltaVal*currDeltaVal * pages.Values[i].Count;
				}

				sigma = (decimal)Math.Pow((double)sigma/valuesInStore, 0.5);
				return sigma;
			}
			
			private decimal CalcMedian(){
				int i,j;
				decimal median=0;
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
				return median;
			}
			private decimal CalcModa(){
				int i;
				decimal modaNumber=0;
				uint currModaVal=0;
				for(i=pages.Count-1; i>=0; i--){
					if(pages.Values[i].ModaVal > currModaVal){
						currModaVal = pages.Values[i].ModaVal;
						modaNumber = pages.Keys[i]*pgCoef + pages.Values[i].ModaCrd*pgValStep;
					}
				}
				return modaNumber;
			}
			
			public void CalcStatistic(){
				decimal ma, sigma, median, moda;
				long prevTicks=0;
				

				lock(locker){
					//	лок нужен чтоб ThreadProcess() не добавлял новых значений в хранилище пока мы считаем параметры
					Stopwatch timer = new Stopwatch();
					timer.Start();
					
					int currQueueLen = inputBuffer.Count;
					DatastoreUpdate();
					DataPutToStore(currQueueLen);
					Console.WriteLine("dT = " + (timer.ElapsedTicks-prevTicks).ToString("000000") + "   Queue flush.");
					prevTicks = timer.ElapsedTicks;

					Console.WriteLine("valuses in store = " + valuesInStore + "     psges = " + pages.Count);
					if(valuesInStore==0){		return;		}
					
					ma = CalcMA_Fast();
					Console.WriteLine("dT = " + (timer.ElapsedTicks-prevTicks).ToString("000000") + "   MA = " + ma);
					prevTicks = timer.ElapsedTicks;
					
					if(pages.Count<100){
						sigma = CalcSigma_Slow(ma);
						Console.WriteLine("dT = " + (timer.ElapsedTicks-prevTicks).ToString("000000") + "   sigma (slow) = " + sigma);
						prevTicks = timer.ElapsedTicks;
					}
					
					sigma = CalcSigma_Fast(ma);
					Console.WriteLine("dT = " + (timer.ElapsedTicks-prevTicks).ToString("000000") + "   sigma (fast) = " + sigma);
					prevTicks = timer.ElapsedTicks;

					median = CalcMedian();
					Console.WriteLine("dT = " + (timer.ElapsedTicks-prevTicks).ToString("000000") + "   Median = " + median);
					prevTicks = timer.ElapsedTicks;

					moda = CalcModa();
					Console.WriteLine("dT = " + (timer.ElapsedTicks-prevTicks).ToString("000000") + "   Moda = " + moda);
					prevTicks = timer.ElapsedTicks;

					timer.Stop();
					Console.WriteLine("total dT = "+ timer.ElapsedTicks + "   in MiliSec = " + timer.Elapsed.TotalMilliseconds);
					Console.WriteLine("new data during calc time = "+ inputBuffer.Count);
				}
			}

			public StatisticCore(int inValPerPage, double inValMinStep){
				rnd = new Random();

				cValPerPage = inValPerPage;
				cValMinStep = inValMinStep;

				pgCoef = (decimal) (cValMinStep*cValPerPage);
				pgValStep = (decimal) cValMinStep;
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
				
				lock(locker){
					DatastoreUpdate();
					DataPutToStore(100);
				}
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
		
		
		static public class SettingsXmlHelper{
			const string fileName = "client_config.xml";
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
				if(mySettings.StatCore.ValuesMinStep <= 0){
					Console.WriteLine("Параметр 'ValuesMinStep' должен быть больше нуля");
					return false;
				}
				if(mySettings.StatCore.ValuesPerPage < 10){
					mySettings.StatCore.ValuesPerPage = 10;
				}
				return true;
			}
		}
	}

	[XmlRootAttribute("Setings")]
	public class SetingsStruct{
		public struct ConnectionParms{
			[XmlAttribute] public string CastGroup;
			[XmlAttribute] public int Port;
			[XmlAttribute] public int DelayMiliSec;
			[XmlAttribute] public int DelayPerod;
		}
		public struct StatisticParms{
			[XmlAttribute] public int ValuesPerPage;
			[XmlAttribute] public double ValuesMinStep;
		}

		public ConnectionParms Connection;
		public StatisticParms StatCore;
	}
}
