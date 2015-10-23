namespace DevApp.Viewmodel
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Linq;
	using System.Windows.Input;
	using Axinom.Toolkit;
	using Model;

	public sealed class MainPageVm : INotifyPropertyChanged
	{
		public ICollection<CatalogItemOverviewVm> CatalogItems => _catalogItems;

		#region bool? IsAutoResumeEnabled (read-only)
		public bool? IsAutoResumeEnabled
		{
			get { return _isAutoResumeEnabled; }
			private set
			{
				if (_isAutoResumeEnabled == value)
					return;

				_isAutoResumeEnabled = value;
				RaisePropertyChanged(nameof(IsAutoResumeEnabled));
			}
		}

		private bool? _isAutoResumeEnabled;
		#endregion

		#region bool? IsBackgroundDownloadEnabled (read-only)
		public bool? IsBackgroundDownloadEnabled
		{
			get { return _isBackgroundDownloadEnabled; }
			private set
			{
				if (_isBackgroundDownloadEnabled == value)
					return;

				_isBackgroundDownloadEnabled = value;
				RaisePropertyChanged(nameof(IsBackgroundDownloadEnabled));
			}
		}

		private bool? _isBackgroundDownloadEnabled;
		#endregion

		public ICommand ToggleAutoResume { get; }
		public ICommand ToggleBackgroundDownload { get; }

		public MainPageVm(ContentCatalog catalog)
		{
			Helpers.Argument.ValidateIsNotNull(catalog, nameof(catalog));

			ToggleAutoResume = new DelegateCommand
			{
				Execute = delegate { App.Current.Settings.IsAutoResumeEnabled = !App.Current.Settings.IsAutoResumeEnabled; }
			};
			ToggleBackgroundDownload = new DelegateCommand
			{
				Execute = async delegate { await BackgroundTaskManager.ToggleIsBackgroundDownloadEnabled(); }
			};

			UpdateSettings();

			#region WeakEventListener: App.Current.Settings.PropertyChanged += OnSettingsChanged
			var settingsListener = new WeakEventListener<MainPageVm, object, PropertyChangedEventArgs>(this);
			settingsListener.OnEventAction = (instance, source, args) => instance.OnSettingsChanged(source, args);
			settingsListener.OnDetachAction = (wel) => App.Current.Settings.PropertyChanged -= wel.OnEvent;
			App.Current.Settings.PropertyChanged += settingsListener.OnEvent;
			#endregion

			_catalogItems = catalog.Items.Select(i => new CatalogItemOverviewVm(i)).ToArray();
		}

		private readonly CatalogItemOverviewVm[] _catalogItems;

		private void OnSettingsChanged(object source, PropertyChangedEventArgs e)
		{
			UpdateSettings();
		}

		private void UpdateSettings()
		{
			IsAutoResumeEnabled = App.Current.Settings.IsAutoResumeEnabled;
			IsBackgroundDownloadEnabled = App.Current.Settings.IsBackgroundDownloadEnabled;
		}

		#region INotifyPropertyChanged implementation
		public event PropertyChangedEventHandler PropertyChanged;

		private void RaisePropertyChanged(string propertyName)
		{
			var eventHandler = PropertyChanged;
			eventHandler?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
		#endregion
	}
}