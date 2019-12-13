using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GitApp
{
	public class ProcessUtils
	{
		public static void ExecuteCmd(string cmd, string workingDirectory, Action<string> processOutput, Action<string> processError)
		{
			using (Process p = new Process())
			{
				p.StartInfo = new ProcessStartInfo()
				{
					FileName = @"C:\Windows\System32\cmd.exe",
					CreateNoWindow = true,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					UseShellExecute = false,
					WorkingDirectory = workingDirectory,
					Arguments = "/c " + cmd
				};
				p.OutputDataReceived += (sender, args) =>
				{
					if (args.Data != null) processOutput(args.Data);
				};
				p.ErrorDataReceived += (sender, args) =>
				{
					if (args.Data != null) processError(args.Data);
				};

				p.EnableRaisingEvents = true;

				p.Start();

				p.BeginOutputReadLine();
				p.BeginErrorReadLine();

				bool exited = false;
				Task.Run(() => 
				{
					Thread.Sleep(5000);

					if (!exited)
					{
						try
						{
							p.Kill();
							processError("Force kill");
						}
						catch (Exception) { }
					}
				});
				p.WaitForExit();
				exited = true;
			}
		}
	}
}
