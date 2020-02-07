using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
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
	public class ConflictedFile
	{
		public string Path { get; set; }
	}

	/// <summary>
	/// Interaction logic for ConflictsDialog.xaml
	/// </summary>
	public partial class ConflictsDialog : Window
	{
		public string StatusMessage { get; set; }
		public ViewModel ViewModel { get; set; }

		public DeferableObservableCollection<ConflictedFile> ConflictedFiles { get; } = new DeferableObservableCollection<ConflictedFile>();
		private Dictionary<string, ConflictedFile> fileMap = new Dictionary<string, ConflictedFile>();

		private Timer timer;

		public ConflictsDialog(ViewModel viewModel)
		{
			this.ViewModel = viewModel;

			var repo = ViewModel.GitStatus.Repo;
			StatusMessage = "Rebase: " + repo.Rebase.GetCurrentStepIndex() + " / " + repo.Rebase.GetTotalStepCount();

			DataContext = this;

			InitializeComponent();

			var timer = new Timer();
			timer.Elapsed += (e, args) =>
			{
				UpdateFiles();
			};
			timer.Interval = 3000;
			timer.Start();

			Closed += (e, args) => { timer.Stop(); timer.Dispose(); };
		}

		private void UpdateFiles()
		{
			var conflictedFilesRaw = ViewModel.GitStatus.Repo.RetrieveStatus().Where(e => e.State == FileStatus.Conflicted).ToList();

			var newFiles = new List<ConflictedFile>();
			foreach (var conflictedFileRaw in conflictedFilesRaw)
			{
				ConflictedFile file;
				if (!fileMap.TryGetValue(conflictedFileRaw.FilePath, out file))
				{
					file = new ConflictedFile();
					file.Path = conflictedFileRaw.FilePath;
				}
				newFiles.Add(file);
			}

			ConflictedFiles.ReplaceWith(newFiles, true);
		}

		private void AbortButton_Click(object sender, RoutedEventArgs e)
		{
			timer.Stop();
			timer.Dispose();
			DialogResult = false;
			Close();
		}

		private void ContinueButton_Click(object sender, RoutedEventArgs e)
		{
			timer.Stop();
			timer.Dispose();
			DialogResult = false;
			Close();
		}
	}
}
