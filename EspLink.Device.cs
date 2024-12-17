﻿using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EL
{
	partial class EspLink
	{
		public EspDevice Device { get; private set; }
		void CreateDevice(uint value, bool isId = false)
		{
			var types = Assembly.GetExecutingAssembly().GetTypes();
			for (int i = 0; i < types.Length; ++i)
			{
				var type = types[i];
				if (typeof(EspDevice).IsAssignableFrom (type.BaseType))
				{
					var attr = type.GetCustomAttribute<EspDeviceAttribute>();
					if (attr != null)
					{
						if ((!isId && attr.Magic == value) || (isId && attr.Id==value))
						{
							Device = (EspDevice)Activator.CreateInstance(type, new object[] { this });
							return;
						}
					}
				}
			}
			throw new NotSupportedException("The connected device is not supported");
		}
	}
}
