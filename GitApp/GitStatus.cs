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
			// Check if we are already doing stuff
			var windowActive = true;//Application.Current?.MainWindow?.IsActive ?? false;
			if (ViewModel.GitPush.PushInProgress || ViewModel.GitPull.PullInProgress || !windowActive || ProcessUtils.OperationInProgress || checkingStatus)
			{
				return;
			}
			checkingStatus = true;

			// find out what submodules we have
			var submodules = new List<string>();
			var submoduleLines = ProcessUtils.ExecuteCmdBlocking("git submodule status", ViewModel.CurrentDirectory).Split('\n');
			foreach (var submodule in submoduleLines)
			{
				if (!string.IsNullOrWhiteSpace(submodule))
				{
					var split = submodule.Trim().Split(' ')[1];
					submodules.Add(Path.Combine(ViewModel.CurrentDirectory, split));
				}
			}

			// store old versions to check if we changed
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

			// status runner func
			string currentBranchName = null;
			var status = new StringBuilder();
			Action<string, bool> doStatus = (dir, isSubmodule) =>
			{
				var preparedToReadDivergence = false;
				var preparedToReadUntracked = false;
				ProcessUtils.ExecuteCmdBlocking("git fetch", dir);
				ProcessUtils.ExecuteCmd(
					"git status",
					dir,
					(output) =>
					{
						status.AppendLine(output);

						NotARepo = false;

						if (output.StartsWith("On branch"))
						{
							if (!isSubmodule)
							{
								currentBranchName = output.Replace("On branch", "").Trim();
							}
						}
						else if (output.StartsWith("Your branch is behind"))
						{
							var split = output.Split(new string[] { " by ", " commit" }, StringSplitOptions.RemoveEmptyEntries);
							var countStr = split[1];
							var count = int.Parse(countStr);

							newNumberCommitsToPull += count;
						}
						else if (output.StartsWith("Your branch is ahead of"))
						{
							var split = output.Split(new string[] { " by ", " commit" }, StringSplitOptions.RemoveEmptyEntries);
							var countStr = split[1];
							var count = int.Parse(countStr);

							newNumberCommitsToPush += count;
						}
						else if (output.Trim().StartsWith("modified:"))
						{
							var path = output.Replace("modified:", "").Trim();
							var change = new Change(path, ChangeType.MODIFIED, ViewModel.GitCommit);
							if (isSubmodule)
							{
								change.Submodule = dir;
							}

							addChange(change);
						}
						else if (output.Trim().StartsWith("added:"))
						{
							var path = output.Replace("added:", "").Trim();
							var change = new Change(path, ChangeType.ADDED, ViewModel.GitCommit);
							if (isSubmodule)
							{
								change.Submodule = dir;
							}

							addChange(change);
						}
						else if (output.Trim().StartsWith("deleted:"))
						{
							var path = output.Replace("deleted:", "").Trim();
							var change = new Change(path, ChangeType.DELETED, ViewModel.GitCommit);
							if (isSubmodule)
							{
								change.Submodule = dir;
							}

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

							newNumberCommitsToPull += int.Parse(split[1]);
							newNumberCommitsToPush += int.Parse(split[0]);

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
								if (File.Exists(Path.Combine(dir, output.Trim())))
								{
									var path = output.Trim();
									var change = new Change(path, ChangeType.UNTRACKED, ViewModel.GitCommit);
									if (isSubmodule)
									{
										change.Submodule = dir;
									}

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
							currentBranchName = "Not a Repo";
							newNumberCommitsToPull = 0;
							return;
						}

						NotARepo = false;
					},
					2000);
			};

			// run the func on root and all submodules
			doStatus(ViewModel.CurrentDirectory, false);
			foreach (var submodule in submodules)
			{
				doStatus(submodule, true);
			}

			// find all branches
			var branchesLines = ProcessUtils.ExecuteCmdBlocking("git branch -a --sort=-committerdate", ViewModel.CurrentDirectory).Split('\n');
			var branches = new List<Branch>();
			var branchMap = new Dictionary<string, Branch>();

			var master = new Branch(ViewModel, "master");
			branches.Add(master);
			branchMap[master.Name] = master;

			foreach (var branchLineRaw in branchesLines.Select(e => e.Trim()))
			{
				var branchLine = branchLineRaw;

				if (branchLine.StartsWith("remotes/origin/HEAD"))
				{
					continue;
				}

				var isCurrent = branchLine.StartsWith("*");
				if (isCurrent)
				{
					branchLine = branchLine.Replace("*", "").Trim();
				}

				var name = branchLine.Replace("remotes/origin/", "");
				var isRemote = branchLine.StartsWith("remotes/origin/");

				if (string.IsNullOrWhiteSpace(name))
				{
					continue;
				}

				if (name == currentBranchName)
				{
					isCurrent = true;
				}

				if (branchMap.TryGetValue(name, out var existing))
				{
					existing.IsRemote = existing.IsRemote || isRemote;
					existing.IsCurrentBranch = isCurrent;
				}
				else
				{
					var branch = new Branch(ViewModel, name);
					branch.IsRemote = isRemote;
					branch.IsCurrentBranch = isCurrent;

					branches.Add(branch);
					branchMap[name] = branch;
				}
			}

			var currentBranch = branches.FirstOrDefault(e => e.IsCurrentBranch) ?? branches[0];

			foreach (var branch in branches)
			{
				if (!branch.IsCurrentBranch)
				{
					branch.ExtraCommits = ProcessUtils.ExecuteCmdBlocking("git cherry " + currentBranchName + " " + branch.Name, ViewModel.CurrentDirectory).Split('\n').Length;
					branch.MissingCommits = ProcessUtils.ExecuteCmdBlocking("git cherry " + branch.Name + " " + currentBranchName, ViewModel.CurrentDirectory).Split('\n').Length;
				}
			}

			var areBranchesDifferent = false;
			if (Branches.Count != branches.Count)
			{
				areBranchesDifferent = true;

			}
			else
			{
				for (int i = 0; i < branches.Count; i++)
				{
					if (
						branches[i].Name != Branches[i].Name ||
						branches[i].IsRemote != Branches[i].IsRemote ||
						branches[i].ExtraCommits != Branches[i].ExtraCommits ||
						branches[i].MissingCommits != Branches[i].MissingCommits ||
						branches[i].IsCurrentBranch != Branches[i].IsCurrentBranch)
					{
						areBranchesDifferent = true;
						break;
					}
				}
			}

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

				var statusStr = status.ToString();
				if (statusStr != lastStatus)
				{
					lastStatus = statusStr;

					Task.Run(() => { ViewModel.GitLog.GetLog(ViewModel.CurrentDirectory); });
				}
			});
		}

		//-----------------------------------------------------------------------
		public void CreateBranch()
		{
			
		}
	}
}
