using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
				ViewModel.ExecuteLoggedCommand("git rebase " + branch);
			}
		}
	}
}
