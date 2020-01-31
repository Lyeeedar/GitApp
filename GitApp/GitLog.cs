using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ToastNotifications.Messages;

namespace GitApp
{
	//-----------------------------------------------------------------------
	public class Commit : NotifyPropertyChanged
	{
		public string ID { get; set; }
		public string Author { get; set; }
		public string Date { get; set; }
		public string Message { get; set; }

		public string Title { get { return Message.Split('\n')[0].Trim(); } }

		public bool IsLocal { get; set; }

		public class File : NotifyPropertyChanged
		{
			public string Name { get; set; }
			public Tuple<List<Line>, List<Line>> Contents { get; set; }
		}

		public List<File> CommitContents
		{
			get
			{
				if (m_commitContents == null)
				{
					m_commitContents = new List<File>();
					GetCommitContents();
				}

				return m_commitContents;
			}
		}
		private List<File> m_commitContents;

		public File SelectedFile
		{
			get { return m_selectedFile; }
			set
			{
				m_selectedFile = value;

				RaisePropertyChangedEvent();
			}
		}
		private File m_selectedFile;

		public ViewModel ViewModel { get; set; }

		public Commit(ViewModel viewModel)
		{
			ViewModel = viewModel;
		}

		public void GetCommitContents()
		{
			var rawContents = ProcessUtils.ExecuteCmdBlocking("git show " + ID, ViewModel.CurrentDirectory);
			var lines = rawContents.Split('\n');

			Action<string, string> processFile = (filePath, rawDiff) =>
			{
				var file = new File();
				file.Name = filePath;
				file.Contents = new Tuple<List<Line>, List<Line>>(new List<Line>(), new List<Line>());

				lock (m_commitContents)
				{
					m_commitContents.Add(file);
				}
				Task.Run(() =>
				{
					var diff = ViewModel.GitDiff.ParseDiff(rawDiff);

					Extensions.SafeBeginInvoke(() =>
					{
						lock (m_commitContents)
						{
							file.Contents = diff;

							file.RaisePropertyChangedEvent(nameof(File.Contents));
						}
					});
				});
			};

			var currentFile = "";
			var currentDiff = new StringBuilder();
			foreach (var line in lines)
			{
				if (line.StartsWith("diff --git a/"))
				{
					var file = line.Replace("diff --git a/", "").Split(' ')[0];

					if (currentFile != "")
					{
						processFile(currentFile, currentDiff.ToString());
					}

					currentDiff.Clear();
					currentFile = file;
				}
				else
				{
					currentDiff.AppendLine(line);
				}
			}

			if (currentFile != "")
			{
				processFile(currentFile, currentDiff.ToString());
			}
		}
	}

	public class GitLog : NotifyPropertyChanged
	{
		//-----------------------------------------------------------------------
		public Commit SelectedCommit
		{
			get { return m_selectedCommit; }
			set
			{
				m_selectedCommit = value;
				RaisePropertyChangedEvent();

				ViewModel.GitDiff.GetCurrentDiff(ViewModel.CurrentDirectory);
			}
		}
		private Commit m_selectedCommit;

		//-----------------------------------------------------------------------
		public List<string> CommitTypes { get; set; }

		//-----------------------------------------------------------------------
		public List<string> CommitScopes { get; set; }

		//-----------------------------------------------------------------------
		public List<Commit> Log { get; set; }

		//-----------------------------------------------------------------------
		public string UndoableLastCommit { get; set; }

		//-----------------------------------------------------------------------
		public static readonly Regex SemanticCommitRegex = new Regex(
			@"(?<Type>\w*)(\((?<Scope>.*)\))?:(?<Description>.*)",
			RegexOptions.Compiled | RegexOptions.IgnoreCase);

		//-----------------------------------------------------------------------
		private ViewModel ViewModel { get; set; }

		//-----------------------------------------------------------------------
		public GitLog(ViewModel viewModel)
		{
			ViewModel = viewModel;
		}

		//-----------------------------------------------------------------------
		public void GetLog(string CurrentDirectory)
		{
			try
			{
				var rawLog = ProcessUtils.ExecuteCmdBlocking("git log", CurrentDirectory);
				var lines = rawLog.Split('\n');

				var log = new List<Commit>();
				var commitsMap = new Dictionary<string, Commit>();

				var types = new HashSet<string>();
				var scopes = new HashSet<string>();

				Commit currentCommit = null;
				foreach (var line in lines)
				{
					if (line.StartsWith("commit "))
					{
						if (currentCommit != null)
						{
							currentCommit.Message = currentCommit.Message.Trim();
							log.Add(currentCommit);
							commitsMap[currentCommit.ID] = currentCommit;

							var matches = SemanticCommitRegex.Matches(currentCommit.Message);
							foreach (Match match in matches)
							{
								var groups = match.Groups;
								var type = groups["Type"].Value.Trim();
								var scope = groups["Scope"].Value.Trim();

								types.Add(type);
								scopes.Add(scope);
							}
						}

						currentCommit = new Commit(ViewModel);
						currentCommit.ID = line.Replace("commit", "").Trim();
					}
					else if (line.StartsWith("Author: "))
					{
						currentCommit.Author = line.Replace("Author: ", "").Trim();
					}
					else if (line.StartsWith("Date: "))
					{
						currentCommit.Date = line.Replace("Date: ", "").Trim();
					}
					else
					{
						currentCommit.Message += line + "\n";
					}
				}

				var rawUnpushedLog = ProcessUtils.ExecuteCmdBlocking("git cherry", CurrentDirectory);
				if (rawUnpushedLog.StartsWith("Could not find a tracked remote branch"))
				{
					foreach (var commit in commitsMap.Values)
					{
						commit.IsLocal = true;
					}
				}
				else
				{
					lines = rawUnpushedLog.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
					foreach (var line in lines)
					{
						commitsMap[line.Split('+')[1].Trim()].IsLocal = true;
					}
				}

				Extensions.SafeBeginInvoke(() => 
				{
					Log = log;
					RaisePropertyChangedEvent(nameof(Log));

					CommitTypes = types.OrderBy(e => e).ToList();
					RaisePropertyChangedEvent(nameof(CommitTypes));

					CommitScopes = scopes.OrderBy(e => e).ToList();
					RaisePropertyChangedEvent(nameof(CommitScopes));

					if (log.Count > 0 && log[0].IsLocal)
					{
						UndoableLastCommit = log[0].Message;
					}
					else
					{
						UndoableLastCommit = null;
					}
					RaisePropertyChangedEvent(nameof(UndoableLastCommit));
				});
			}
			catch (Exception e)
			{
				Extensions.SafeBeginInvoke(() =>
				{
					ViewModel.ToastNotifier.ShowError("Get Log failed:\n" + e.Message);
				});
			}
		}
	}
}
