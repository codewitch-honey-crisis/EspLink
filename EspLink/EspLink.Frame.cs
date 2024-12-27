using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EL
{
	partial class EspLink
	{
		const byte _FrameDelimiter = 0xC0;
		const byte _FrameEscape = 0xDB;
		async Task WriteFrameAsync(byte[] data, int index, int length, CancellationToken cancellationToken, int timeout = -1)
		{
			int count = 0;
			for(var i = index;i<index+length;++i)
			{
				var b = data[i];
				switch(b)
				{
					case _FrameEscape:
					case _FrameDelimiter:
						count += 2;
						break;
					default:
						++count;
						break;
				}
			}
			var toWrite = new byte[count+2];
			toWrite[0] = _FrameDelimiter;
			var j = 1;
			for(var i = index;i<index+length;++i)
			{
				var src = data[i + index];
				switch(src)
				{
					case _FrameEscape:
						toWrite[j++] = _FrameEscape;
						toWrite[j++] = 0xDD;
						break;
					case _FrameDelimiter:
						toWrite[j++] = _FrameEscape;
						toWrite[j++] = 0xDC;
						break;
					default:
						toWrite[j++] = src;
						break;
				}
			}
			toWrite[j] = _FrameDelimiter;
			var port = GetOrOpenPort();
			try
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (_isWindows)
				{
					// TODO: Showstopper. On Linux this hangs on Serial JTAG due to https://github.com/dotnet/runtime/issues/2037
					port.WriteTimeout = timeout;
					await port.BaseStream.WriteAsync(toWrite, 0, toWrite.Length);
					cancellationToken.ThrowIfCancellationRequested();
					await port.BaseStream.FlushAsync();
				} else
				{
					// Write timeout does not work with async on linux over serial jtag
					
					port.WriteTimeout = timeout;
					port.Write(toWrite, 0, toWrite.Length);
					cancellationToken.ThrowIfCancellationRequested();
				}
			}
			finally
			{
				port.WriteTimeout = -1;
				
			}
		}
		async Task<byte[]> ReadFrameAsync(CancellationToken cancellationToken, int timeout = -1)
		{
			var bytes = new List<byte>();
			var time = 0;
			var foundStart = false;
			var inEscape = false;
			while(true)
			{
				cancellationToken.ThrowIfCancellationRequested();
				var i = ReadByteNoBlock();
				if (0 > i)
				{
					await Task.Delay(10);
					time += 10;
					if (timeout > -1 && time >= timeout)
					{
						throw new TimeoutException("The read operation timed out");
					}
					continue;
				}
				time = 0;
				if(inEscape)
				{
					switch(i)
					{
						case 0xDD:
							bytes.Add(_FrameEscape);
							break;
						case 0xDC:
							bytes.Add(_FrameDelimiter);
							break;
						default:
							throw new IOException("Invalid data found in frame");
						
					}
					inEscape = false;
					continue;
				}
				if(!foundStart)
				{
					if (i == _FrameDelimiter)
					{
						foundStart = true;
						continue;
					}
				} else if(i==_FrameDelimiter)
				{
					break;
				}
				switch(i)
				{
					case _FrameEscape:
						inEscape = true;
						continue;
					default:
						bytes.Add((byte)i);
						break;
				}
			}
			return bytes.ToArray();
		}
	}
}
