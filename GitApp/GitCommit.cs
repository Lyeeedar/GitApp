using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Media;
using ToastNotifications.Messages;

namespace GitApp
{
	//-----------------------------------------------------------------------
	public enum ChangeType
	{
		UNTRACKED,
		MODIFIED,
		ADDED,
		DELETED
	}

	//-----------------------------------------------------------------------
	public class Change : NotifyPropertyChanged
	{
		//-----------------------------------------------------------------------
		public GitCommit ViewModel { get; set; }

		//-----------------------------------------------------------------------
		public string File { get; set; }

		//-----------------------------------------------------------------------
		public ChangeType ChangeType { get; set; }

		//-----------------------------------------------------------------------
		public string Icon
		{
			get
			{
				if (ChangeType == ChangeType.ADDED || ChangeType == ChangeType.UNTRACKED)
				{
					return "/Resources/Added.png";
				}
				else if (ChangeType == ChangeType.DELETED)
				{
					return "/Resources/Removed.png";
				}
				else
				{
					return "/Resources/Modified.png";
				}
			}
		}

		//-----------------------------------------------------------------------
		public bool Added
		{
			get { return m_added; }
			set
			{
				m_added = value;
				RaisePropertyChangedEvent();

				ViewModel.RaisePropertyChangedEvent(nameof(ViewModel.ChangeMultiSelect));
				ViewModel.RaisePropertyChangedEvent(nameof(ViewModel.CanCommit));
			}
		}
		private bool m_added;

		//-----------------------------------------------------------------------
		public string Submodule { get; set; }

		//-----------------------------------------------------------------------
		public Change(string file, ChangeType changeType, GitCommit viewModel)
		{
			this.File = file;
			this.ChangeType = changeType;
			this.ViewModel = viewModel;
		}

		//-----------------------------------------------------------------------
		public string Key { get { return File + ChangeType; } }
	}

	public class GitCommit : NotifyPropertyChanged
	{
		//-----------------------------------------------------------------------
		public List<Change> ChangeList { get; set; } = new List<Change>();

		//-----------------------------------------------------------------------
		public Command<object> CommitCMD { get { return new Command<object>((obj) => { Commit(ViewModel.CurrentDirectory); }); } }

		//-----------------------------------------------------------------------
		public Command<object> UndoCMD { get { return new Command<object>((obj) => { Undo(ViewModel.CurrentDirectory); }); } }

		//-----------------------------------------------------------------------
		public string CommitType
		{
			get { return m_commitType; }
			set
			{
				m_commitType = value;
				RaisePropertyChangedEvent();
			}
		}
		private string m_commitType;

		//-----------------------------------------------------------------------
		public string CommitScope
		{
			get { return m_commitScope; }
			set
			{
				m_commitScope = value;
				RaisePropertyChangedEvent();
			}
		}
		private string m_commitScope;

		//-----------------------------------------------------------------------
		public string CommitMessage
		{
			get { return m_commitMessage; }
			set
			{
				m_commitMessage = value;
				RaisePropertyChangedEvent();

				RaisePropertyChangedEvent(nameof(CanCommit));
			}
		}
		private string m_commitMessage;

		//-----------------------------------------------------------------------
		public bool CanCommit
		{
			get { return !string.IsNullOrWhiteSpace(CommitMessage) && ChangeList.Any(e => e.Added); }
		}

		//-----------------------------------------------------------------------
		public Change SelectedChange
		{
			get { return m_selectedChange; }
			set
			{
				m_selectedChange = value;
				RaisePropertyChangedEvent();

				ViewModel.GitDiff.GetCurrentDiff(ViewModel.CurrentDirectory);
			}
		}
		private Change m_selectedChange;

		//-----------------------------------------------------------------------
		public bool? ChangeMultiSelect
		{
			get
			{
				bool? state = null;
				foreach (var change in ChangeList)
				{
					if (state.HasValue)
					{
						if (change.Added != state.Value)
						{
							return null;
						}
					}
					else
					{
						state = change.Added;
					}
				}

				return state.HasValue ? state.Value : false;
			}
			set
			{
				if (value.HasValue)
				{
					foreach (var change in ChangeList)
					{
						change.Added = value.Value;
					}
				}
				RaisePropertyChangedEvent();
			}
		}

		//-----------------------------------------------------------------------
		public ViewModel ViewModel { get; }

		//-----------------------------------------------------------------------
		public GitCommit(ViewModel viewModel)
		{
			ViewModel = viewModel;
		}

		//-----------------------------------------------------------------------
		public void Commit(string CurrentDirectory)
		{
			if (ViewModel.GitPush.PushInProgress || ViewModel.GitPull.PullInProgress)
			{
				return;
			}

			ViewModel.CMDLines.Add(new Line("\n------------------------------------\n", Brushes.DarkGray));

			try
			{
				ViewModel.ExecuteLoggedCommand("git reset HEAD -- .");

				var submodules = ChangeList.Where(e => e.Added && e.Submodule != null).Select(e => e.Submodule).Distinct().ToList();
				foreach (var submodule in submodules)
				{
					ViewModel.ExecuteLoggedCommand("git reset HEAD -- .", submodule);
				}

				foreach (var change in ChangeList)
				{
					if (change.Added)
					{
						ViewModel.ExecuteLoggedCommand("git add \"" + change.File + "\"", change.Submodule);
					}
				}

				var message = CommitMessage;

				if (!string.IsNullOrWhiteSpace(CommitType))
				{
					if (!string.IsNullOrWhiteSpace(CommitScope))
					{
						message = CommitType + "(" + CommitScope + "): " + message;
					}
					else
					{
						message = CommitType + ": " + message;
					}
				}

				var commitMessage = "git commit -m\"" + message + "\"";
				ViewModel.ExecuteLoggedCommand(commitMessage);
				foreach (var submodule in submodules)
				{
					ViewModel.ExecuteLoggedCommand(commitMessage, submodule);
				}

				CommitScope = "";
				CommitType = "";
				CommitMessage = "";

				ViewModel.GitStatus.CheckStatus();

				Extensions.SafeBeginInvoke(() =>
				{
					ViewModel.ToastNotifier.ShowSuccess("Commit complete");
				});
			}
			catch (Exception ex)
			{
				ViewModel.ToastNotifier.ShowError(ex.Message);
			}
		}

		//-----------------------------------------------------------------------
		public void Undo(string CurrentDirectory)
		{
			ProcessUtils.ExecuteCmdBlocking("git reset --soft HEAD~1", CurrentDirectory);
			foreach (var submodule in ViewModel.GitStatus.Submodules)
			{
				ProcessUtils.ExecuteCmdBlocking("git reset --soft HEAD~1", submodule);
			}

			var matches = GitLog.SemanticCommitRegex.Matches(ViewModel.GitLog.UndoableLastCommit);
			foreach (Match match in matches)
			{
				var groups = match.Groups;
				var type = groups["Type"].Value.Trim();
				var scope = groups["Scope"].Value.Trim();
				var message = groups["Description"].Value.Trim();

				CommitType = type;
				CommitScope = scope;
				CommitMessage = message;
			}

			ViewModel.GitStatus.CheckStatus();
		}
	}
}
