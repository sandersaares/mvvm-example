namespace DevApp.Model
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using Axinom.DownloadManager;
	using Axinom.Toolkit;

	public sealed class CatalogItem : INotifyPropertyChanged
	{
		public ContentFeedItem FeedItem { get; }

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

		#region MediaAgent MediaAgent (read-only)
		/// <summary>
		/// May be null if the media agent is temporarily not available (e.g. during initial moments after
		/// startup when we might still be loading the Download Manager data and acquiring media storage).
		/// </summary>
		public MediaAgent MediaAgent
		{
			get { return _mediaAgent; }
			private set
			{
				if (_mediaAgent == value)
					return;

				_mediaAgent = value;
				RaisePropertyChanged(nameof(MediaAgent));
			}
		}

		private MediaAgent _mediaAgent;
		#endregion

		// Assumes that the media storage session is currently in the released state.
		public CatalogItem(ContentFeedItem feedItem, TransientMediaStorageSession mediaStorageSession)
		{
			Helpers.Argument.ValidateIsNotNull(feedItem, nameof(feedItem));
			Helpers.Argument.ValidateIsNotNull(mediaStorageSession, nameof(mediaStorageSession));

			FeedItem = feedItem;

			var mediaAgentCancellation = new CancellationTokenSource();

			mediaStorageSession.Acquired += async delegate
			{
				// Acquire the token before the async call, to make a local copy (CTS will be reset after each release).
				var cancel = mediaAgentCancellation.Token;
				MediaAgent = await MediaAgent.AcquireAsync(FeedItem.MediaUrl, mediaStorageSession.MediaStorage);

				// Session got released before we managed to actually use it.
				if (cancel.IsCancellationRequested)
					return;

				MediaAgent.PropertyChanged += OnMediaAgentPropertyChanged;

				RefreshLicenseStatus();
			};
			mediaStorageSession.Releasing += delegate
			{
				// Cancel any pending AcquireAsync() by this.
				mediaAgentCancellation.Cancel();
				mediaAgentCancellation.Dispose();
				mediaAgentCancellation = new CancellationTokenSource();

				if (MediaAgent != null)
				{
					MediaAgent.PropertyChanged -= OnMediaAgentPropertyChanged;
					MediaAgent.Dispose();
					MediaAgent = null;
				}
			};
		}

		private async void OnMediaAgentPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			// License status also depends on the media agent state.
			if (e.PropertyName == nameof(MediaAgent.State))
			{
				RefreshLicenseStatus();
				await DownloadLicenseIfNecessaryAsync();
			}
		}

		public void RefreshLicenseStatus()
		{
			// If PlayReady has not yet been initialized by the launch logic, pretend it is clear content.
			// This is done because we want to avoid touching PlayReady until we know that it has been nicely
			// activated and initialized on a background thread, away from any potential UI slowdowns.
			if (FeedItem.KeyId == null || !App.Current.PlayReadyInitialized)
			{
				LicenseStatus = LicenseStatus.ClearContent;
				return;
			}

			bool hasLicense;

			try
			{
				hasLicense = Helpers.PlayReady.IsPersistentLicensePresent(FeedItem.KeyId.Value);
			}
			catch (Exception ex)
			{
				hasLicense = false;

				// Ignore MSPR_E_NEEDS_INDIVIDUALIZATION - that's normal on first startup.
				if (ex.HResult != unchecked((int)0x8004B822))
				{
					_log.Error("Unable to check license status: " + ex);
				}
			}

			if (hasLicense)
			{
				LicenseStatus = LicenseStatus.HasValidLicense;
			}
			else
			{
				// If we do not have a license, the status rather depends on whether we want one or not.
				// We only pre-acquire licenses if we download the content item.
				if (MediaAgent == null || MediaAgent.State == MediaAgentState.NotDownloading)
				{
					LicenseStatus = LicenseStatus.WillAcquireReactively;
				}
				else
				{
					LicenseStatus = LicenseStatus.MissingLicense;
				}
			}
		}

		public async Task DownloadLicenseIfNecessaryAsync()
		{
			if (!Helpers.Network.IsInternetAvailable())
				return;

			try
			{
				if (LicenseStatus == LicenseStatus.MissingLicense)
					await Helpers.PlayReady.AcquirePersistentLicenseAsync(FeedItem.KeyId.Value, Constants.LicenseServerUrl);

				RefreshLicenseStatus();
			}
			catch (Exception ex)
			{
				_log.Error("Unable to download license: " + ex);
			}
		}

		#region INotifyPropertyChanged implementation
		public event PropertyChangedEventHandler PropertyChanged;

		private void RaisePropertyChanged(string propertyName)
		{
			var eventHandler = PropertyChanged;
			eventHandler?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
		#endregion

		private static readonly LogSource _log = Log.Default.CreateChildSource(nameof(CatalogItem));
	}
}