using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;

namespace EspLinkUpdater
{
	internal class Program
	{
		static int Main()
		{
			var iswin = Environment.OSVersion.Platform == PlatformID.Win32NT || Environment.OSVersion.Platform == PlatformID.Win32Windows || Environment.OSVersion.Platform == PlatformID.Win32S;
			var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			var zipFile = Path.Combine(path, "esplink.zip");
			var exeFile = iswin?Path.Combine(path, "esplink.exe"): Path.Combine(path, "esplink.dll");
			var downloadFile = Path.Combine(path, "esplink.exe.download");
			if(File.Exists(downloadFile))
			{
				try
				{
					File.Delete(exeFile);
				}
				catch { };
				try
				{
					File.Move(downloadFile, exeFile);
					ProcessStartInfo psi;
					if (iswin)
					{
						psi = new ProcessStartInfo()
						{
							FileName = exeFile,
							UseShellExecute = true,
							CreateNoWindow = true,
							Arguments = "/finish_updater"
						};
					} else
					{
						psi = new ProcessStartInfo()
						{
							FileName = "dotnet",
							CreateNoWindow = true,
							Arguments = $"{exeFile} --finish_updater"
						};
					}
					var proc = Process.Start(psi);
					return 0;
				}
				catch
				{
					return 1;
				}
			}
			downloadFile = Path.Combine(path, "esplink.zip.download");
			if (!File.Exists(downloadFile))
			{
				Console.Error.WriteLine("Could not find downloaded content. Aborting update.");
				return 2;
			}
			using (var zip = ZipFile.OpenRead(downloadFile))
			{
				foreach(var entry in zip.Entries)
				{
					var entryPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), entry.FullName);
					try
					{
						if(File.Exists(entryPath))
						{
							File.Delete(entryPath);
						}
					}
					catch { }
				}
			}
			ZipFile.ExtractToDirectory(downloadFile, Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
			try
			{
				ProcessStartInfo psi;
				if (iswin)
				{
					psi = new ProcessStartInfo()
					{
						FileName = exeFile,
						UseShellExecute = true,
						CreateNoWindow = true,
						Arguments = "/finish_updater"
					};
				}
				else
				{
					psi = new ProcessStartInfo()
					{
						FileName = "dotnet",
						CreateNoWindow = true,
						Arguments = $"{exeFile} --finish_updater"
					};
				}
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
