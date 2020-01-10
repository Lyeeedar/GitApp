﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Timers;

namespace GitApp
{
	public class GitStatus : NotifyPropertyChanged
	{
		//-----------------------------------------------------------------------
		public string Branch
		{
			get { return m_branch; }
			set
			{
				m_branch = value;
				RaisePropertyChangedEvent();
			}
		}
		private string m_branch;

		//-----------------------------------------------------------------------
		public int NumberCommitsToPull
		{
			get { return m_numberCommitsToPull; }
			set
			{
				m_numberCommitsToPull = value;
				RaisePropertyChangedEvent();
				RaisePropertyChangedEvent(nameof(UpToDate));
			}
		}
		private int m_numberCommitsToPull;

		//-----------------------------------------------------------------------
		public int NumberCommitsToPush
		{
			get { return m_numberCommitsToPush; }
			set
			{
				m_numberCommitsToPush = value;
				RaisePropertyChangedEvent();
				RaisePropertyChangedEvent(nameof(UpToDate));
			}
		}
		private int m_numberCommitsToPush;

		//-----------------------------------------------------------------------
		public bool UpToDate
		{
			get
			{
				return NumberCommitsToPull + NumberCommitsToPush == 0;
			}
		}

		//-----------------------------------------------------------------------
		public bool NotARepo
		{
			get { return m_notARepo; }
			set
			{
				if (m_notARepo != value)
				{
					m_notARepo = value;
					RaisePropertyChangedEvent();
				}
			}
		}
		private bool m_notARepo;

		//-----------------------------------------------------------------------
		public ViewModel ViewModel { get; }

		//-----------------------------------------------------------------------
		public GitStatus(ViewModel viewModel)
		{
			ViewModel = viewModel;

			var timer = new Timer();
			timer.Elapsed += (e, args) =>
			{
				CheckStatus();
			};
			timer.Interval = 3000;
			timer.Start();
		}

		//-----------------------------------------------------------------------
		bool checkingStatus = false;
		string lastStatus = null;
		public void CheckStatus()
		{
			var windowActive = true;//Application.Current?.MainWindow?.IsActive ?? false;
			if (ViewModel.GitPush.PushInProgress || ViewModel.GitPull.PullInProgress || !windowActive || ProcessUtils.OperationInProgress || checkingStatus)
			{
				return;
			}

			checkingStatus = true;

			var newNumberCommitsToPush = 0;
			var newNumberCommitsToPull = 0;

			var previousChanges = new Dictionary<string, Change>();
			foreach (var change in ViewModel.GitCommit.ChangeList)
			{
				previousChanges[change.Key] = change;
			}
			var newChanges = new List<Change>();
			var addedChanges = new HashSet<string>();
			var changesChanged = false;

			Action<Change> addChange = (change) =>
			{
				if (addedChanges.Contains(change.Key))
				{
					return;
				}
				addedChanges.Add(change.Key);

				Change existingChange;
				if (previousChanges.TryGetValue(change.Key, out existingChange))
				{
					newChanges.Add(existingChange);
				}
				else
				{
					changesChanged = true;
					newChanges.Add(change);
				}
			};

			var status = new StringBuilder();

			var preparedToReadDivergence = false;
			var preparedToReadUntracked = false;
			ProcessUtils.ExecuteCmdBlocking("git fetch", ViewModel.CurrentDirectory);
			ProcessUtils.ExecuteCmd(
				"git status",
				ViewModel.CurrentDirectory,
				(output) =>
				{
					status.AppendLine(output);

					NotARepo = false;

					if (output.StartsWith("On branch"))
					{
						Branch = output.Replace("On branch", "").Trim();
					}
					else if (output.StartsWith("Your branch is behind"))
					{
						var split = output.Split(new string[] { " by ", " commits" }, StringSplitOptions.RemoveEmptyEntries);
						var countStr = split[1];
						var count = int.Parse(countStr);

						newNumberCommitsToPull = count;
					}
					else if (output.StartsWith("Your branch is ahead of"))
					{
						var split = output.Split(new string[] { " by ", " commit" }, StringSplitOptions.RemoveEmptyEntries);
						var countStr = split[1];
						var count = int.Parse(countStr);

						newNumberCommitsToPush = count;
					}
					else if (output.Trim().StartsWith("modified:"))
					{
						var path = output.Replace("modified:", "").Trim();
						var change = new Change(path, ChangeType.MODIFIED, ViewModel.GitCommit);

						addChange(change);
					}
					else if (output.Trim().StartsWith("added:"))
					{
						var path = output.Replace("added:", "").Trim();
						var change = new Change(path, ChangeType.ADDED, ViewModel.GitCommit);

						addChange(change);
					}
					else if (output.Trim().StartsWith("deleted:"))
					{
						var path = output.Replace("deleted:", "").Trim();
						var change = new Change(path, ChangeType.DELETED, ViewModel.GitCommit);

						addChange(change);
					}
					else if (output.StartsWith("Your branch and 'origin/master' have diverged"))
					{
						preparedToReadDivergence = true;
					}
					else if (preparedToReadDivergence && output.StartsWith("and have"))
					{
						var line = output.Replace("and have ", "").Replace("different commits each, respectively.", "").Trim();
						var split = line.Split(new string[] { " and " }, StringSplitOptions.RemoveEmptyEntries);

						newNumberCommitsToPull = int.Parse(split[1]);
						newNumberCommitsToPush = int.Parse(split[0]);

						preparedToReadDivergence = false;
					}
					else if (output.StartsWith("Untracked files:"))
					{
						preparedToReadUntracked = true;
					}
					else if (preparedToReadUntracked)
					{
						try
						{
							if (File.Exists(Path.Combine(ViewModel.CurrentDirectory, output.Trim())))
							{
								var path = output.Trim();
								var change = new Change(path, ChangeType.UNTRACKED, ViewModel.GitCommit);

								addChange(change);
							}
						}
						catch (Exception ex) { }
					}
				},
				(error) =>
				{
					status.AppendLine(error);

					if (error.StartsWith("fatal: Not a git repository (or any of the parent directories)") || error.StartsWith("Force kill"))
					{
						NotARepo = true;
						Branch = "Not a Repo";
						newNumberCommitsToPull = 0;
						return;
					}

					NotARepo = false;
				},
				2000);

			if (previousChanges.Count != newChanges.Count)
			{
				changesChanged = true;
			}

			if (changesChanged)
			{
				ViewModel.GitCommit.ChangeList = newChanges.OrderBy(e => e.File).ToList();
				ViewModel.GitCommit.RaisePropertyChangedEvent(nameof(GitCommit.ChangeList));
			}

			NumberCommitsToPull = newNumberCommitsToPull;
			NumberCommitsToPush = newNumberCommitsToPush;

			checkingStatus = false;

			var statusStr = status.ToString();
			if (statusStr != lastStatus)
			{
				lastStatus = statusStr;

				ViewModel.GitLog.GetLog(ViewModel.CurrentDirectory);
			}
		}
	}
}
