using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace EL
{
	partial class EspLink
	{
		/// <summary>
		/// A reset strategy
		/// </summary>
		/// <param name="port">The target serial port</param>
		/// <param name="cancellationToken">The token that can be used to cancel the request</param>
		/// <returns>True if the reset was successful, otherwise false</returns>
		public delegate Task<bool> ResetStrategy(SerialPort port,CancellationToken cancellationToken);
		/// <summary>
		/// Do not reset
		/// </summary>
		public static readonly ResetStrategy NoResetStrategy = new ResetStrategy(NoResetImplAsync);
		/// <summary>
		/// Hard reset the device (doesn't enter bootloader/will exit bootloader)
		/// </summary>
		public static readonly ResetStrategy HardResetStrategy = new ResetStrategy(HardResetImplAsync);
		/// <summary>
		/// Hard reset the device (USB)
		/// </summary>
		public static readonly ResetStrategy HardResetUsbStrategy = new ResetStrategy(HardResetUsbImplAsync);
		/// <summary>
		/// Reset the device using Dtr/Rts to force the MCU into bootloader mode
		/// </summary>
		public static readonly ResetStrategy ClassicResetStrategy = new ResetStrategy(ClassicResetImplAsync);
		/// <summary>
		/// Reset the device using Dtr/Rts to force the MCU into bootloader mode (USB Serial JTAG)
		/// </summary>
		public static readonly ResetStrategy SerialJtagResetStrategy = new ResetStrategy(SerialJtagResetImplAsync);
		static async Task<bool> SerialJtagResetImplAsync(SerialPort port, CancellationToken cancellationToken)
		{
			if (port == null || !port.IsOpen) { return false; }
			port.RtsEnable = false;
			port.DtrEnable = port.DtrEnable;
			port.DtrEnable = false;
			await Task.Delay(100, cancellationToken);
			cancellationToken.ThrowIfCancellationRequested();
			port.DtrEnable = true;
			port.RtsEnable = false;
			port.DtrEnable = port.DtrEnable;
			await Task.Delay(100, cancellationToken);
			cancellationToken.ThrowIfCancellationRequested();
			port.RtsEnable = true;
			port.DtrEnable = port.DtrEnable;
			port.DtrEnable = false;
			port.RtsEnable = true;
			port.DtrEnable = port.DtrEnable;
			await Task.Delay(100, cancellationToken);
			cancellationToken.ThrowIfCancellationRequested();
			port.DtrEnable = false;
			port.RtsEnable = false;
			port.DtrEnable = port.DtrEnable;

			return true;
		}
		async static Task<bool> HardResetImplIntAsync(SerialPort port, bool isUsb, CancellationToken cancellationToken)
		{
			if (port == null || !port.IsOpen) { return false; }
			port.RtsEnable = true;
			port.DtrEnable = port.DtrEnable;
			if (isUsb)
			{
				await Task.Delay(200,cancellationToken);
				cancellationToken.ThrowIfCancellationRequested();
				port.RtsEnable = false;
				port.DtrEnable = port.DtrEnable;
				await Task.Delay(200,cancellationToken);
				cancellationToken.ThrowIfCancellationRequested();
			}
			else
			{
				await Task.Delay(100,cancellationToken);
				cancellationToken.ThrowIfCancellationRequested();
				port.RtsEnable = false;
				port.DtrEnable = port.DtrEnable;

			}

			return true;
		}
		static async Task<bool> NoResetImplAsync(SerialPort port, CancellationToken cancellationToken)
		{
			await Task.CompletedTask;
			return true;
		}
		static async Task<bool> HardResetImplAsync(SerialPort port, CancellationToken cancellationToken)
		{
			return await HardResetImplIntAsync(port, false,cancellationToken);
		}
		static async Task<bool> HardResetUsbImplAsync(SerialPort port, CancellationToken cancellationToken)
		{
			return await HardResetImplIntAsync(port, true,cancellationToken);
		}
		static async Task<bool> ClassicResetImplAsync(SerialPort port, CancellationToken cancellationToken)
		{
			if (port == null || !port.IsOpen) { return false; }
			port.DtrEnable = false;
			port.RtsEnable = true;
			port.DtrEnable = port.DtrEnable;
			await Task.Delay(50,cancellationToken);
			cancellationToken.ThrowIfCancellationRequested();
			port.DtrEnable = true;
			port.RtsEnable = false;
			port.DtrEnable = port.DtrEnable;
			await Task.Delay(550,cancellationToken);
			cancellationToken.ThrowIfCancellationRequested();
			port.DtrEnable = false;
			return true;
		}
		/// <summary>
		/// Terminates any connection and asynchronously reset the device.
		/// </summary>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to cancel the operation</param>
		/// <param name="strategy">The reset strategy to use, or null to hard reset</param>
		/// <exception cref="IOException">Unable to communicate with the device</exception>
		public async Task ResetAsync(CancellationToken cancellationToken,ResetStrategy strategy = null)
		{
			Close();
			try
			{
				if (strategy == null)
				{
					strategy = HardResetStrategy;
				}
				SerialPort port = GetOrOpenPort(true);
				port.Handshake = Handshake.None;
				DiscardInput();

				// On targets with USB modes, the reset process can cause the port to
				// disconnect / reconnect during reset.
				// This will retry reconnections on ports that
				// drop out during the reset sequence.
				for (var i = 2; i >= 0 && !cancellationToken.IsCancellationRequested; --i)
				{
					{
						var b = await strategy?.Invoke(port,cancellationToken);
						if (b)
						{
							return;
						}
					}
				}
				cancellationToken.ThrowIfCancellationRequested();
				throw new IOException("Unable to reset device");
				
			}
			finally
			{
				Close();
			}
		}
		/// <summary>
		/// Terminates any connection and reset the device.
		/// </summary>
		/// <param name="strategy">The reset strategy to use, or null to hard reset</param>
		/// <exception cref="IOException">Unable to communicate with the device</exception>
		public void Reset(ResetStrategy strategy = null)
		{
			ResetAsync(CancellationToken.None, strategy).Wait();
		}
	}
}
