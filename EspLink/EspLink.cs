using System;

namespace EL
{
	public enum EspSerialType
	{
		Autodetect = 0,
		Standard = 1,
		UsbSerialJtag = 2,
	}
	/// <summary>
	/// Provides flashing capabilities for Espressif devices
	/// </summary>
	public partial class EspLink
	{
		static readonly bool _isWindows = (Environment.OSVersion.Platform == PlatformID.Win32NT || Environment.OSVersion.Platform == PlatformID.Win32Windows || Environment.OSVersion.Platform == PlatformID.Win32S);
		/// <summary>
		/// Construct a new instance on the given COM port
		/// </summary>
		/// <param name="portName">The COM port name</param>
		public EspLink(string portName, EspSerialType serialType = EspSerialType.Autodetect)
		{
			_portName = portName;
			EspSerialType type = EspSerialType.Autodetect;
			switch(serialType)
			{
				case EspSerialType.Autodetect:
					if(_isWindows)
					{
						type = AutodetectSerialTypeWindows(portName);
					} else if(Environment.OSVersion.Platform==PlatformID.Unix)
					{
						type = AutodetectSerialTypeUnix(portName);
					}
					break;
				default:
					type = serialType; break; 
			}
			if(type == EspSerialType.Autodetect)
			{
				throw new NotSupportedException("The serial type could not be autodetected");
			}
			_isUsbSerialJTag = type == EspSerialType.UsbSerialJtag;
		}
		/// <summary>
		/// The default timeout in milliseconds
		/// </summary>
		public int DefaultTimeout { get; set; } = 5000;
		

	}
}
