using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace GitApp
{
	//-----------------------------------------------------------------------
	public class Branch
	{
		//-----------------------------------------------------------------------
		public string Name { get; set; }

		//-----------------------------------------------------------------------
		public bool IsRemote { get; set; }

		//-----------------------------------------------------------------------
		public bool IsCurrentBranch { get; set; }

		//-----------------------------------------------------------------------
		public int ExtraCommits { get; set; }

		//-----------------------------------------------------------------------
		public int MissingCommits { get; set; }

		//-----------------------------------------------------------------------
		public string DifferenceMessage
		{
			get
			{
				var message = "";

				if (ExtraCommits > 0)
				{
					message += "+" + ExtraCommits + " ";
				}

				if (MissingCommits > 0)
				{
					message += "-" + MissingCommits + " ";
				}

				return message;
			}
		}

		//-----------------------------------------------------------------------
		public HashSet<string> Commits { get; set; }

		//-----------------------------------------------------------------------
		public Command<object> SwitchToBranchCMD { get { return new Command<object>((obj) => { SwitchToBranch(); }); } }

		//-----------------------------------------------------------------------
		public ViewModel ViewModel { get; }

		//-----------------------------------------------------------------------
		public Branch(ViewModel viewModel, string name)
		{
			this.ViewModel = viewModel;
			this.Name = name;
		}

		//-----------------------------------------------------------------------
		public void SwitchToBranch()
		{
			ViewModel.ExecuteLoggedCommand("git checkout " + Name);
		}
	}

	public class GitStatus : NotifyPropertyChanged
	{
		//-----------------------------------------------------------------------
		public Command<string> CreateBranchCMD { get { return new Command<string>((obj) => { CreateBranch(); }); } }

		//-----------------------------------------------------------------------
		public Branch Branch
		{
			get { return m_branch; }
			set
			{
				m_branch = value;
				RaisePropertyChangedEvent();
			}
		}
		private Branch m_branch;

		//-----------------------------------------------------------------------
		public DeferableObservableCollection<Branch> Branches { get; } = new DeferableObservableCollection<Branch>();

		//-----------------------------------------------------------------------
		public List<string> Submodules { get; } = new List<string>();

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
		public string PullMessage
		{
			get { return m_pullMessage; }
			set
			{
				m_pullMessage = value;
				RaisePropertyChangedEvent();
			}
		}
		private string m_pullMessage;

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
		public Repository Repo { get; set; }
		private string RepoPath { get; set; }

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
		public void ClearStatus()
		{
			NumberCommitsToPull = 0;
			NumberCommitsToPush = 0;
			Submodules.Clear();
			Branches.Clear();
			Branch = null;
		}

		//-----------------------------------------------------------------------
		bool checkingStatus = false;
		string lastStatus = null;
		public void CheckStatus()
		{
			// Check if we are already doing stuff
			var windowActive = true;//Application.Current?.MainWindow?.IsActive ?? false;
			if (ViewModel.GitPush.PushInProgress || ViewModel.GitPull.PullInProgress || !windowActive || ProcessUtils.OperationInProgress || checkingStatus)
			{
				return;
			}
			checkingStatus = true;

			var startTime = DateTime.Now;

			if (RepoPath != ViewModel.CurrentDirectory)
			{
				Repo?.Dispose();
				Repo = new Repository(ViewModel.CurrentDirectory);
				RepoPath = ViewModel.CurrentDirectory;
			}

			var submodules = Repo.Submodules.Select(e => e.Path);

			// store old versions to check if we changed
			var newNumberCommitsToPush = Repo.Head.TrackingDetails.AheadBy ?? 0;
			var newNumberCommitsToPull = Repo.Head.TrackingDetails.BehindBy ?? 0;

			var previousChanges = new Dictionary<string, Change>();
			foreach (var change in ViewModel.GitCommit.ChangeList)
			{
				previousChanges[change.Key] = change;
			}
			var newChanges = new List<Change>();
			var addedChanges = new HashSet<string>();
			var changesChanged = false;

			// method to add a change and filter duplicates
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

			foreach (var file in Repo.RetrieveStatus())
			{
				if (file.State == FileStatus.DeletedFromWorkdir || file.State == FileStatus.DeletedFromIndex)
				{
					addChange(new Change(file.FilePath, ChangeType.DELETED, ViewModel.GitCommit));
				}
				else if (file.State == FileStatus.NewInIndex)
				{
					addChange(new Change(file.FilePath, ChangeType.ADDED, ViewModel.GitCommit));
				}
				else if (
					file.State == FileStatus.ModifiedInWorkdir || file.State == FileStatus.ModifiedInIndex ||
					file.State == FileStatus.RenamedInIndex || file.State == FileStatus.RenamedInWorkdir)
				{
					addChange(new Change(file.FilePath, ChangeType.MODIFIED, ViewModel.GitCommit));
				}
				else if (file.State == FileStatus.NewInWorkdir)
				{
					addChange(new Change(file.FilePath, ChangeType.UNTRACKED, ViewModel.GitCommit));
				}
			}

			// status runner func
			var branches = new List<Branch>();
			Branch currentBranch = null;
			foreach (var repoBranch in Repo.Branches)
			{
				var branch = new Branch(ViewModel, repoBranch.FriendlyName);
				branch.IsCurrentBranch = repoBranch.IsCurrentRepositoryHead;
				branch.IsRemote = repoBranch.IsTracking;
				branch.Commits = new HashSet<string>(repoBranch.Commits.Select(e => e.Sha));
				branches.Add(branch);

				if (branch.IsCurrentBranch)
				{
					currentBranch = branch;
				}
			}

			foreach (var branch in branches)
			{
				if (branch.Name != Repo.Head.FriendlyName)
				{
					branch.ExtraCommits = branch.Commits.Where(e => !currentBranch.Commits.Contains(e)).Count();
					branch.MissingCommits = currentBranch.Commits.Where(e => !branch.Commits.Contains(e)).Count();
				}
			}

			var areBranchesDifferent = branches.Count != Branches.Count;

			// find all branches

			Extensions.SafeBeginInvoke(() =>
			{
				if (areBranchesDifferent)
				{
					Branches.Clear();
					Branches.AddRange(branches);

					Branch = currentBranch;

					if (Branch != null)
					{
						Branches.Remove(Branch);
						Branches.Insert(0, Branch);
					}
				}

				// if we had changes, notify stuff
				if (previousChanges.Count != newChanges.Count)
				{
					changesChanged = true;
				}

				if (!Enumerable.SequenceEqual(submodules, Submodules))
				{
					Submodules.Clear();
					Submodules.AddRange(submodules);
					RaisePropertyChangedEvent(nameof(Submodules));
				}

				if (changesChanged)
				{
					ViewModel.GitCommit.ChangeList = newChanges.OrderBy(e => e.File).ToList();
					ViewModel.GitCommit.RaisePropertyChangedEvent(nameof(GitCommit.ChangeList));
				}

				// update counts
				NumberCommitsToPull = newNumberCommitsToPull;
				NumberCommitsToPush = newNumberCommitsToPush;

				if (NumberCommitsToPull > 0)
				{
					var pullMessage = "";
					pullMessage += ProcessUtils.ExecuteCmdBlocking("git log --pretty=%B ..origin/master", ViewModel.CurrentDirectory);
					pullMessage += "\n";
					pullMessage += ProcessUtils.ExecuteCmdBlocking("git submodule foreach git log --pretty=%B ..origin/master", ViewModel.CurrentDirectory);
					pullMessage = string.Join("\n", pullMessage.Split('\n').Where(e => !string.IsNullOrWhiteSpace(e) && !e.StartsWith("Entering")));

					PullMessage = pullMessage;
				}

				checkingStatus = false;

				var statusStr = Repo.Head.Tip.Sha;
				if (statusStr != lastStatus)
				{
					lastStatus = statusStr;

					Task.Run(() => { ViewModel.GitLog.GetLog(ViewModel.CurrentDirectory); });
				}
			});

			var endTime = DateTime.Now;
			var diff = endTime - startTime;
			Console.WriteLine("Status complete in " + diff.TotalSeconds + " seconds");
		}

		//-----------------------------------------------------------------------
		public void CreateBranch()
		{
			
		}
	}
}
