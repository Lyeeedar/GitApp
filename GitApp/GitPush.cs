using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using ToastNotifications.Messages;

namespace GitApp
{
	public class GitPush : NotifyPropertyChanged
	{
		//-----------------------------------------------------------------------
		public Command<object> PushCMD { get { return new Command<object>((obj) => { Push(ViewModel.CurrentDirectory); }); } }
		public bool PushInProgress { get; set; }

		//-----------------------------------------------------------------------
		public ViewModel ViewModel { get; }

		//-----------------------------------------------------------------------
		public GitPush(ViewModel viewModel)
		{
			ViewModel = viewModel;
		}

		//-----------------------------------------------------------------------
		public void Push(string CurrentDirectory)
		{
			if (PushInProgress || ViewModel.GitPull.PullInProgress)
			{
				return;
			}

			PushInProgress = true;
			RaisePropertyChangedEvent(nameof(PushInProgress));

			ViewModel.CMDLines.Add(new Line("\n------------------------------------\n", Brushes.DarkGray));
			ViewModel.CMDLines.Add(new Line("Button push", Brushes.SkyBlue));

			Task.Run(() =>
			{
				var failed = "";
				ProcessUtils.ExecuteCmd(
					"git push",
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
						ViewModel.ToastNotifier.ShowSuccess("Push complete");
					});
				}

				Extensions.SafeBeginInvoke(() =>
				{
					PushInProgress = false;
					RaisePropertyChangedEvent(nameof(PushInProgress));

					ViewModel.GitStatus.CheckStatus();
				});
			});
		}
	}
}
