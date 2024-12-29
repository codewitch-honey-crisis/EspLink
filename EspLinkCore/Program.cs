using Cli;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using static System.Net.Mime.MediaTypeNames;

using File = System.IO.File;
namespace EL
{
	class HexOrDecConverter : UInt32Converter
	{
		public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
		{
			if (value is string str)
			{
				if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
				{
					str = str.Substring(2);
					uint res;
					if (!uint.TryParse(str, NumberStyles.HexNumber, CultureInfo.InvariantCulture.NumberFormat, out res))
					{
						// force the base to throw an error
						return base.ConvertFrom(context, culture, value);
					}
					return res;
				}
			}
			return base.ConvertFrom(context, culture, value);
		}
	}
	class TimeoutConverter : Int32Converter
	{
		public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
		{
			if (value is string str)
			{
				if (str.StartsWith("-") || str.Equals("0", StringComparison.Ordinal))
				{
					throw new Exception("The value must be positive");
				}
				if (str.Equals("none", StringComparison.Ordinal))
				{
					return -1;
				}
			}
			return base.ConvertFrom(context, culture, value);
		}
	}
	class HandshakeConverter : TypeConverter
	{
		public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
		{
			if (sourceType == typeof(string))
			{
				return true;
			}
			return base.CanConvertFrom(context, sourceType);
		}
		public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
		{
			if (value is string str)
			{
				if (0 == string.Compare("hardware", str, StringComparison.OrdinalIgnoreCase))
				{
					return Handshake.RequestToSend;
				}
				else if (0 == string.Compare("software", str, StringComparison.OrdinalIgnoreCase))
				{
					return Handshake.XOnXOff;
				}
				else if (0 == string.Compare("both", str, StringComparison.OrdinalIgnoreCase))
				{
					return Handshake.RequestToSendXOnXOff;
				}
				else if (0 == string.Compare("none", str, StringComparison.OrdinalIgnoreCase))
				{
					return Handshake.None;
				}
			}
			return base.ConvertFrom(context, culture, value);
		}
	}
	class SerialTypeConverter : TypeConverter
	{
		public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
		{
			if (sourceType == typeof(string))
			{
				return true;
			}
			return base.CanConvertFrom(context, sourceType);
		}
		public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
		{
			if (value is string str)
			{
				if (0 == string.Compare("auto", str, StringComparison.OrdinalIgnoreCase))
				{
					return EspSerialType.Autodetect;
				}
				else if (0 == string.Compare("standard", str, StringComparison.OrdinalIgnoreCase))
				{
					return EspSerialType.Standard;
				}
				else if (0 == string.Compare("jtag", str, StringComparison.OrdinalIgnoreCase))
				{
					return EspSerialType.UsbSerialJtag;
				}
			}
			return base.ConvertFrom(context, culture, value);
		}
	}

	class Program
	{
		[CmdArg("help", Group = "help", Description = "Displays this screen and exits")]
		static bool help = false;
		[CmdArg(Name = "ports", Group = "ports", ElementName = "ports", Description = "List the COM ports")]
		static bool ports = false;
		[CmdArg(Ordinal = 0, Optional = false, ElementName = "port", Description = "The COM port to use")]
		static string port = null;
		[CmdArg(Ordinal = 1, Optional = false, ElementName = "file", Description = "The input file")]
		static FileInfo input = null;
		[CmdArg(Ordinal = 2, Optional = true, ElementConverter = "EL.HexOrDecConverter", ElementName = "offset", Description = "The flash address to load the file at. Defaults to 0x10000 (or 0x8000 for partitions)")]
		static uint offset = 0xFFFFFFFF;
		[CmdArg(Name = "chunk", Optional = true, ElementConverter = "EL.HexOrDecConverter", ElementName = "kilobytes", Description = "The size of blocks to use in kilobytes. Defaults to 16")]
		static uint chunk = 0;
		[CmdArg(Name = "baud", Optional = true, ElementName = "baud", Description = "The baud to upload at")]
		static int baud = 115200 * 8;
		[CmdArg(Name = "type", Optional = true, ElementName = "type", ElementConverter = "EL.SerialTypeConverter", Description = "The type of serial connection: auto (default), standard, or jtag")]
		static EspSerialType serialType = EspSerialType.Autodetect;
		[CmdArg(Name = "handshake", Optional = true, ElementName = "handshake", ElementConverter = "EL.HandshakeConverter", Description = "The serial handshake to use: hardware (default), software, both or none.")]
		static Handshake handshake = Handshake.RequestToSend;
		[CmdArg(Name = "timeout", Optional = true, ElementName = "seconds", ElementConverter = "EL.TimeoutConverter", Description = "The timeout for I/O operations, in seconds or \"none\". Defaults to 5")]
		static int timeout = 0;
		[CmdArg(Name = "partition", Optional = true, ElementName = "partition", Description = "Flashes a partition table from a CSV or binary file")]
		static bool partition = false;
		[CmdArg(Name = "nocompress", Optional = true, ElementName = "nocompress", Description = "Do not use compression")]
		static bool nocompress = false;

		internal class EspProgress : IProgress<int>
		{
			int _old = -1;
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

		static async Task<int> Main(string[] args)
		{
			
#if !DEBUG
			try
			{
#endif
				CliUtility.ParseAndSet(args, null, typeof(Program));
#if !DEBUG
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine("Error: " + ex.Message);
				return 1;
			}
#endif
			if (help)
			{
				CliUtility.PrintUsage(CliUtility.GetSwitches(null, typeof(Program)));
				return 0;
			}
			if (ports)
			{
				Console.Error.WriteLine($"{CliUtility.AssemblyTitle} v{CliUtility.AssemblyVersion}");
				Console.Error.WriteLine();

				foreach (var port in EspLink.GetPorts())
				{
					Console.WriteLine(port);

				}
				return 0;
			}
			try
			{
				if (!input.FullName.StartsWith("\\\\") && !File.Exists(input.FullName))
				{
					throw new FileNotFoundException("The input file \"" + input.Name + "\" could not be found", input.FullName);
				}
				using (var link = new EspLink(port, serialType))
				{
					if (chunk == 0)
					{
						chunk = 16;
					}
					link.DefaultTimeout = timeout == -1 ? -1 : timeout == 0 ? 5000 : timeout * 1000;
					
					using (var cts = new CancellationTokenSource())
					{

						Console.CancelKeyPress += (object sender, ConsoleCancelEventArgs e) =>
						{
							e.Cancel = true;
							cts.Cancel();
						};

						var tok = cts.Token;
						Console.WriteLine($"{CliUtility.AssemblyTitle} v{CliUtility.AssemblyVersion}");
						Console.WriteLine();
						await Console.Out.FlushAsync();
						string hs;
						switch (handshake)
						{
							case Handshake.None:
								hs = "no handshaking";
								break;
							case Handshake.RequestToSend:
								hs = "hardware handshaking";
								break;
							case Handshake.XOnXOff:
								hs = "software handshaking";
								break;
							default:
								hs = "hardware and software handshaking";
								break;
						}
						string sjt = link.IsUsbSerialJtag ? " (USB Serial JTAG)" : "";
						Console.Write($"Connecting to {port}{sjt} with {hs}...");
						await Console.Out.FlushAsync();
						link.SerialHandshake = handshake;
						await link.ConnectAsync(EspConnectMode.Default, 3, false, tok, link.DefaultTimeout, new EspProgress());
						Console.WriteLine("done!");
						await Console.Out.FlushAsync();
						Console.WriteLine("Running stub... ");
						await Console.Out.FlushAsync();
						await link.RunStubAsync(tok, link.DefaultTimeout, new EspProgress());
						Console.WriteLine();
						await Console.Out.FlushAsync();

						if (baud != 115200)
						{
							await link.SetBaudRateAsync(baud, tok, link.DefaultTimeout);
							Console.WriteLine($"Changed baud rate to {link.BaudRate}");
						}
						if (input.FullName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
						{
							partition = true;
						}
						if (offset == 0xFFFFFFFF)
						{
							offset = partition ? (uint)0x8000 : 0x10000;
						}
						using (FileStream stm = File.Open(input.FullName, FileMode.Open, FileAccess.Read))
						{
							var blocksize = chunk == 0 ? link.FlashWriteBlockSize: chunk * 1024;
							var iscsv = partition && input.FullName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);
							var part = iscsv ? "Parsing partition table and f" : "F";
							Console.WriteLine($"{part}lashing to offset 0x{offset:X}... ");
							await Console.Out.FlushAsync();

							if (iscsv)
							{
								TextReader reader = new StreamReader(stm);
								await link.FlashPartitionAsync(tok, reader, !nocompress, blocksize, offset, 3, false, link.DefaultTimeout, new EspProgress());
							}
							else
							{
								await link.FlashAsync(tok, stm, !nocompress, blocksize, offset, 3, false, link.DefaultTimeout, new EspProgress());
							}
							Console.WriteLine();
							Console.WriteLine("Hard resetting");
							await Console.Out.FlushAsync();
							await link.ResetAsync(tok);
						}
					}
				}
				return 0;
			}
			catch (OperationCanceledException)
			{
				Console.WriteLine();
				Console.WriteLine("Operation canceled by user. Device may be in invalid state.");
				return 1;
			}
#if !DEBUG
			catch (Exception ex)
			{
				CliUtility.PrintUsage(CliUtility.GetSwitches(null, typeof(Program)));
				Console.WriteLine();
				Console.Error.WriteLine("Error: " + ex.Message);
				return 1;
			}
#endif
		}

	}
}
