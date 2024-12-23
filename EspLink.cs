using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace EL
{
	public partial class EspLink
	{
		static readonly Regex _bootloaderRegex = new Regex(@"boot:0x([0-9a-fA-F]+)(.*waiting for download)?", RegexOptions.CultureInvariant | RegexOptions.Compiled);
		bool _inBootloader = false;
		public EspLink(string portName)
		{
			_portName = portName;
		}
		void CheckReady(bool checkConnected =true)
		{
			if(checkConnected)
			{
				if(Device==null)
				{
					throw new InvalidOperationException("The device is not connected");
				}
			}
			if (!_inBootloader)
			{
				throw new InvalidOperationException("The bootloader is not entered");
			}
		}
		public int DefaultTimeout { get; set; } = 5000;
		

	}
}
