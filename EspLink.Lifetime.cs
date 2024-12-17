using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EL
{
    partial class EspLink : IDisposable
    {
		void Cleanup()
		{
			Device = null;
#if false
			_isSpiFlashAttached = false;
#endif
		}
		void IDisposable.Dispose()
		{
			Close();
			GC.SuppressFinalize(this);
		}
		~EspLink()
		{
			Close();
		}
	}
}
