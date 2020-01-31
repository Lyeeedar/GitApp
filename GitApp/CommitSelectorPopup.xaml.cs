using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace GitApp
{
	//-----------------------------------------------------------------------
	public class MergeCommit
	{
		public string Hash { get; set; }
		public string Message { get; set; }
		public bool Selected { get; set; }
	}

	//-----------------------------------------------------------------------
	public partial class CommitSelectorPopup : Window, INotifyPropertyChanged
	{
		//-----------------------------------------------------------------------
		public enum MergeOperation
		{
			CherryPick,
			Rebase
		}

		//-----------------------------------------------------------------------
		public bool CanSelect { get { return Operation == MergeOperation.CherryPick; } }

		//-----------------------------------------------------------------------
		public string Branch { get; set; }

		//-----------------------------------------------------------------------
		public MergeOperation Operation { get; set; }

		//-----------------------------------------------------------------------
		public List<MergeCommit> Commits { get; set; }

		//-----------------------------------------------------------------------
		public ViewModel ViewModel { get; set; }

		//-----------------------------------------------------------------------
		public CommitSelectorPopup(string branch, MergeOperation operation, ViewModel viewModel)
		{
			this.Branch = branch;
			this.Operation = operation;
			this.ViewModel = viewModel;

			DataContext = this;
			InitializeComponent();

			UpdateCommits();
		}

		//-----------------------------------------------------------------------
		public void UpdateCommits()
		{
			var commits = new List<MergeCommit>();

			var missingCommits = ProcessUtils.ExecuteCmdBlocking("git cherry " + ViewModel.GitStatus.Branch.Name + " " + Branch, ViewModel.CurrentDirectory)
				.Split('\n')
				.Where(e => !string.IsNullOrWhiteSpace(e))
				.Select(e => e.Substring(1))
				.ToList();

			foreach (var commitHash in missingCommits)
			{
				var message = ProcessUtils.ExecuteCmdBlocking("git log --format=%B -n 1 " + commitHash, ViewModel.CurrentDirectory);
				var commit = new MergeCommit();
				commit.Hash = commitHash.Trim();
				commit.Message = message.Trim();

				commits.Add(commit);
			}
					   
			Commits = commits;
			RaisePropertyChangedEvent(nameof(Commits));
		}

		//-----------------------------------------------------------------------
		private void CancelButton_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}

		//-----------------------------------------------------------------------
		private void PerformButton_Click(object sender, RoutedEventArgs e)
		{
			DialogResult = true;
			Close();
		}

		//--------------------------------------------------------------------------
		public event PropertyChangedEventHandler PropertyChanged;

		//-----------------------------------------------------------------------
		public void RaisePropertyChangedEvent
		(
			[CallerMemberName] string i_propertyName = ""
		)
		{
			if (PropertyChanged != null)
			{
				PropertyChanged(this, new PropertyChangedEventArgs(i_propertyName));
			}
		}
	}
}
