using System;

namespace udp_server
{
	class DataGenerator{
		private double stepCoef = 1e-4;
		private int digits = 4;
		private double firstValue, sinAmplitude;
		private int rndParam, sinFreq;
		private long timeStamp;	// на самом деле номер генерирумеого числа в последовательности, но имя лучше не придумал
		private Random rnd;

		public DataGenerator(SetingsStruct.StatisticParms inParm)
		{
			rnd = new Random();
			timeStamp = 100;

			firstValue = inParm.FirstValue;
			rndParam = inParm.RndParam;
			if(inParm.ValueDigits<=10 && inParm.ValueDigits>=0)
			{
				digits = inParm.ValueDigits;
				stepCoef = Math.Pow(10, -digits);
			}
			else
			{
				Console.WriteLine("'ValueDigits' должен быть в пределах [0, 10].   Используем 'ValueDigits' = "+digits);
			}
			sinFreq = inParm.SinFreq * 1000;
			sinAmplitude = inParm.SinAmplitude;
		}
		
		public int GetData(ref byte[] dataArr)
		{
			int dataSize=0;
			byte[] bitData;

			double sinShift=0;
			if(sinAmplitude>0 && sinFreq>0)
			{
				int sinPos = (int)((timeStamp-100)%sinFreq);
				sinShift = Math.Sin(Math.PI*2*sinPos/sinFreq);
			}
			
			double gaussRand = (rnd.Next(rndParam) + rnd.Next(rndParam) + rnd.Next(rndParam)) * stepCoef;
			if(rnd.Next(2)>0)
				gaussRand=-gaussRand;
			
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
}