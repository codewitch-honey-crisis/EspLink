using System;

namespace EL
{
	/// <summary>
	/// The type of serial connection
	/// </summary>
	public enum EspSerialType
	{
		/// <summary>
		/// Autodetect the serial type
		/// </summary>
		Autodetect = 0,
		/// <summary>
		/// Standard serial or serial over USB
		/// </summary>
		Standard = 1,
		/// <summary>
		/// USB Serial JTAG (ESP32-S3)
		/// </summary>
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
		/// <param name="serialType">The type of serial connection</param>
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
		
		public uint FlashWriteBlockSize
		{
			get
			{
				CheckReady();
				return Device.FLASH_WRITE_SIZE;
			}
		}
	}
}
