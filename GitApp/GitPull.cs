using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using ToastNotifications.Messages;

namespace GitApp
{
	public class GitPull : NotifyPropertyChanged
	{
		//-----------------------------------------------------------------------
		public Command<object> PullCMD { get { return new Command<object>((obj) => { Pull(ViewModel.CurrentDirectory); }); } }
		public bool PullInProgress { get; set; }

		//-----------------------------------------------------------------------
		public ViewModel ViewModel { get; }

		//-----------------------------------------------------------------------
		public GitPull(ViewModel viewModel)
		{
			ViewModel = viewModel;
		}

		//-----------------------------------------------------------------------
		public void Pull(string CurrentDirectory)
		{
			if (ViewModel.GitPush.PushInProgress || PullInProgress)
			{
				return;
			}

			PullInProgress = true;
			RaisePropertyChangedEvent(nameof(PullInProgress));

			ViewModel.CMDLines.Add(new Line("\n------------------------------------\n", Brushes.DarkGray));
			ViewModel.CMDLines.Add(new Line("Button pull", Brushes.SkyBlue));

			Task.Run(() =>
			{
				var failed = "";
				ProcessUtils.ExecuteCmd(
					"git pull --rebase",
					CurrentDirectory,
					(output) =>
					{
						Extensions.SafeBeginInvoke(() =>
						{
							ViewModel.CMDLines.Add(new Line(output, Brushes.White));
						});
					},
					(error) =>
					{
						Extensions.SafeBeginInvoke(() =>
						{
							ViewModel.CMDLines.Add(new Line(error, Brushes.Red));
							failed += error;
						});
					},
					null);

				ProcessUtils.ExecuteCmd(
					"git submodule update --init --recursive --force --rebase",
					CurrentDirectory,
					(output) =>
					{
						Extensions.SafeBeginInvoke(() =>
						{
							ViewModel.CMDLines.Add(new Line(output, Brushes.White));
						});
					},
					(error) =>
					{
						Extensions.SafeBeginInvoke(() =>
						{
							ViewModel.CMDLines.Add(new Line(error, Brushes.Red));
							failed += error;
						});
					},
					null);

				if (!string.IsNullOrWhiteSpace(failed))
				{
					Extensions.SafeBeginInvoke(() =>
					{
						ViewModel.ToastNotifier.ShowError(failed);
					});
				}
				else
				{
					Extensions.SafeBeginInvoke(() =>
					{
						ViewModel.ToastNotifier.ShowSuccess("Pull complete");
					});
				}

				Extensions.SafeBeginInvoke(() =>
				{
					PullInProgress = false;
					RaisePropertyChangedEvent(nameof(PullInProgress));

					ViewModel.GitStatus.CheckStatus();
				});
			});
		}
	}
}
