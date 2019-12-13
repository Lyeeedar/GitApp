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
	public class Change
	{
		public string File { get; set; }
		public ChangeType ChangeType { get; set; }

		public Change(string file, ChangeType changeType)
		{
			this.File = file;
			this.ChangeType = changeType;
		}
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
			}
		}
		private int m_numberCommitsToPull;

		//-----------------------------------------------------------------------
		public DeferableObservableCollection<Change> ChangeList { get; } = new DeferableObservableCollection<Change>();

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
		}

		//-----------------------------------------------------------------------
		public void Push()
		{
			ProcessUtils.ExecuteCmdBlocking("git push", CurrentDirectory);
		}

		//-----------------------------------------------------------------------
		public void Pull()
		{
			ProcessUtils.ExecuteCmdBlocking("git pull --rebase", CurrentDirectory);
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
