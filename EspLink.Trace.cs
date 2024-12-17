using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EL
{
	partial class EspLink
	{
#if TRACE
		static void TraceHexInline(byte[] data, int index, int length,TextWriter writer)
		{
			if (data == null || data.Length == 0 || length == 0) { return; }
			if (length>16) length = 16;
			for (int j = 0; j < length; ++j)
			{
				writer.Write(data[j + index].ToString("X2"));
			}
		}
		static void TraceHex(byte[] data, int index, int length, TextWriter writer)
		{
			if(length<=16)
			{
				TraceHexInline(data, index, length, writer);
				return;
			}
			writer.WriteLine();
			if (data == null || data.Length==0 || length==0) { return; }
			for (int i = index; i < length; i+=16)
			{
				writer.Write("    ");
				var len = 16;
				if(len+i>length)
				{
					len = length - i;
				}
				var rem = 16 - len;
				for(int j = 0;j<len;++j)
				{
					writer.Write(data[j + i].ToString("X2") + " ");
				}
				for(int j = 0;j<rem;++j)
				{
					writer.Write("   ");
				}
				writer.Write("| ");
				for(int j=0;j<len;++j)
				{
					var b = data[j + i];
					if(b>=32&b<127)
					{
						writer.Write((char)b);
					} else
					{
						writer.Write('.');
					}
				}
				writer.WriteLine();
			}
		}
#endif
	}
}
