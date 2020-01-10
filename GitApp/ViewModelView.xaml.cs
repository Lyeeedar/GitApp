﻿using System;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace GitApp
{
	/// <summary>
	/// Interaction logic for ViewModelView.xaml
	/// </summary>
	public partial class ViewModelView : UserControl
	{
		//-----------------------------------------------------------------------
		public ViewModel GitViewModel { get; set; } = new ViewModel();

		//-----------------------------------------------------------------------
		public ViewModelView()
		{
			InitializeComponent();

			DataContext = GitViewModel;
		}

		//-----------------------------------------------------------------------
		private void PullClick(object sender, MouseButtonEventArgs e)
		{
			GitViewModel.Pull();
		}

		//-----------------------------------------------------------------------
		private void PushClick(object sender, MouseButtonEventArgs e)
		{
			GitViewModel.Push();
		}

		//-----------------------------------------------------------------------
		private void ArbitraryCMDKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Enter)
			{
				if (!string.IsNullOrWhiteSpace(GitViewModel.ArbitraryCMD))
				{
					GitViewModel.RunArbitraryCommand(GitViewModel.ArbitraryCMD);
					GitViewModel.ArbitraryCMD = "";
				}
			}
		}
	}
}