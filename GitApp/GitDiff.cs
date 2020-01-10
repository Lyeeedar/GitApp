using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace GitApp
{
	public class GitDiff : NotifyPropertyChanged
	{
		//-----------------------------------------------------------------------
		private static SolidColorBrush RemovedBrush = new SolidColorBrush(Color.FromArgb(50, 255, 50, 50));
		private static SolidColorBrush AddedBrush = new SolidColorBrush(Color.FromArgb(50, 50, 255, 50));
		private static SolidColorBrush ModifiedBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 50));
		private static SolidColorBrush GreyBrush = new SolidColorBrush(Color.FromArgb(50, 150, 150, 150));
		private static SolidColorBrush DarkGreyBrush = new SolidColorBrush(Color.FromArgb(50, 100, 100, 100));

		//-----------------------------------------------------------------------
		public Tuple<List<Line>, List<Line>> SelectedDiff { get; set; }

		//-----------------------------------------------------------------------
		enum DiffType
		{
			BEFORE,
			AFTER,
			BOTH
		}

		//-----------------------------------------------------------------------
		public class DiffBlock
		{
			public List<string> Lines { get; } = new List<string>();
			public Brush Brush { get; set; }
		}

		//-----------------------------------------------------------------------
		public ViewModel ViewModel { get; }

		//-----------------------------------------------------------------------
		public GitDiff(ViewModel viewModel)
		{
			ViewModel = viewModel;
		}

		//-----------------------------------------------------------------------
		public void GetCurrentDiff(string CurrentDirectory)
		{
			if (ViewModel.GitCommit.SelectedChange == null)
			{
				SelectedDiff = new Tuple<List<Line>, List<Line>>(new List<Line>(), new List<Line>());
				RaisePropertyChangedEvent(nameof(SelectedDiff));
				return;
			}

			var rawDiff = ProcessUtils.ExecuteCmdBlocking("git diff \"" + ViewModel.GitCommit.SelectedChange.File + "\"", CurrentDirectory);

			SelectedDiff = ParseDiff(rawDiff);
			RaisePropertyChangedEvent(nameof(SelectedDiff));
		}

		//-----------------------------------------------------------------------
		public Tuple<List<Line>, List<Line>> ParseDiff(string rawDiff)
		{
			var strlines = rawDiff.Split('\n');

			var blocks = new List<DiffBlock>();

			var isDiff = false;
			DiffBlock currentBlock = new DiffBlock();
			var currentBlockType = ' ';
			var currentBlockBrush = Brushes.Transparent;
			foreach (var strLine in strlines)
			{
				if (strLine == "") continue;

				var blockType = strLine[0];
				if (blockType != currentBlockType)
				{
					currentBlock.Brush = currentBlockBrush;
					blocks.Add(currentBlock);
					currentBlockType = blockType;

					currentBlock = new DiffBlock();
				}

				if (strLine[0] == '@')
				{
					isDiff = true;
					currentBlockBrush = GreyBrush;
				}
				else if (strLine[0] == '+')
				{
					currentBlockBrush = ModifiedBrush;
				}
				else if (strLine[0] == '-')
				{
					currentBlockBrush = RemovedBrush;
				}
				else
				{
					currentBlockBrush = Brushes.Transparent;
				}

				if (isDiff)
				{
					currentBlock.Lines.Add(strLine.Trim());
				}
			}

			var beforelines = new List<Line>();
			var afterlines = new List<Line>();

			for (int i = 0; i < blocks.Count; i++)
			{
				var prevBlock = i > 0 ? blocks[i - 1] : null;
				var block = blocks[i];
				var nextBlock = i < blocks.Count - 1 ? blocks[i + 1] : null;

				if (prevBlock != null && prevBlock.Brush == RemovedBrush && block.Brush == AddedBrush)
				{
					foreach (var line in prevBlock.Lines)
					{
						beforelines.Add(new Line(line, ModifiedBrush));
					}

					foreach (var line in block.Lines)
					{
						afterlines.Add(new Line(line, ModifiedBrush));
					}

					while (beforelines.Count < afterlines.Count)
					{
						beforelines.Add(new Line("", DarkGreyBrush));
					}

					while (afterlines.Count < beforelines.Count)
					{
						afterlines.Add(new Line("", DarkGreyBrush));
					}
				}
				else if (block.Brush == RemovedBrush)
				{
					if (nextBlock == null || nextBlock.Brush != AddedBrush)
					{
						foreach (var line in block.Lines)
						{
							beforelines.Add(new Line(line, block.Brush));
							afterlines.Add(new Line("", block.Brush));
						}
					}
				}
				else if (block.Brush == AddedBrush)
				{
					foreach (var line in block.Lines)
					{
						beforelines.Add(new Line("", block.Brush));
						afterlines.Add(new Line(line, block.Brush));
					}
				}
				else
				{
					foreach (var line in block.Lines)
					{
						beforelines.Add(new Line(line, block.Brush));
						afterlines.Add(new Line(line, block.Brush));
					}
				}
			}

			return new Tuple<List<Line>, List<Line>>(beforelines, afterlines);
		}
	}
}
