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
					using (var output = File.OpenWrite(Path.Combine(outpath, Path.GetFileNameWithoutExtension(file) + ".idx")))
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
					using (var output = File.OpenWrite(Path.Combine(outpath, Path.GetFileNameWithoutExtension(file) + ".text")))
					{
						output.Write(text,0,text.Length);
					}
					var data = Convert.FromBase64String((string)o["data"]);
					using (var output = File.OpenWrite(Path.Combine(outpath, Path.GetFileNameWithoutExtension(file) + ".data")))
					{
						output.Write(data, 0, data.Length);
					}

				}
			}
		}
	}
}
