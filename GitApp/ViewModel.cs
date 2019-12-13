using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Media;
using System.Xml.Serialization;
using WPFFolderBrowser;

namespace GitApp
{
	//-----------------------------------------------------------------------
	public class Line
	{
		public string Text { get; set; }
		public Brush Brush { get; set; }

		public Line(string text, Brush brush)
		{
			this.Text = text;
			this.Brush = brush;
		}
	}

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
		public ViewModel ViewModel { get; set; }
		public string File { get; set; }
		public ChangeType ChangeType { get; set; }
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

		public Change(string file, ChangeType changeType, ViewModel viewModel)
		{
			this.File = file;
			this.ChangeType = changeType;
			this.ViewModel = viewModel;
		}

		public string Key { get { return File + ChangeType; } }
	}

	//-----------------------------------------------------------------------
	public class ViewModel : NotifyPropertyChanged
	{
		//-----------------------------------------------------------------------
		public string ProjectName
		{
			get { return m_projectName; }
			set
			{
				m_projectName = value;
				RaisePropertyChangedEvent();
			}
		}
		private string m_projectName;

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
		public List<Change> ChangeList { get; set; } = new List<Change>();

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
		public string CurrentDirectory
		{
			get { return m_currentDirectory; }
			set
			{
				m_currentDirectory = value;
				RaisePropertyChangedEvent();

				ProjectName = Path.GetFileName(CurrentDirectory);

				CheckStatus();
			}
		}
		private string m_currentDirectory;

		//-----------------------------------------------------------------------
		public string SettingsPath = Path.GetFullPath("Settings.xml");
		public SerializableDictionary<string, string> Settings { get; set; }

		//-----------------------------------------------------------------------
		public Command<object> ChangeDirectoryCMD { get { return new Command<object>((obj) => { ChangeDirectory(); }); } }

		//-----------------------------------------------------------------------
		public Command<object> PullCMD { get { return new Command<object>((obj) => { Pull(); }); } }

		//-----------------------------------------------------------------------
		public Command<object> PushCMD { get { return new Command<object>((obj) => { Push(); }); } }

		//-----------------------------------------------------------------------
		public Command<object> CommitCMD { get { return new Command<object>((obj) => { Commit(); }); } }

		//-----------------------------------------------------------------------
		public Command<object> ClearConsoleCMD { get { return new Command<object>((obj) => { CMDLines.Clear(); }); } }

		//-----------------------------------------------------------------------
		public DeferableObservableCollection<Line> CMDLines { get; } = new DeferableObservableCollection<Line>();

		//-----------------------------------------------------------------------
		public string ArbitraryCMD
		{
			get { return m_arbitraryCMD; }
			set
			{
				m_arbitraryCMD = value;
				RaisePropertyChangedEvent();
			}
		}
		public string m_arbitraryCMD;

		//-----------------------------------------------------------------------
		public Change SelectedChange
		{
			get { return m_selectedChange; }
			set
			{
				m_selectedChange = value;
				RaisePropertyChangedEvent();

				GetCurrentDiff();
			}
		}
		private Change m_selectedChange;

		//-----------------------------------------------------------------------
		public List<Line> SelectedDiff { get; set; }

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
		public ViewModel()
		{
			if (File.Exists(SettingsPath))
			{
				using (var filestream = File.Open(SettingsPath, FileMode.Open, FileAccess.Read))
				{
					try
					{
						var serializer = new XmlSerializer(typeof(SerializableDictionary<string, string>));
						Settings = (SerializableDictionary<string, string>)serializer.Deserialize(filestream);
					}
					catch (Exception ex)
					{
						Message.Show(ex.Message + ex.ToString(), "Exception!");
						Settings = new SerializableDictionary<string, string>();
					}
				}
			}
			else
			{
				Settings = new SerializableDictionary<string, string>();
			}

			CurrentDirectory = GetSetting<string>("CurrentDirectory");

			var timer = new Timer();
			timer.Elapsed += (e, args) =>
			{
				CheckStatus();
			};
			timer.Interval = 3000;
			timer.Start();
		}

		//-----------------------------------------------------------------------
		public void ChangeDirectory()
		{
			var dlgRoot = new WPFFolderBrowserDialog();
			dlgRoot.ShowPlacesList = true;
			dlgRoot.Title = "Git Project";
			bool? resultRoot = dlgRoot.ShowDialog();

			if (resultRoot == true)
			{
				ProcessUtils.ExecuteCmd(
					"git rev-parse --show-toplevel", 
					dlgRoot.FileName, 
					(output) => 
					{
						CurrentDirectory = output;
						StoreSetting("CurrentDirectory", CurrentDirectory);
					}, 
					(error) => 
					{
						Message.Show("The selected directory is no a valid git project", "Invalid Directory");
					});
			}
		}

		//-----------------------------------------------------------------------
		public void CheckStatus()
		{
			NumberCommitsToPull = 0;
			NumberCommitsToPull = 0;

			var previousChanges = new Dictionary<string, Change>();
			foreach (var change in ChangeList)
			{
				previousChanges[change.Key] = change;
			}
			var newChanges = new List<Change>();
			var changesChanged = false;

			Action<Change> addChange = (change) => 
			{
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

			ProcessUtils.ExecuteCmd(
				"git status",
				CurrentDirectory,
				(output) =>
				{
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

						NumberCommitsToPull = count;
					}
					else if (output.StartsWith("Your branch is ahead of"))
					{
						var split = output.Split(new string[] { " by ", " commit" }, StringSplitOptions.RemoveEmptyEntries);
						var countStr = split[1];
						var count = int.Parse(countStr);

						NumberCommitsToPush = count;
					}
					else if (output.Trim().StartsWith("modified:"))
					{
						var path = output.Replace("modified:", "").Trim();
						var change = new Change(path, ChangeType.MODIFIED, this);

						addChange(change);
					}
					else if (output.Trim().StartsWith("added:"))
					{
						var path = output.Replace("added:", "").Trim();
						var change = new Change(path, ChangeType.ADDED, this);

						addChange(change);
					}
					else if (output.Trim().StartsWith("deleted:"))
					{
						var path = output.Replace("deleted:", "").Trim();
						var change = new Change(path, ChangeType.DELETED, this);

						addChange(change);
					}
				},
				(error) =>
				{
					if (error.StartsWith("fatal: Not a git repository (or any of the parent directories)") || error.StartsWith("Force kill"))
					{
						NotARepo = true;
						Branch = "Not a Repo";
						NumberCommitsToPull = 0;
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
				ChangeList = newChanges.OrderBy(e => e.File).ToList();
				RaisePropertyChangedEvent(nameof(ChangeList));
			}
		}

		//-----------------------------------------------------------------------
		public void Push()
		{
			try
			{
				ProcessUtils.ExecuteCmdBlocking("git push", CurrentDirectory);
			}
			catch (Exception ex)
			{
				Message.Show(ex.Message, "Push failed");
			}
		}

		//-----------------------------------------------------------------------
		public void Pull()
		{
			try
			{
				ProcessUtils.ExecuteCmdBlocking("git pull --rebase", CurrentDirectory);
			}
			catch (Exception ex)
			{
				Message.Show(ex.Message, "Pull failed");
			}
		}

		//-----------------------------------------------------------------------
		public void Commit()
		{
			try
			{
				ProcessUtils.ExecuteCmdBlocking("git reset HEAD -- .", CurrentDirectory);

				foreach (var change in ChangeList)
				{
					if (change.Added)
					{
						ProcessUtils.ExecuteCmdBlocking("git add " + change.File, CurrentDirectory);
					}
				}

				ProcessUtils.ExecuteCmdBlocking("git commit -m\"" + CommitMessage + "\"", CurrentDirectory);
				CommitMessage = "";
			}
			catch (Exception ex)
			{
				Message.Show(ex.Message, "Commit failed");
			}
		}

		//-----------------------------------------------------------------------
		public void GetCurrentDiff()
		{
			var rawDiff = ProcessUtils.ExecuteCmdBlocking("git diff " + SelectedChange.File, CurrentDirectory);
			var strlines = rawDiff.Split('\n');

			var lines = new List<Line>();

			var isDiff = false;
			var currentBlock = new StringBuilder();
			var currentBlockType = ' ';
			var currentBlockBrush = Brushes.Transparent;
			foreach (var strLine in strlines)
			{
				if (strLine == "") continue;

				var blockType = strLine[0];
				if (blockType != currentBlockType)
				{
					lines.Add(new Line(currentBlock.ToString(), currentBlockBrush));
					currentBlock.Clear();
					currentBlockType = blockType;
				}

				if (strLine[0] == '@')
				{
					isDiff = true;
					currentBlockBrush = Brushes.DarkGray;
				}
				else if (strLine[0] == '+')
				{
					currentBlockBrush = Brushes.DarkGreen;
				}
				else if (strLine[0] == '-')
				{
					currentBlockBrush = Brushes.DarkRed;
				}
				else
				{
					currentBlockBrush = Brushes.Transparent;
				}

				if (isDiff)
				{
					currentBlock.AppendLine(strLine);
				}
			}

			SelectedDiff = lines;
			RaisePropertyChangedEvent(nameof(SelectedDiff));
		}

		//-----------------------------------------------------------------------
		public void RunArbitraryCommand(string cmd)
		{
			CMDLines.Add(new Line("\n------------------------------------\n", Brushes.DarkGray));
			CMDLines.Add(new Line(cmd, Brushes.Green));

			Task.Run(() => 
			{
				ProcessUtils.ExecuteCmd(
					cmd,
					CurrentDirectory,
					(output) =>
					{
						Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
						{
							CMDLines.Add(new Line(output, Brushes.White));
						}));
					},
					(error) =>
					{
						Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
						{
							CMDLines.Add(new Line(error, Brushes.Red));
						}));
					},
					null);
			});
			
		}

		//-----------------------------------------------------------------------
		public void StoreSetting(string key, object value)
		{
			string valueAsString = value.SerializeObject();

			Settings[key] = valueAsString;
			Directory.CreateDirectory(System.IO.Path.GetDirectoryName(SettingsPath));

			if (!File.Exists(SettingsPath)) File.Create(SettingsPath).Close();

			using (var filestream = File.Open(SettingsPath, FileMode.Truncate, FileAccess.Write))
			{
				try
				{
					var serializer = new XmlSerializer(Settings.GetType());
					serializer.Serialize(filestream, Settings);
				}
				catch (Exception e)
				{
					Message.Show(e.Message, "Exception!");
				}
			}
		}

		//-----------------------------------------------------------------------
		public T GetSetting<T>(string key, T fallback = default(T))
		{
			if (Settings.ContainsKey(key)) return Settings[key].DeserializeObject<T>();
			else return fallback;
		}
	}
}
