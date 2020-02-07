using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace GitApp
{
	public class GitMerge : NotifyPropertyChanged
	{
		//-----------------------------------------------------------------------
		public Command<string> CherryPickCMD { get { return new Command<string>((branch) => { CherryPick(branch); }); } }

		//-----------------------------------------------------------------------
		public Command<string> RebaseCMD { get { return new Command<string>((branch) => { Rebase(branch); }); } }

		//-----------------------------------------------------------------------
		public ViewModel ViewModel { get; set; }

		//-----------------------------------------------------------------------
		public GitMerge(ViewModel viewModel)
		{
			this.ViewModel = viewModel;
		}

		//-----------------------------------------------------------------------
		public void CherryPick(string branch)
		{
			// show multiselect for commits
			var selector = new CommitSelectorPopup(branch, CommitSelectorPopup.MergeOperation.CherryPick, ViewModel);

			// run git cherrypick for each
			if (selector.ShowDialog() == true)
			{
				foreach (var commit in selector.Commits.Where(e => e.Selected))
				{
					ViewModel.ExecuteLoggedCommand("git cherry-pick " + commit.Hash);
				}
			}
		}

		//-----------------------------------------------------------------------
		public void Rebase(string branch)
		{
			var selector = new CommitSelectorPopup(branch, CommitSelectorPopup.MergeOperation.Rebase, ViewModel);

			if (selector.ShowDialog() == true)
			{
				var hasUncommittedChanges = ViewModel.GitCommit.ChangeList.Any(e => e.ChangeType != ChangeType.UNTRACKED);
				if (hasUncommittedChanges)
				{
					ViewModel.ExecuteLoggedCommand("git stash");
				}

				var repo = ViewModel.GitStatus.Repo;

				var config = repo.Config;
				var author = config.BuildSignature(DateTimeOffset.Now);
				
				var sourceBranch = repo.Branches.First(e => e.FriendlyName == branch);

				var identity = new LibGit2Sharp.Identity(author.Name, author.Email);
				var options = new LibGit2Sharp.RebaseOptions();

				var status = repo.Rebase.Start(sourceBranch, sourceBranch.TrackedBranch, repo.Head, identity, options);

				while (true)
				{
					if (status.Status == LibGit2Sharp.RebaseStatus.Complete)
					{
						ViewModel.CMDLines.Add(new Line("Rebase complete", Brushes.Green));
						break;
					}
					else if (status.Status == LibGit2Sharp.RebaseStatus.Conflicts)
					{
						var dialog = new ConflictsDialog(ViewModel);
						var result = dialog.ShowDialog();

						if (result == false)
						{
							repo.Rebase.Abort();
							break;
						}
					}
					else if (status.Status == LibGit2Sharp.RebaseStatus.Stop)
					{
						var dialog = new ConflictsDialog(ViewModel);
						var result = dialog.ShowDialog();

						if (result == false)
						{
							repo.Rebase.Abort();
							break;
						}
					}

					repo.Rebase.Continue(identity, options);
				}

				if (hasUncommittedChanges)
				{
					ViewModel.ExecuteLoggedCommand("git stash pop");
				}
			}
		}
	}
}
