using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Threading;


namespace SystemIdleMonitor
{
	static class CheckProcess
	{
		static List<string> BlackList;
		static CheckProcess()
		{
			//パス
			string AppPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
			string AppDir = Path.GetDirectoryName(AppPath);
			string AppName = Path.GetFileNameWithoutExtension(AppPath);
			string txtpath = Path.Combine(AppDir, AppName + ".txt");

			//initialize
			BlackList = new List<string>();

			//テキスト読込み
			if (File.Exists(txtpath) == false) return;
			var textList = File.ReadAllLines(txtpath).ToList();


			//BlackList登録
			BlackList = textList.Select(
			 (line) =>
			 {	//コメント削除
				 int found = line.IndexOf("//");
				 line = (-1 < found) ? line.Substring(0, found) : line;
				 line = line.Trim();

				 //.exe削除
				 bool haveExe = (Path.GetExtension(line).ToLower() == ".exe");
				 line = (haveExe) ? Path.GetFileNameWithoutExtension(line) : line;
				 return line;
			 })
				//空白行削除
			 .Where((line) => string.IsNullOrWhiteSpace(line) == false)
				//重複排除
			 .Distinct()
			 .ToList();

		}


		//BlackList内のプロセスが稼動していない？
		public static bool NotExistBlack()
		{

			foreach (var blk in BlackList)
			{
				var prclist = Process.GetProcessesByName(blk);
				if (0 < prclist.Count()) return false;
			}

			return true;
		}



	}
}
