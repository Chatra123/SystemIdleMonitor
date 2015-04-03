using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Collections;


namespace SystemIdleMonitor
{

	#region pcQueue
	class pcQueue
	{

		public bool Enable;
		public float Threshold { get; private set; }
		public int Capacity { get; private set; }

		readonly object sync = new object();
		private List<float> items;
		private List<float> Items
		{
			get { lock (sync) { return items; } }
			set { lock (sync) { items = value; } }
		}

		public int Count { get { return Items.Count; } }
		public bool HasData { get { return 0 < Items.Count; } }
		public bool Fill { get { return Capacity <= Items.Count; } }

		public float LatestValue { get { lock (sync) { return (Enable && HasData) ? Items[Items.Count - 1] : 0; } } }
		public float Average { get { lock (sync) { return (Enable && HasData) ? Items.Average() : 0; } } }


		//Constructor
		public pcQueue(float newThreshold, int newCapacity)
		{
			Enable = (0 <= newThreshold && 0 < newCapacity) ? true : false;
			Threshold = newThreshold;
			Capacity = newCapacity;
			Items = new List<float>(newCapacity);
		}

		//Reset
		public void Reset()
		{
			lock (sync) { Items = new List<float>(Capacity); }
		}


		//Push
		public void Push(float value)
		{
			if (Enable == false) return;
			lock (sync)
			{
				if (Fill) Pop();
				Items.Add(value);
			}
		}

		//Pop
		public void Pop()
		{
			if (Enable == false) return;
			lock (sync)
			{
				if (HasData) Items.RemoveAt(0);
			}
		}


		//IsUnderThreshold
		public bool IsUnderThreshold
		{
			get
			{
				if (Enable == false) return true;
				lock (sync)
				{
					//Averageは完全な０．００にならないから、Thresholdに０．０１加える。
					if (Fill && Average < Threshold + 0.01) return true;
					else return false;
				}
			}
		}

	}
	#endregion





	#region SystemMonitor
	class SystemMonitor
	{
		SystemCounter systemCounter;
		pcQueue queCpu, queHDD, queNetwork;
		int QueCapacity;


		Timer MonitoringTimer;
		Random rnd = new Random();
		public bool TimerIsWorking = false;
		readonly object sync = new object();


		//Constructor
		public SystemMonitor(float thd_cpu, float thd_hdd, float thd_net, int duration_sec)
		{
			lock (sync)
			{
				//Queue
				QueCapacity = (int)Math.Ceiling(1.0 * duration_sec / 3.0);
				queCpu = new pcQueue(thd_cpu, QueCapacity);
				queHDD = new pcQueue(thd_hdd, QueCapacity);
				queNetwork = new pcQueue(thd_net, QueCapacity);

				//SystemCounter
				systemCounter = new SystemCounter();

				//timer
				MonitoringTimer = new Timer(new TimerCallback(timer_Tick));
				TimerStart();
			}
		}


		//TimerStart
		public void TimerStart()
		{
			lock (sync)
			{
				//interval = 1sec			CPU usage = 0.25%
				//         = 3sec			CPU usage = 0.08%
				MonitoringTimer.Change(2000, 2000);		//  3.0s ± 1.0sec
				TimerIsWorking = true;
			}
		}


		//TimerStop
		public void TimerStop()
		{
			lock (sync)
			{
				TimerIsWorking = false;
				MonitoringTimer.Change(Timeout.Infinite, Timeout.Infinite);

				//reset
				queCpu.Reset();
				queHDD.Reset();
				queNetwork.Reset();
			}
		}


		//Tick
		public void timer_Tick(object obj)
		{
			//intervalが３秒と大きいので等間隔処理にならないようランダム時間待機
			Thread.Sleep(rnd.Next(0, 2000));

			lock (sync)
			{
				//Enqueque
				if (queCpu.Enable) queCpu.Push(systemCounter.Processor.Usage());
				if (queHDD.Enable) queHDD.Push(systemCounter.HDD.Transfer(BytePerSec.MiBps));
				if (queNetwork.Enable) queNetwork.Push(systemCounter.Network.Transfer(bitPerSec.Mibps));
			}
		}


		//SystemIsIdle
		public bool SystemIsIdle()
		{
			lock (sync)
			{
				return TimerIsWorking
								&& queCpu.IsUnderThreshold
								&& queHDD.IsUnderThreshold
								&& queNetwork.IsUnderThreshold;
			}
		}


		//show
		public string GetMonitorState()
		{
			lock (sync)
			{
				string lineformat = "{0,6:##0} %     {1,6:###0.0} MiB/s {2,6:###0.0} Mibps";
				string cpuformat = "{0,6:##0} %     ", hddformat = "{0,6:###0.0} MiB/s ", netformat = "{0,6:###0.0} Mibps";
				string line, empty = "             ";

				var state = new StringBuilder();
				state.AppendLine("               CPU         HDD          Network");


				//Threshold
				state.AppendFormat("Threshold :" + lineformat,
															queCpu.Threshold, queHDD.Threshold, queNetwork.Threshold);
				state.AppendLine();
				state.AppendLine();

				//Average
				line = "";
				line += "  Average :";
				line += (queCpu.Enable) ? String.Format(cpuformat, queCpu.Average) : empty;
				line += (queHDD.Enable) ? String.Format(hddformat, queHDD.Average) : empty;
				line += (queNetwork.Enable) ? String.Format(netformat, queNetwork.Average) : empty;
				state.AppendLine(line);

				//Value
				line = "";
				line += "    Value :";
				line += (queCpu.Enable) ? String.Format(cpuformat, queCpu.LatestValue) : empty;
				line += (queHDD.Enable) ? String.Format(hddformat, queHDD.LatestValue) : empty;
				line += (queNetwork.Enable) ? String.Format(netformat, queNetwork.LatestValue) : empty;
				state.AppendLine(line);

				//fill
				var quelist = new pcQueue[] { queCpu, queHDD, queNetwork };
				quelist = quelist.Where((que) => que.Enable).ToArray();
				if (0 < quelist.Count())
				{
					line = "";
					line += "     fill :";
					line += String.Format(" {0,3:##0} / {1,3:##0}", quelist[0].Count, quelist[0].Capacity);
					state.AppendLine(line);
				}

				return state.ToString();
			}
		}
	}
	#endregion



}
