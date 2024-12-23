using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace EL
{
	public partial class EspLink
	{
		public EspLink(string portName)
		{
			_portName = portName;
		}
		public int DefaultTimeout { get; set; } = 5000;
		

	}
}
