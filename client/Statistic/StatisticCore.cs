using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace UDP_client
{
		public class StatisticCore
		{
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
			
			public void CalcStatistic()
			{
				decimal ma, sigmaS=0, sigmaF, median, moda;
				long prevTicks=0;
				long tFlush, tMA, tSigmaS=0, tSigmaF, tMedian, tModa;
 				int newPacketsOnCalcTime;
				TimeSpan timeToClac;

				lock(locker)
				{
					//	лок нужен чтоб ThreadProcess() не добавлял новых значений в хранилище пока мы считаем параметры
					Stopwatch timer = new Stopwatch();
					timer.Start();
					
					int currQueueLen = inputBuffer.Count;
					DatastoreUpdate();
					DataPutToStore(currQueueLen);
					if(valuesInStore==0){
						Console.WriteLine("No valuses in store.");
						return;
					}
					tFlush = timer.ElapsedTicks;
					prevTicks = timer.ElapsedTicks;

					ma = CalcMA_Fast();
					tMA = timer.ElapsedTicks-prevTicks;
					prevTicks = timer.ElapsedTicks;

					if(pages.Count<100){
						sigmaS = CalcSigma_Slow(ma);
						tSigmaS = timer.ElapsedTicks-prevTicks;
						prevTicks = timer.ElapsedTicks;
					}

					sigmaF = CalcSigma_Fast(ma);
					tSigmaF = timer.ElapsedTicks-prevTicks;
					prevTicks = timer.ElapsedTicks;

					median = CalcMedian();
					tMedian = timer.ElapsedTicks-prevTicks;
					prevTicks = timer.ElapsedTicks;

					moda = CalcModa();
					tModa = timer.ElapsedTicks-prevTicks;
					prevTicks = timer.ElapsedTicks;
					timeToClac = timer.Elapsed;

					newPacketsOnCalcTime = inputBuffer.Count;

					Console.WriteLine("dT = " + tFlush.ToString("000000") + "   Queue flush.");
					Console.WriteLine("valuses in store = " + valuesInStore + "     psges = " + pages.Count);
					Console.WriteLine("dT = " + tMA.ToString("000000") + "   MA = " + ma);
					if(pages.Count<100){
						Console.WriteLine("dT = " + tSigmaS.ToString("000000") + "   sigma (slow) = " + sigmaS);
					}
					Console.WriteLine("dT = " + tSigmaF.ToString("000000") + "   sigma (fast) = " + sigmaF);
					Console.WriteLine("dT = " + tMedian.ToString("000000") + "   Median = " + median);
					Console.WriteLine("dT = " + tModa.ToString("000000") + "   Moda = " + moda);
					
					timer.Stop();
					Console.WriteLine("total dT to calc = "+ prevTicks + "   in MiliSec = " + timeToClac.TotalMilliseconds);
					Console.WriteLine("total dT to print= "+ (timer.ElapsedTicks-prevTicks) + "   in MiliSec = " + (timer.Elapsed.TotalMilliseconds-timeToClac.TotalMilliseconds) );
					Console.WriteLine("new data during calc time = " + newPacketsOnCalcTime + "     new data during all time = "+ inputBuffer.Count);
				}
			}

			public StatisticCore(int inValPerPage, int inDigits){
				rnd = new Random();

				cValPerPage = inValPerPage;
				cValMinStep = Math.Pow(10, -inDigits);

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
}