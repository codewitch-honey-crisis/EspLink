using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EL
{

	internal class Program
	{
		
		internal class EspProgress : IProgress<int>
		{
			int _old=-1;
			public EspProgress()
			{
			}
			public int Value { get; private set; }
			public bool IsBounded
			{
				get
				{
					return Value >= 0;
				}
			}

			public void Report(int value)
			{
				Value = value;
				if (value != _old)
				{
					if (IsBounded)
					{
						Console.WriteLine($"{value}%");
						Console.Out.Flush();
					}
					else
					{
						Console.Write(".");
						Console.Out.Flush();
					}
					_old = value;
				}
			}
		}
		static void MonitorThreadProc(object state)
		{
			var cts = (CancellationTokenSource)state;
			while(!Console.KeyAvailable)
			{
				Thread.Sleep(10);
			}
			cts.Cancel();
		}
		static async Task<int> Main(string[] args)
		{
			if (args.Length < 2)
			{
				return -1;
			}
			var port = args[0];
			var path = args[1];
			try
			{
				using (var link = new EspLink(port))
				{
					var cts = new CancellationTokenSource();
					var mon = new Thread(new ParameterizedThreadStart(MonitorThreadProc));
					mon.Start(cts);

					var tok = cts.Token;
					Console.WriteLine("Press any key to cancel...");
					Console.Write("Connecting...");
					await Console.Out.FlushAsync();
					await link.ConnectAsync(true, 3, true, tok, link.DefaultTimeout, new EspProgress());
					Console.WriteLine("\bdone!");
					await Console.Out.FlushAsync();
					Console.WriteLine("Running stub... ");
					await Console.Out.FlushAsync();
					await link.RunStubAsync(tok, link.DefaultTimeout, new EspProgress());
					Console.WriteLine();
					await Console.Out.FlushAsync();
					await link.SetBaudRateAsync(115200, 115200 * 4, tok, link.DefaultTimeout);
					Console.WriteLine($"Changed baud rate to {link.BaudRate}");
					Console.WriteLine("Flashing... ");
					await Console.Out.FlushAsync();
					using (var stm = File.Open(path, FileMode.Open, FileAccess.Read))
					{
						await link.FlashAsync(tok, stm, 16*1024, 0x10000, 3, false, link.DefaultTimeout, new EspProgress());
						Console.WriteLine();
						Console.WriteLine("Hard resetting");
						await Console.Out.FlushAsync();
						link.Reset();
					}
					mon.Abort();
				}
				return 0;
			}
			catch (OperationCanceledException)
			{
				Console.WriteLine("Operation canceled by user. Device may be in invalid state.");
				return 1;
			}
		}
	}
}
