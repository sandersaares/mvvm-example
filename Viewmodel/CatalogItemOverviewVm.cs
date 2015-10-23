namespace DevApp.Viewmodel
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Linq;
	using System.Windows.Input;
	using Windows.UI.Xaml;
	using Windows.UI.Xaml.Controls;
	using Axinom.DownloadManager;
	using Axinom.Toolkit;
	using Model;
	using View;

	public sealed class CatalogItemOverviewVm : INotifyPropertyChanged
	{
		public string Title { get; }

		#region bool DownloadFunctionalityAvailable (read-only)
		/// <summary>
		/// If false then DownloadState and any other properties related to downloading are meaningless.
		/// </summary>
		public bool DownloadFunctionalityAvailable
		{
			get { return _downloadFunctionalityAvailable; }
			private set
			{
				if (_downloadFunctionalityAvailable == value)
					return;

				_downloadFunctionalityAvailable = value;
				RaisePropertyChanged(nameof(DownloadFunctionalityAvailable));
			}
		}

		private bool _downloadFunctionalityAvailable;
		#endregion

		#region MediaAgentState DownloadState (read-only)
		public MediaAgentState DownloadState
		{
			get { return _downloadState; }
			private set
			{
				if (_downloadState == value)
					return;

				_downloadState = value;
				RaisePropertyChanged(nameof(DownloadState));

				UpdateVisibilities();
			}
		}

		private MediaAgentState _downloadState;
		#endregion

		#region LicenseStatus LicenseStatus (read-only)
		public LicenseStatus LicenseStatus
		{
			get { return _licenseStatus; }
			private set
			{
				if (_licenseStatus == value)
					return;

				_licenseStatus = value;
				RaisePropertyChanged(nameof(LicenseStatus));
			}
		}

		private LicenseStatus _licenseStatus;
		#endregion

		#region double DownloadProgress (read-only)
		public double DownloadProgress
		{
			get { return _downloadProgress; }
			private set
			{
				if (_downloadProgress == value)
					return;

				_downloadProgress = value;
				RaisePropertyChanged(nameof(DownloadProgress));
			}
		}

		private double _downloadProgress;
		#endregion

		#region Visibility StartDownloadButtonVisibility (read-only)
		public Visibility StartDownloadButtonVisibility
		{
			get { return _startDownloadButtonVisibility; }
			private set
			{
				if (_startDownloadButtonVisibility == value)
					return;

				_startDownloadButtonVisibility = value;
				RaisePropertyChanged(nameof(StartDownloadButtonVisibility));
			}
		}

		private Visibility _startDownloadButtonVisibility;
		#endregion

		#region Visibility ResumeButtonVisibility (read-only)
		public Visibility ResumeButtonVisibility
		{
			get { return _resumeButtonVisibility; }
			private set
			{
				if (_resumeButtonVisibility == value)
					return;

				_resumeButtonVisibility = value;
				RaisePropertyChanged(nameof(ResumeButtonVisibility));
			}
		}

		private Visibility _resumeButtonVisibility;
		#endregion

		#region Visibility PauseDownloadButtonVisibility (read-only)
		public Visibility PauseDownloadButtonVisibility
		{
			get { return _pauseDownloadButtonVisibility; }
			private set
			{
				if (_pauseDownloadButtonVisibility == value)
					return;

				_pauseDownloadButtonVisibility = value;
				RaisePropertyChanged(nameof(PauseDownloadButtonVisibility));
			}
		}

		private Visibility _pauseDownloadButtonVisibility;
		#endregion

		#region Visibility DeleteDataButtonVisibility (read-only)
		public Visibility DeleteDataButtonVisibility
		{
			get { return _deleteDataButtonVisibility; }
			private set
			{
				if (_deleteDataButtonVisibility == value)
					return;

				_deleteDataButtonVisibility = value;
				RaisePropertyChanged(nameof(DeleteDataButtonVisibility));
			}
		}

		private Visibility _deleteDataButtonVisibility;
		#endregion

		#region Visibility DownloadProgressVisibility (read-only)
		public Visibility DownloadProgressVisibility
		{
			get { return _downloadProgressVisibility; }
			private set
			{
				if (_downloadProgressVisibility == value)
					return;

				_downloadProgressVisibility = value;
				RaisePropertyChanged(nameof(DownloadProgressVisibility));
			}
		}

		private Visibility _downloadProgressVisibility;
		#endregion

		#region double EstimatedSizeInGigabytes (read-only)
		public double EstimatedSizeInGigabytes
		{
			get { return _estimatedSizeInGigabytes; }
			private set
			{
				if (_estimatedSizeInGigabytes == value)
					return;

				_estimatedSizeInGigabytes = value;
				RaisePropertyChanged(nameof(EstimatedSizeInGigabytes));
			}
		}

		private double _estimatedSizeInGigabytes;
		#endregion

		#region Visibility EstimatedSizeVisibility (read-only)
		public Visibility EstimatedSizeVisibility
		{
			get { return _estimatedSizeVisibility; }
			private set
			{
				if (_estimatedSizeVisibility == value)
					return;

				_estimatedSizeVisibility = value;
				RaisePropertyChanged(nameof(EstimatedSizeVisibility));
			}
		}

		private Visibility _estimatedSizeVisibility;
		#endregion

		public ICommand StartDownload { get; }
		public ICommand ResumeDownload { get; }
		public ICommand DeleteData { get; }
		public ICommand PauseDownload { get; }

		public ICommand PlayCommand { get; }

		/// <summary>
		/// Raised when the item requests the app to navigate to the playback page, using the provided viewmodel.
		/// </summary>
		public event EventHandler<PlaybackPageVm> RequestPlaybackPage;

		public CatalogItemOverviewVm(CatalogItem item)
		{
			Helpers.Argument.ValidateIsNotNull(item, nameof(item));

			_item = item;
			Title = item.FeedItem.Title;

			UpdateCatalogItemLicenseStatus();

			var itemListener = new WeakEventListener<CatalogItemOverviewVm, object, PropertyChangedEventArgs>(this);
			itemListener.OnEventAction = (instance, source, args) => instance.OnCatalogItemPropertyChanged(source, args);
			itemListener.OnDetachAction = (wel) => _item.PropertyChanged -= wel.OnEvent;
			_item.PropertyChanged += itemListener.OnEvent;

			StartDownload = new DelegateCommand
			{
				Execute = async delegate
				{
					// Save it here because _item.MediaAgent will become null if the storage is closed during this method.
					var mediaAgent = _item.MediaAgent;

					try
					{
						// We will deliberately not away this; the download will start on a background thread.
						var loadTask = mediaAgent.GetAvailableRepresentationsAsync();

						var vm = new SelectRepresentationsDialogVm(loadTask);
						var dialog = new SelectRepresentationsDialog(vm);

						// After this we may get kicked off the UI thread, for extra performance.
						var result = await dialog.ShowAsync().IgnoreContext();

						if (result == ContentDialogResult.Primary)
						{
							var selected = vm.SelectedRepresentations.Select(r => r.Representation).ToArray();
							await mediaAgent.StartDownloadAsync(selected);

							// If auto-resume is on, we pause immediately and let auto-resume logic
							// handle the queueing of any downloads, one by one, when it wants to.
							if (App.Current.Settings.IsAutoResumeEnabled)
								await mediaAgent.PauseAsync();
						}
					}
					catch (ObjectDisposedException)
					{
						// This could be sort of normal if the media agent is disposed during the process.
						// For example, it could happen because the application was suspended in the middle of this.
						_log.Error("Failed to start download because media agent has been disposed of.");
					}
				},
				CanExecute = x => DownloadFunctionalityAvailable
			};
			ResumeDownload = new DelegateCommand
			{
				Execute = async delegate
				{
					// A manual pause/resume command disables auto-resume.
					App.Current.Settings.IsAutoResumeEnabled = false;

					await _item.MediaAgent.ResumeAsync();
				},
				CanExecute = x => DownloadFunctionalityAvailable
			};
			PauseDownload = new DelegateCommand
			{
				Execute = async delegate
				{
					// A manual pause/resume command disables auto-resume.
					App.Current.Settings.IsAutoResumeEnabled = false;

					await _item.MediaAgent.PauseAsync();
				},
				CanExecute = x => DownloadFunctionalityAvailable
			};
			DeleteData = new DelegateCommand
			{
				Execute = async delegate { await _item.MediaAgent.ResetAsync(); },
				CanExecute = x => DownloadFunctionalityAvailable
			};
			PlayCommand = new DelegateCommand
			{
				Execute = delegate
				{
					var request = RequestPlaybackPage;
					request?.Invoke(this, new PlaybackPageVm(_item));
				}
			};

			if (_item.MediaAgent != null)
			{
				UpdateMediaAgentState();
				UpdateMediaAgentFields();
				ConnectToMediaAgent();
			}

			DownloadFunctionalityAvailable = _item.MediaAgent != null;
			UpdateVisibilities();
		}

		private void OnCatalogItemPropertyChanged(object source, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(CatalogItem.LicenseStatus))
			{
				UpdateCatalogItemLicenseStatus();
			}
			else if (e.PropertyName == nameof(CatalogItem.MediaAgent))
			{
				DownloadFunctionalityAvailable = _item.MediaAgent != null;

				UpdateVisibilities();
				RaiseCommandCanExecuteChanged();

				if (_item.MediaAgent == null)
				{
					DisconnectFromMediaAgent();
				}
				else
				{
					UpdateMediaAgentState();
					UpdateMediaAgentFields();
					ConnectToMediaAgent();
				}
			}
		}

		private readonly CatalogItem _item;

		private WeakEventListener<CatalogItemOverviewVm, object, PropertyChangedEventArgs> _mediaAgentListener;

		private void ConnectToMediaAgent()
		{
			var mediaAgent = _item.MediaAgent;

			_mediaAgentListener = new WeakEventListener<CatalogItemOverviewVm, object, PropertyChangedEventArgs>(this);
			_mediaAgentListener.OnEventAction = (instance, source, args) => instance.OnMediaAgentPropertyChanged(source, args);
			_mediaAgentListener.OnDetachAction = (wel) => mediaAgent.PropertyChanged -= wel.OnEvent;
			mediaAgent.PropertyChanged += _mediaAgentListener.OnEvent;
		}

		private void DisconnectFromMediaAgent()
		{
			if (_mediaAgentListener == null)
				return;

			_mediaAgentListener.Detach();
			_mediaAgentListener = null;
		}

		private void UpdateCatalogItemLicenseStatus()
		{
			LicenseStatus = _item.LicenseStatus;
		}

		private void OnMediaAgentPropertyChanged(object source, PropertyChangedEventArgs e)
		{
			// This will be called on the same thread that we were originally loaded on (UI thread), so it's all okay.

			if (e.PropertyName == nameof(MediaAgent.State))
			{
				UpdateMediaAgentState();
			}
			else if (e.PropertyName == nameof(MediaAgent.DownloadProgress)
			         || e.PropertyName == nameof(MediaAgent.EstimatedSize))
			{
				UpdateMediaAgentFields();
			}
		}

		private void UpdateMediaAgentState()
		{
			DownloadState = _item.MediaAgent.State;
			UpdateVisibilities();
		}

		private void UpdateMediaAgentFields()
		{
			DownloadProgress = _item.MediaAgent.DownloadProgress;
			EstimatedSizeInGigabytes = _item.MediaAgent.EstimatedSize.GetValueOrDefault() / 1024.0 / 1024.0 / 1024.0;
		}

		private static readonly MediaAgentState[] _startDownloadButtonVisibleInStates =
		{
			MediaAgentState.NotDownloading,
		};

		private static readonly MediaAgentState[] _resumeButtonVisibleInStates =
		{
			MediaAgentState.Paused
		};

		private static readonly MediaAgentState[] _pauseDownloadButtonVisibleInStates =
		{
			MediaAgentState.Downloading,
			MediaAgentState.TemporaryFailure,
			MediaAgentState.NetworkUnavailable,
			MediaAgentState.NotEnoughFreeSpace
		};

		private static readonly MediaAgentState[] _deleteDataButtonVisibleInStates =
		{
			MediaAgentState.Downloaded,
			MediaAgentState.Downloading,
			MediaAgentState.TemporaryFailure,
			MediaAgentState.Paused,
			MediaAgentState.NetworkUnavailable,
			MediaAgentState.NotEnoughFreeSpace
		};

		private static readonly MediaAgentState[] _downloadProgressVisibleInStates =
		{
			MediaAgentState.Downloading,
			MediaAgentState.TemporaryFailure,
			MediaAgentState.Paused,
			MediaAgentState.NetworkUnavailable,
			MediaAgentState.NotEnoughFreeSpace
		};

		private void UpdateVisibilities()
		{
			if (DownloadFunctionalityAvailable)
			{
				StartDownloadButtonVisibility = _startDownloadButtonVisibleInStates.Contains(DownloadState) ? Visibility.Visible : Visibility.Collapsed;
				ResumeButtonVisibility = _resumeButtonVisibleInStates.Contains(DownloadState) ? Visibility.Visible : Visibility.Collapsed;
				PauseDownloadButtonVisibility = _pauseDownloadButtonVisibleInStates.Contains(DownloadState) ? Visibility.Visible : Visibility.Collapsed;
				DeleteDataButtonVisibility = _deleteDataButtonVisibleInStates.Contains(DownloadState) ? Visibility.Visible : Visibility.Collapsed;
				DownloadProgressVisibility = _downloadProgressVisibleInStates.Contains(DownloadState) ? Visibility.Visible : Visibility.Collapsed;

				EstimatedSizeVisibility = DownloadState != MediaAgentState.NotDownloading ? Visibility.Visible : Visibility.Collapsed;
			}
			else
			{
				StartDownloadButtonVisibility = Visibility.Collapsed;
				ResumeButtonVisibility = Visibility.Collapsed;
				PauseDownloadButtonVisibility = Visibility.Collapsed;
				DeleteDataButtonVisibility = Visibility.Collapsed;
				DownloadProgressVisibility = Visibility.Collapsed;
				EstimatedSizeVisibility = Visibility.Collapsed;
			}
		}

		private void RaiseCommandCanExecuteChanged()
		{
			((DelegateCommand)StartDownload).RaiseCanExecuteChanged();
			((DelegateCommand)ResumeDownload).RaiseCanExecuteChanged();
			((DelegateCommand)DeleteData).RaiseCanExecuteChanged();
			((DelegateCommand)PauseDownload).RaiseCanExecuteChanged();
		}

		#region INotifyPropertyChanged implementation
		public event PropertyChangedEventHandler PropertyChanged;

		private void RaisePropertyChanged(string propertyName)
		{
			var eventHandler = PropertyChanged;
			eventHandler?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
		#endregion

		private static readonly LogSource _log = Log.Default.CreateChildSource(nameof(CatalogItemOverviewVm));
	}
}