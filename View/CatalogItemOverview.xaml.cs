namespace DevApp.View
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Windows.UI.Xaml.Controls;
	using Viewmodel;

	public sealed partial class CatalogItemOverview : UserControl
	{
		private CatalogItemOverviewVm Viewmodel => (CatalogItemOverviewVm)DataContext;

		public CatalogItemOverview()
		{
			this.InitializeComponent();

			DataContextChanged += delegate { Bindings.Update(); };
		}
	}
}