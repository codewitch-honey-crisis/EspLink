using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO.Ports;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace EL
{
	partial class EspLink
	{
		string _portName;
		SerialPort _port;
		int _baudRate = 115200;
		readonly object _serialReadLock = new object();
		Queue<byte> _serialIncoming = new Queue<byte>();	
		Handshake _serialHandshake;
		bool _isUsbSerialJTag = false;
		static readonly Lazy<Regex> _udveadmScrape = new Lazy<Regex>(() => new Regex("ID_MODEL=([0-9_A-Fa-f]+)", RegexOptions.CultureInvariant));
		/// <summary>
		/// The serial handshake protocol(s) to use
		/// </summary>
		public Handshake SerialHandshake
		{
			get => _serialHandshake;
			set { _serialHandshake = value;
				if (_port != null)
				{
					_port.Handshake = _serialHandshake;
				}
			
			}
		}
		/// <summary>
		/// True if the port is a USB serial JTAG connection, otherwise false
		/// </summary>
		public bool IsUsbSerialJtag
		{
			get
			{
				return _isUsbSerialJTag;
			}
		}
		SerialPort GetOrOpenPort(bool throwOnError=false)
		{
			if (_port == null)
			{
				_port = new SerialPort(_portName, 115200, Parity.None, 8, StopBits.One);
				_port.ReceivedBytesThreshold = 1;
				_port.DataReceived += _port_DataReceived;
				_port.ErrorReceived += _port_ErrorReceived;				
			}
			if (!_port.IsOpen)
			{
				try
				{
					_port.Open();
				}

				catch {
					if (throwOnError) { throw; }
					return null; 
				}
			}
			return _port;
		}
		void DiscardInput()
		{
			var port = GetOrOpenPort(false);
			if (port != null && port.IsOpen)
			{
				port.DiscardInBuffer();
			}
			lock (_serialReadLock)
			{
				_serialIncoming.Clear();
			}
		}
		async Task<byte[]> ReadExistingInputAsync()
		{
			var port = GetOrOpenPort(false);
			while (port != null && port.IsOpen && port.BytesToRead>0)
			{
				await Task.Delay(10);
			}
			byte[] result;
			lock (_serialReadLock)
			{
				result = new byte[_serialIncoming.Count];
				_serialIncoming.CopyTo(result, 0);
				_serialIncoming.Clear();
			}
			return result;
		}
		void _port_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
		{
			System.Diagnostics.Debug.WriteLine("Serial error: "+e.EventType.ToString());
		}

		int ReadByteNoBlock()
		{
			lock(_serialReadLock)
			{
				if(_serialIncoming.Count>0)
				{
					return _serialIncoming.Dequeue();
				}
			}
			return -1;
		}
		void _port_DataReceived(object sender, SerialDataReceivedEventArgs e)
		{
			if(e.EventType==SerialData.Chars)
			{
				int len = _port.BytesToRead;
				int i = -1;
				lock (_serialReadLock)
				{
					try
					{
						while (len-- > 0)
						{
							i = _port.ReadByte();
							if (i < 0) break;
							_serialIncoming.Enqueue((byte)i);
						}
					}
					catch
					{

					}
				}
			}
		}
		/// <summary>
		/// Asynchronously changes the baud rate
		/// </summary>
		/// <param name="newBaud">The new baud rate</param>
		/// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the operation</param>
		/// <param name="timeout">The timeout in milliseconds</param>
		/// <returns>A waitable <see cref="Task"/></returns>
		public async Task SetBaudRateAsync(int newBaud, CancellationToken cancellationToken,int timeout = -1)
		{
			int oldBaud = _baudRate;
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
				await Task.Delay(50); // ignore crap.
				DiscardInput();
			}
		}
		/// <summary>
		/// Gets or sets the baud rate
		/// </summary>
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
					SetBaudRateAsync(value, CancellationToken.None,DefaultTimeout).Wait();			
				}
			}
		}
		/// <summary>
		/// Closes the link
		/// </summary>
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
		class PortComparer : IComparer<string>
		{
			static readonly char[] _digits = new char[] {'0','1','2','3','4','5','6','7','8','9'};
			private static int NumForPort(string port, out int numIndex)
			{
				numIndex = -1;
				int idx = port.LastIndexOfAny(_digits);
				if (idx == port.Length - 1)
				{
					while (Array.IndexOf(_digits, port[idx]) > -1 && idx >= 0) --idx;
					++idx;
					var num = port.Substring(idx);
					if (int.TryParse(num, NumberStyles.Integer, CultureInfo.InvariantCulture.NumberFormat, out int result)) { numIndex = idx; return result; }
				}
				return -1;
			}
			public int Compare(string x, string y)
			{
				var xn = NumForPort(x, out int idx);
				if (xn > -1)
				{
					var xs = x.Substring(0, idx);
					var yn = NumForPort(y, out idx);
					if (yn > -1)
					{
						var ys = y.Substring(0, idx);
						var cmp = string.Compare(xs, ys, StringComparison.InvariantCulture);
						if (cmp == 0)
						{
							return xn.CompareTo(yn);
						}
					}
				}
				return string.Compare(x, y, StringComparison.InvariantCulture);
			}
			public static readonly PortComparer Default = new PortComparer();
		}
		/// <summary>
		/// Retrieves a list of the COM ports
		/// </summary>
		/// <returns>A read-only list of tuples indicating the name, id, long name, VID, PID, and description of the port</returns>
		public static string[] GetPorts()
		{
			var names = SerialPort.GetPortNames();
			Array.Sort(names, PortComparer.Default);
			return names;
		}

		
		static EspSerialType AutodetectSerialTypeUnix(string portName)
		{
			try
			{
				// get the USB Model Id of the serial port
				var psi = new ProcessStartInfo()
				{
					FileName = "udevadm",
					CreateNoWindow = true,
					RedirectStandardOutput = true,
					Arguments = $"info --query=all --name={portName}"
				};
				using (var proc = Process.Start(psi))
				{
					proc.WaitForExit();
					var output = proc.StandardOutput.ReadToEnd();
					var match = _udveadmScrape.Value.Match(output);
					if (match.Success && match.Groups.Count > 1)
					{
						if (match.Groups[1].Value == "1001")
						{
							return EspSerialType.UsbSerialJtag;
						}
					}
				}
				return EspSerialType.Standard;
			}
			catch {
				return EspSerialType.Autodetect;
			}
		}
		static EspSerialType AutodetectSerialTypeWindows(string portName)
		{
			try
			{
				var mgmtType = Type.GetType("System.Management.ManagementClass, System.Management, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", true);
				dynamic pnpCls = Activator.CreateInstance(mgmtType, new object[] { "Win32_PnPEntity" });

				var pnpCol = pnpCls.GetInstances();

				foreach (var pnpObj in pnpCol)
				{
					var clsid = pnpObj["classGuid"];

					if (clsid != null && ((string)clsid).Equals("{4d36e978-e325-11ce-bfc1-08002be10318}", StringComparison.OrdinalIgnoreCase))
					{
						string deviceId = pnpObj["deviceid"].ToString();

						string pid = null;
						int pidIndex = deviceId.IndexOf("PID_");
						if (pidIndex > -1)
						{
							string startingAtPid = deviceId.Substring(pidIndex);
							pid = startingAtPid.Substring(4, 4); // pid is four characters long
						}
						if (pid != "1001")
						{
							continue;
						}
						var nameProp = pnpObj["name"];
						var name = nameProp.ToString();
						var idx = name.IndexOf('(');
						if (idx > -1)
						{
							var lidx = name.IndexOf(')', idx + 2);
							if (lidx > -1)
							{
								name = name.Substring(idx + 1, lidx - idx - 1);
							}
						}
						if (portName.Equals(name, StringComparison.OrdinalIgnoreCase))
						{
							return EspSerialType.UsbSerialJtag;
						}

					}
				}

				return EspSerialType.Standard;
			}
			catch
			{
				return EspSerialType.Autodetect;
			}
		}
	}
}
