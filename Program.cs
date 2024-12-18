using Cli;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using static System.Net.WebRequestMethods;

namespace EL
{

	internal class Program
	{
		static readonly Regex _scrapeTags = new Regex(@"([0-9]+\.[0-9]+\.[0-9]+\.[0-9]+)<\/h2>",RegexOptions.IgnoreCase);
		const string tagUrl = "https://github.com/codewitch-honey-crisis/EspLink/releases";
		const string updateUrlFormat = "https://github.com/codewitch-honey-crisis/EspLink/releases/download/{0}/esplink.exe";
		[CmdArg("update",Group="update",Description ="Updates the application if a new version is available")]
		static bool update = false;
		[CmdArg("help", Group = "help", Description = "Displays this screen and exits")]
		static bool help = false;
		[CmdArg(Ordinal = 0,Optional =false,ElementName = "port",Description ="The COM port to use")]
		static string port = null;
		[CmdArg(Ordinal = 1, Optional = false,ElementName ="file", Description ="The input file")]
		static FileInfo input = null;
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
		static async Task DownloadVersionAsync(Version version)
		{
			var localpath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

			using (var http = new HttpClient())
			{
				var url = string.Format(updateUrlFormat, version.ToString());
				using (var input = await http.GetStreamAsync(url))
				{
					var filepath = Path.Combine(localpath, "esplink.exe.download");
					try
					{
						System.IO.File.Delete(filepath);
					}
					catch { }
					using (var output = System.IO.File.OpenWrite(filepath))
					{
						await input.CopyToAsync(output);
					}
				}
			}
		}
		static async Task<Version> TryGetLaterVersionAsync()
		{
			try
			{
				var ver = Assembly.GetExecutingAssembly().GetName().Version;
				using (var http = new HttpClient())
				{
					var versions = new List<Version>();
					using (var reader = new StreamReader(await http.GetStreamAsync(tagUrl)))
					{
						var match = _scrapeTags.Match(reader.ReadToEnd());
						while (match.Success)
						{
							Version v;
							if (Version.TryParse(match.Groups[1].Value, out v))
							{
								versions.Add(v);
							}
							match = match.NextMatch();
						}
					}
					versions.Sort();
					var result = versions[versions.Count - 1];
					if (result > ver)
					{
						return result;
					}
				}
			}
			catch { }
			return new Version();
		}
		static async Task ExtractUpdaterAsync()
		{
			var localpath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			using(Stream input = Assembly.GetExecutingAssembly().GetManifestResourceStream("EL.EspLinkUpdater.exe"))
			{
				if(input==null)
				{
					throw new Exception("Could not extract updater");
				}
				using (var output = System.IO.File.OpenWrite(Path.Combine(localpath, "EspLinkUpdater.exe")))
				{
					await input.CopyToAsync(output);
				}
			}
		}
		static async Task<int> Main(string[] args)
		{
			var updaterpath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "EspLinkUpdater.exe");

			// in case we just updated:
			if (args.Length == 1)
			{
				if (args[0] == "/finish_updater")
				{
					try
					{

						if (System.IO.File.Exists(updaterpath))
						{
							System.IO.File.Delete(updaterpath);
							Console.WriteLine("Application updated.");
							return 0;
						}
					}
					catch
					{
						Console.WriteLine("Warning: Could not delete temporary files.");

						return 0;
					}
				}
			}
			CliUtility.ParseAndSet(args, null, typeof(Program));
			if(help)
			{
				CliUtility.PrintUsage(CliUtility.GetSwitches(null,typeof(Program)));
				var latest = await TryGetLaterVersionAsync();

				if (Assembly.GetExecutingAssembly().GetName().Version < latest)
				{
					Console.WriteLine();
					Console.WriteLine("An update is available.");
				
				}
				return 0;
			}
			if (update)
			{
				var latest = await TryGetLaterVersionAsync();
				if (Assembly.GetExecutingAssembly().GetName().Version < latest)
				{
					await DownloadVersionAsync(latest);
					await ExtractUpdaterAsync();
					var psi = new ProcessStartInfo()
					{
						FileName = updaterpath,
						UseShellExecute = true
					};
					var proc = Process.Start(psi);
				}
				else
				{
					Console.WriteLine("A later version was not available. No update was performed.");
					return 1;
				}
				return 0;
			}
			try
			{
				
				using (var link = new EspLink(port))
				{
					var latest = await TryGetLaterVersionAsync();

					if (Assembly.GetExecutingAssembly().GetName().Version < latest)
					{
						Console.WriteLine("An update is available. Run with /update to update the utility");
						Console.WriteLine();
					}
					var cts = new CancellationTokenSource();
					Console.CancelKeyPress += (object sender, ConsoleCancelEventArgs e) => {
						cts.Cancel();
					};
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
					using (var stm = System.IO.File.Open(input.FullName, FileMode.Open, FileAccess.Read))
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
