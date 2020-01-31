using System;
using System.Collections.Generic;
using System.Linq;
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
	/// <summary>
	/// Interaction logic for BranchPopup.xaml
	/// </summary>
	public partial class BranchPopup : Window
	{
		public string BranchName { get; set; }
		public ViewModel ViewModel { get; set; }

		public BranchPopup(ViewModel viewModel)
		{
			this.ViewModel = viewModel;
			DataContext = this;

			InitializeComponent();
		}

		private void CreateButton_Click(object sender, RoutedEventArgs e)
		{
			if (!string.IsNullOrWhiteSpace(BranchName))
			{
				ViewModel.ExecuteLoggedCommand("git branch " + BranchName);
			}

			Close();
		}

		private void CancelButton_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}
	}
}
