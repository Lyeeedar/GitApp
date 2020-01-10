using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Xml.Serialization;
using ToastNotifications;
using ToastNotifications.Lifetime;
using ToastNotifications.Messages;
using ToastNotifications.Position;
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
		public string CurrentDirectory
		{
			get { return m_currentDirectory; }
			set
			{
				m_currentDirectory = value;
				RaisePropertyChangedEvent();

				ProjectName = Path.GetFileName(CurrentDirectory);

				GitStatus.CheckStatus();
				GitLog.GetLog(m_currentDirectory);

				RecentProjects.Remove(m_currentDirectory);
				RecentProjects.Insert(0, m_currentDirectory);

				StoreSetting("RecentProjects", RecentProjects);
				RaisePropertyChangedEvent(nameof(RecentProjects));
			}
		}
		private string m_currentDirectory;

		//-----------------------------------------------------------------------
		public List<string> RecentProjects { get; set; } = new List<string>();

		//-----------------------------------------------------------------------
		public string SettingsPath = Path.GetFullPath("GitAppSettings.xml");
		public SerializableDictionary<string, string> Settings { get; set; }

		//-----------------------------------------------------------------------
		public Command<string> ChangeDirectoryCMD { get { return new Command<string>((obj) => { ChangeDirectory(obj); }); } }

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
		public int SelectedTab
		{
			get { return m_selectedTab; }
			set
			{
				m_selectedTab = value;
				RaisePropertyChangedEvent();

				RaisePropertyChangedEvent(nameof(ShowChangesDiff));
			}
		}
		private int m_selectedTab;

		//-----------------------------------------------------------------------
		public bool ShowChangesDiff
		{
			get
			{
				return SelectedTab == 0;
			}
		}

		//-----------------------------------------------------------------------
		public Notifier ToastNotifier { get; set; }

		//-----------------------------------------------------------------------
		public GitLog GitLog { get; }

		//-----------------------------------------------------------------------
		public GitPush GitPush { get; }

		//-----------------------------------------------------------------------
		public GitPull GitPull { get; }

		//-----------------------------------------------------------------------
		public GitCommit GitCommit { get; }

		//-----------------------------------------------------------------------
		public GitDiff GitDiff { get; }

		//-----------------------------------------------------------------------
		public GitStatus GitStatus { get; }

		//-----------------------------------------------------------------------
		public ViewModel()
		{
			ToastNotifier = new Notifier(cfg =>
			{
				cfg.PositionProvider = new WindowPositionProvider(
					parentWindow: Application.Current.MainWindow,
					corner: Corner.BottomRight,
					offsetX: 10,
					offsetY: 10);

				cfg.LifetimeSupervisor = new TimeAndCountBasedLifetimeSupervisor(
					notificationLifetime: TimeSpan.FromSeconds(15),
					maximumNotificationCount: MaximumNotificationCount.FromCount(5));

				cfg.Dispatcher = Application.Current.Dispatcher;
			});

			GitLog = new GitLog(this);
			GitPush = new GitPush(this);
			GitPull = new GitPull(this);
			GitCommit = new GitCommit(this);
			GitDiff = new GitDiff(this);
			GitStatus = new GitStatus(this);

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
			RecentProjects = 
				GetSetting<List<string>>("RecentProjects")?
				.Select(e => Path.GetFullPath(e))
				.Where(e => Directory.Exists(e))
				.ToList()
				?? 
				new List<string>();
		}

		//-----------------------------------------------------------------------
		public string ExecuteLoggedCommand(string cmd)
		{
			Extensions.SafeBeginInvoke(() =>
			{
				CMDLines.Add(new Line(cmd, Brushes.LimeGreen));
			});

			var success = "";
			var failed = "";
			ProcessUtils.ExecuteCmd(
					cmd,
					CurrentDirectory,
					(output) =>
					{
						Extensions.SafeBeginInvoke(() =>
						{
							CMDLines.Add(new Line(output, Brushes.White));
							success += output;
						});
					},
					(error) =>
					{
						Extensions.SafeBeginInvoke(() =>
						{
							CMDLines.Add(new Line(error, Brushes.Red));
							failed += error;
						});
					},
					null);

			if (!string.IsNullOrWhiteSpace(failed))
			{
				throw new Exception(failed);
			}

			return success;
		}

		//-----------------------------------------------------------------------
		public void ChangeDirectory(string dir)
		{
			if (dir == null)
			{
				var dlgRoot = new WPFFolderBrowserDialog();
				dlgRoot.ShowPlacesList = true;
				dlgRoot.Title = "Git Project";
				bool? resultRoot = dlgRoot.ShowDialog();

				if (resultRoot == true)
				{
					dir = dlgRoot.FileName;
				}
			}

			if (dir != null)
			{
				ProcessUtils.ExecuteCmd(
					"git rev-parse --show-toplevel",
					dir,
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
						Extensions.SafeBeginInvoke(() =>
						{
							CMDLines.Add(new Line(output, Brushes.White));
						});
					},
					(error) =>
					{
						Extensions.SafeBeginInvoke(() =>
						{
							CMDLines.Add(new Line(error, Brushes.Red));
						});
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
