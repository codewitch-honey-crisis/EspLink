using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace EspLinkUpdater
{
	internal class Program
	{
		static int Main()
		{
			var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			var exeFile = Path.Combine(path, "esplink.exe");
			var downloadFile = Path.Combine(path, "esplink.exe.download");
			try
			{
				File.Delete(exeFile);
			}
			catch { };
			try
			{
				File.Move(downloadFile, exeFile);
				var psi = new ProcessStartInfo()
				{
					FileName = exeFile,
					UseShellExecute = true,
					CreateNoWindow = true,
					Arguments = "/finish_updater"
				};
				var proc = Process.Start(psi);
				return 0;
			}
			catch
			{
				return 1;
			}
		}
	}
}
