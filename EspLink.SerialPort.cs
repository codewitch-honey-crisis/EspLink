using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace EL
{
	partial class EspLink
	{
		string _portName;
		SerialPort _port;
		int _baudRate = 115200;
		ConcurrentQueue<byte> _serialIncoming = new ConcurrentQueue<byte>();

		SerialPort GetOrOpenPort()
		{
			if (_port == null)
			{
				_port = new SerialPort(_portName, 115200);
				_port.DataReceived += _port_DataReceived;
			}
			if (!_port.IsOpen)
			{
				try
				{
					_port.Open();
				}

				catch { return null; }
			}
			return _port;
		}
		int ReadByteNoBlock()
		{
			byte result;
			if(_serialIncoming.TryDequeue(out result))
			{
				return result;
			}
			return -1;
		}
		private void _port_DataReceived(object sender, SerialDataReceivedEventArgs e)
		{
			if(e.EventType==SerialData.Chars)
			{
				var port =(SerialPort)sender;
				if (port.BytesToRead > 0) {
					var ba = new byte[port.BytesToRead];
					var len = port.Read(ba, 0, ba.Length);
					for (int i = 0; i < len; i++)
					{
						_serialIncoming.Enqueue(ba[i]);
					}
				}
			}
		}
		public async Task SetBaudRateAsync(int oldBaud, int newBaud, CancellationToken cancellationToken,int timeout = -1)
		{
			_baudRate = newBaud;
			if (Device == null || _inBootloader == false)
			{
				return;
			}
			// stub takes the new baud rate and the old one
			var secondArg = IsStub ? (uint)oldBaud : 0;
			var data = new byte[8];
			PackUInts(data, 0, new uint[] {(uint)newBaud,secondArg });
			await CommandAsync(cancellationToken, Device.ESP_CHANGE_BAUDRATE, data, 0, timeout);
			if(_port!=null&&_port.IsOpen)
			{
				_port.BaudRate = newBaud;
				Thread.Sleep(50); // ignore crap.
				_port.DiscardInBuffer();
			}
		}
		
		
		public int BaudRate
		{
			get
			{
				return _baudRate;
			}
			set
			{
				if(value!=_baudRate)
				{
					SetBaudRateAsync(_baudRate, value, CancellationToken.None,DefaultTimeout).Wait();			
				}
			}
		}
		public void Close()
		{
			if (_port != null)
			{
				if (_port.IsOpen)
				{
					try
					{
						_port.Close();
					}
					catch { }
				}
				_port = null;
			}
			Cleanup();
		}

	}
}
