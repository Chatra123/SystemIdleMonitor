using System;
using System.Collections.Generic;
using System.Diagnostics;  // for PerformanceCounter
using System.Linq;
using System.Text;
using System.IO;

namespace SystemIdleMonitor
{


	#region CounterFactory
	public enum CounterList
	{
		Processor,
		HDD_Read,
		HDD_Write,
		HDD_Transfer,
		Network_Sent,
		Network_Recive,
		Network_Transfer,
	}

	public static class CategoryList
	{
		public const string Processor = "Processor", HDD = "PhysicalDisk", Network = "Network Interface";
	}

	public static class InstanceList
	{
		public const string Total = "_Total";
	}


	static class CounterFactory
	{

		public static PerformanceCounter Create(CounterList counterName)
		{
			string[] nameset = GetCounterNameSet(counterName);
			// ex.   perfcnt = new PerformanceCounter(categoryName, counterName, instanceName);
			return new PerformanceCounter(nameset[0], nameset[1], nameset[2]);
		}

		public static PerformanceCounter Create(CounterList counterName, string insName)
		{
			string[] nameset = GetCounterNameSet(counterName);
			return new PerformanceCounter(nameset[0], nameset[1], insName);
		}



		static string[] GetCounterNameSet(CounterList list)
		{
			string[] nameset = null;

			switch (list)
			{

				case CounterList.Processor:
					nameset = new string[] { "Processor", "% Processor Time", "_Total" };
					break;

				case CounterList.HDD_Read:
					nameset = new string[] { "PhysicalDisk", "Disk Read Bytes/sec", "" };
					break;

				case CounterList.HDD_Write:
					nameset = new string[] { "PhysicalDisk", "Disk Write Bytes/sec", "" };
					break;

				case CounterList.HDD_Transfer:
					nameset = new string[] { "PhysicalDisk", "Disk Transfers/sec", "" };
					break;

				case CounterList.Network_Sent:
					nameset = new string[] { "Network Interface", "Bytes Sent/sec", "" };
					break;

				case CounterList.Network_Recive:
					nameset = new string[] { "Network Interface", "Bytes Received/sec", "" };
					break;

				case CounterList.Network_Transfer:
					nameset = new string[] { "Network Interface", "Bytes Total/sec", "" };
					break;

				default:
					break;
			}

			return nameset;
		}


		public static List<string> GetInstanceTable(string categoryName)
		{
			return new PerformanceCounterCategory(categoryName).GetInstanceNames().ToList();
		}

	}
	#endregion


	class SystemCounter
	{
		//PreLoad
		public static void PreLoad(int delay = 0)
		{
			System.Threading.ThreadPool.QueueUserWorkItem(new System.Threading.WaitCallback(
					(obj) =>
					{
						System.Threading.Thread.Sleep(delay);
						CounterFactory.Create(CounterList.Processor);
					}), null);
		}


		public ProcessorMonitor Processor;
		public HddMonitor HDD;
		public NetworkMonitor Network;

		//constructor
		public SystemCounter()
		{
			Processor = new ProcessorMonitor();
			HDD = new HddMonitor();
			Network = new NetworkMonitor();
		}




		#region Processor
		//
		//Processor
		public class ProcessorMonitor
		{
			public PerformanceCounter Processor;

			public ProcessorMonitor()
			{
				Processor = CounterFactory.Create(CounterList.Processor);
				Processor.NextValue();	//１回目のNextValueは０が返されるのでここで実行する。
			}

			public float Usage()
			{
				try { return Processor.NextValue(); }
				catch { return 0; }
			}
		}
		#endregion



		#region HDD
		//
		//HDD
		public class HddMonitor
		{
			public List<HDDCounter> HDDList = new List<HDDCounter>();
			public HDDCounter HDDTotal;


			public HDDCounter this[string driveLetter]
			{
				get { return GetCounterByName(driveLetter); }
			}

			public HddMonitor()
			{
				HDDTotal = new HDDCounter(InstanceList.Total);

				var table = CounterFactory.GetInstanceTable(CategoryList.HDD);
				foreach (var insName in table)
					if (insName.ToLower().Contains(InstanceList.Total.ToLower()) == false)
						HDDList.Add(new HDDCounter(insName));	//_Totalでないなら追加
			}

			HDDCounter GetCounterByName(string driveName)
			{
				driveName = driveName.ToLower();
				if (driveName.Contains("total")) return HDDTotal;

				driveName = System.Text.RegularExpressions.Regex.Match(driveName, "[A-Za-z]").Value;
				foreach (HDDCounter hdd in HDDList)
					if (hdd.InsName.ToLower().Contains(driveName))
						return hdd;

				throw new Exception();
			}

			//FixedDriveを検索
			public List<string> GetFixedDrive()
			{
				var fixedDrives = new List<string>();
				foreach (var drive in Environment.GetLogicalDrives())
				{
					DriveInfo di = new DriveInfo(drive);
					if (di.DriveType == DriveType.Fixed)
						fixedDrives.Add(di.ToString());
				}
				return fixedDrives;
			}


			//Read
			public float Read(BytePerSec prefix)
			{
				try { return HDDTotal.Read(prefix); }
				catch { return 0; }
			}

			//Write
			public float Write(BytePerSec prefix)
			{
				try { return HDDTotal.Write(prefix); }
				catch { return 0; }
			}

			//Transfer
			public float Transfer(BytePerSec prefix)
			{
				//return Read(prefix) + Write(prefix);
				try { return HDDTotal.Transfer(prefix); }
				catch { return 0; }
			}
		}



		public class HDDCounter
		{
			private string name;
			public PerformanceCounter readCounter, writeCounter, transferCounter;

			public HDDCounter(string insName)
			{
				name = insName;
				readCounter = CounterFactory.Create(CounterList.HDD_Read, insName);
				writeCounter = CounterFactory.Create(CounterList.HDD_Write, insName);
				transferCounter = CounterFactory.Create(CounterList.HDD_Transfer, insName);

				Read(BytePerSec.Bps);						//１回目のNextValueは０が返されるのでここで実行する。
				Write(BytePerSec.Bps);
				Transfer(BytePerSec.Bps);
			}

			public string InsName { get { return name; } }

			//Read
			public float Read(BytePerSec prefix)
			{
				try { return Prefixing.Convert(readCounter.NextValue(), (SIPrefix)prefix); }
				catch { return 0; }
			}

			//Write
			public float Write(BytePerSec prefix)
			{
				try { return Prefixing.Convert(writeCounter.NextValue(), (SIPrefix)prefix); }
				catch { return 0; }
			}

			//Transfers
			public float Transfer(BytePerSec prefix)
			{
				try { return Prefixing.Convert(transferCounter.NextValue(), (SIPrefix)prefix); }
				catch { return 0; }
			}

		}
		#endregion



		#region Network
		//
		//Network
		public class NetworkMonitor
		{
			public List<NetworkCounter> NetworkList = new List<NetworkCounter>();

			public NetworkCounter this[string nicName]
			{
				get { return GetCounterByName(nicName); }
			}

			//NetworkCounter
			public NetworkMonitor()
			{
				var table = CounterFactory.GetInstanceTable(CategoryList.Network);

				foreach (string insName in table)
					NetworkList.Add(new NetworkCounter(insName));
			}

			NetworkCounter GetCounterByName(string tgtName)
			{
				foreach (NetworkCounter counter in NetworkList)
					if (counter.InsName.ToLower().Contains(tgtName.ToLower()))
						return counter;
				return null;
			}

			public List<string> GetNic()
			{
				var namelist_Nic = new List<string>();
				foreach (var counter in NetworkList)
					namelist_Nic.Add(counter.InsName);
				return namelist_Nic;
			}


			//Receive
			public float Receive(bitPerSec bitpersec)
			{
				return NetworkList.Select((counter) => counter.Receive(bitpersec)).Sum();
			}

			//Sent
			public float Sent(bitPerSec bitpersec)
			{
				return NetworkList.Select((counter) => counter.Sent(bitpersec)).Sum();
			}

			//Transfer
			public float Transfer(bitPerSec bitpersec)
			{
				return NetworkList.Select((counter) => counter.Transfer(bitpersec)).Sum();
			}
		}



		public class NetworkCounter
		{
			private string name;
			public PerformanceCounter receiveCounter, sentCounter, transferCounter;
			public string InsName { get { return name; } }


			public NetworkCounter(string insName)
			{
				name = insName;
				receiveCounter = CounterFactory.Create(CounterList.Network_Recive, insName);
				sentCounter = CounterFactory.Create(CounterList.Network_Sent, insName);
				transferCounter = CounterFactory.Create(CounterList.Network_Transfer, insName);

				Receive(bitPerSec.bps);				//１回目のNextValueは０が返されるのでここで実行する。
				Sent(bitPerSec.bps);
				Transfer(bitPerSec.bps);
			}


			//Receive
			public float Receive(bitPerSec bitpersec)
			{
				// Byte/secをbpsに変換するために * 8
				try { return Prefixing.Convert(receiveCounter.NextValue() * 8, (SIPrefix)bitpersec); }
				catch { return 0; }
			}

			//Sent
			public float Sent(bitPerSec bitpersec)
			{
				try { return Prefixing.Convert(sentCounter.NextValue() * 8, (SIPrefix)bitpersec); }
				catch { return 0; }
			}

			//Transfer
			public float Transfer(bitPerSec bitpersec)
			{
				try { return Prefixing.Convert(transferCounter.NextValue() * 8, (SIPrefix)bitpersec); }
				catch { return 0; }
			}


		}
		#endregion


	}



	#region Prefix
	enum Byte { B = 0, KiB = 1, MiB = 2, }
	enum bit { b = 0, Kib = 1, Mib = 2, }
	enum BytePerSec { Bps = 0, KiBps = 1, MiBps = 2, }
	enum bitPerSec { bps = 0, Kibps = 1, Mibps = 2, }
	enum SIPrefix { none = 0, K = 1, M = 2, }

	static class Prefixing
	{
		//値をSIPrefixで丸める 
		public static float Convert(float value, SIPrefix prefix)
		{
			for (int i = 0; i < (int)prefix; i++)
				value /= 1024;
			return value;
		}

		// SIPrefixを自動判断
		static float AutoOptimizePrefix(float value, out SIPrefix prefix)
		{
			int iprefix;
			for (iprefix = 0; iprefix < 2; iprefix++)
			{
				if (value < 1024) break;
				value /= 1024;
			}
			prefix = (SIPrefix)iprefix;
			return value;
		}
	}
	#endregion



}
