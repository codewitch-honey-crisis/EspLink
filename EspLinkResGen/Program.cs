using System;
using System.IO;

using Json;

namespace EspLinkResGen
{
	internal class Program
	{
		static uint SwapBytes(uint x)
		{
			// swap adjacent 16-bit blocks
			x = (x >> 16) | (x << 16);
			// swap adjacent 8-bit blocks
			return ((x & 0xFF00FF00) >> 8) | ((x & 0x00FF00FF) << 8);

		}
		static void Main(string[] args)
		{
			Console.WriteLine(Environment.CommandLine);
			var outpath = args[1];
			foreach(var file in Directory.GetFiles(args[0],"*.json"))
			{
				using (var reader = new StreamReader(file))
				{
					JsonObject o = (JsonObject)JsonObject.Parse(reader);
					var fname = Path.Combine(outpath, Path.GetFileNameWithoutExtension(file) + ".idx");
					try
					{
						if (File.Exists(fname))
						{
							File.Delete(fname);
						}
					}
					catch { }
					using (var output = File.OpenWrite(fname))
					{
						uint entryPoint = (uint)(double)o["entry"];
						uint textStart = (uint)(double)o["text_start"];
						uint dataStart = (uint)(double)o["data_start"];
						if (!BitConverter.IsLittleEndian)
						{
							entryPoint = SwapBytes(entryPoint);
							textStart = SwapBytes(textStart);
							dataStart = SwapBytes(dataStart);
						}
						var ba = BitConverter.GetBytes(entryPoint);	
						output.Write(ba,0,ba.Length);
						ba = BitConverter.GetBytes(textStart);
						output.Write(ba, 0, ba.Length);
						ba = BitConverter.GetBytes(dataStart);
						output.Write(ba, 0, ba.Length);
					}
					var text = Convert.FromBase64String((string)o["text"]);
					fname = Path.Combine(outpath, Path.GetFileNameWithoutExtension(file) + ".text");
					try
					{
						if (File.Exists(fname))
						{
							File.Delete(fname);
						}
					}
					catch { }
					using (var output = File.OpenWrite(fname))
					{
						output.Write(text,0,text.Length);
					}
					var data = Convert.FromBase64String((string)o["data"]);
					fname = Path.Combine(outpath, Path.GetFileNameWithoutExtension(file) + ".data");
					try
					{
						if (File.Exists(fname))
						{
							File.Delete(fname);
						}
					}
					catch { }
					using (var output = File.OpenWrite(fname))
					{
						output.Write(data, 0, data.Length);
					}

				}
			}
		}
	}
}
