using System;

namespace UDP_client
{
		public class DataProcessor
		{
			private ulong firstTimeStamp=1, lastTimeStamp=0, recievedPackets=0;
			private object packetStatisticLocker = new object();

			private StatisticCore core;

			public DataProcessor(SetingsStruct.StatisticParms inPrams)
			{
				core = new StatisticCore(inPrams.ValuesPerPage, inPrams.ValueDigits);
			}
			
			public void PushData(ref byte[] dataArr)
			{
				if(dataArr.Length<12)
					return;
				
				ulong timeStamp = BitConverter.ToUInt64(dataArr, 0);
				double value = BitConverter.ToDouble(dataArr, 8);

				lock(packetStatisticLocker)
				{
					if(firstTimeStamp==1)
						firstTimeStamp = timeStamp;
					
					if(lastTimeStamp<timeStamp)
						lastTimeStamp = timeStamp;
					
					recievedPackets++;
				}
				core.PushValue(value);
			}
			
			public void CalcDataParam()
			{
				ulong missedPackets = (lastTimeStamp-firstTimeStamp+1) - recievedPackets;
				
				lock(packetStatisticLocker)
				{
					Console.WriteLine();
					Console.WriteLine("Packets recieved = " + recievedPackets + "   Packets missed = " + missedPackets);
				}
				core.CalcStatistic();
			}

			public void WorkThread()
			{
				while (true)
                {
                    core.ThreadProcess();
                }
			}
			
		}
		
		class StatisticDatapage
		{
			public uint [] data;
			private int modaCrd;
			private uint modaVal;
			private ulong valuesCount;
			private decimal valuesSumm;

			public StatisticDatapage(int inSize)
			{
				data = new uint[inSize];
				modaCrd = -1;
				modaVal = 0;
				valuesCount = 0;
				valuesSumm = 0;
			}

			public void IncreaseCell(int inIndex)
			{
				data[inIndex]++;
				valuesCount++;
				valuesSumm += inIndex;

				if(data[inIndex]>modaVal)
				{
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