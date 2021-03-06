﻿using System;
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
        //-----------------------------------------------------------------------
        public static bool OperationInProgress = false;

        //-----------------------------------------------------------------------
        public static string ExecuteCmdBlocking(string cmd, string workingDirectory)
		{
			var output = new StringBuilder();
			var error = new StringBuilder();
			ExecuteCmd(cmd, workingDirectory, 
                (line) => 
                {
                    output.AppendLine(line);
                }, 
                (line) =>
                {
                    if (line.StartsWith("fatal"))
                    {
                        error.AppendLine(line);
                    }
                    else
                    {
                        output.AppendLine(line);
                    }
                }, null);

			if (error.Length > 0)
			{
				throw new Exception(error.ToString());
			}

			return output.ToString();
		}

		//-----------------------------------------------------------------------
		public static void ExecuteCmd(string cmd, string workingDirectory, Action<string> processOutput, Action<string> processError, int? timeout = 5000)
		{
            OperationInProgress = true;

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
					Arguments = "cmd /S /C \"" + cmd + "\""
				};

				System.Timers.Timer timeoutTimer = null;

				if (timeout.HasValue)
				{
					timeoutTimer = new System.Timers.Timer();
					timeoutTimer.Elapsed += (e, args) =>
					{
						try
						{
							p.Kill();
							processError("Force kill");
						}
						catch (Exception) { }
					};
					timeoutTimer.Interval = timeout.Value;

					timeoutTimer.Start();
				}

				p.OutputDataReceived += (sender, args) =>
				{
					if (args.Data != null)
					{
						processOutput(args.Data);

						if (timeout.HasValue)
						{
							timeoutTimer.Stop();
							timeoutTimer.Start();
						}
					}
				};
				p.ErrorDataReceived += (sender, args) =>
				{
					if (args.Data != null)
					{
						processError(args.Data);

						if (timeout.HasValue)
						{
							timeoutTimer.Stop();
							timeoutTimer.Start();
						}
					}
				};

				p.EnableRaisingEvents = true;
				p.Start();

				p.BeginOutputReadLine();
				p.BeginErrorReadLine();

				p.WaitForExit();

				if (timeout.HasValue)
				{
					timeoutTimer.Stop();
					timeoutTimer.Dispose();
				}
			}

            OperationInProgress = false;

        }
	}
}
