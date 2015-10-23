namespace DevApp
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using Windows.ApplicationModel;
	using Windows.ApplicationModel.Activation;
	using Windows.Storage;
	using Windows.UI.Core;
	using Windows.UI.Xaml;
	using Windows.UI.Xaml.Controls;
	using Windows.UI.Xaml.Navigation;
	using Axinom.Toolkit;
	using Model;
	using View;
	using Viewmodel;

	sealed partial class App : Application
	{
		public new static App Current => (App)Application.Current;

		public string LogFilePath => Path.Combine(ApplicationData.Current.TemporaryFolder.Path, Constants.AppLogFilename);

		public AppSettings Settings { get; }
		public TransientMediaStorageSession MediaStorageSession { get; private set; }
		public ContentCatalog ContentCatalog { get; private set; }

		/// <summary>
		/// PlayReady operations are avoided if this is false, since the first PlayReady operations may be expensive
		/// and we want to be sure we initialize (and, if necessary, activate) PlayReady on a background thread first.
		/// </summary>
		public bool PlayReadyInitialized { get; private set; }

		public App()
		{
			this.InitializeComponent();

			// Note that the constructor does not run on the UI thread.

#if DEBUG
			Log.Default.RegisterListener(new DebugLogListener());
#endif

			// AutoFlush means it can be a bit slow, unfortunately... do not use like this in real production scenario.
			var logFileStream = File.Open(LogFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
			Log.Default.RegisterListener(new StreamWriterLogListener(new StreamWriter(logFileStream, Encoding.UTF8)
			{
				AutoFlush = true
			}));

			Settings = new AppSettings();

			_log.Debug(Helpers.Debug.ToDebugString(Settings));

			Suspending += OnSuspending;
			Resuming += OnResuming;
			UnhandledException += OnUnhandledException;
		}

		protected override async void OnLaunched(LaunchActivatedEventArgs e)
		{
			// This method runs on UI thread, so starting from here we can work with UI-thread-related things.

			_log.Debug("Launched. {0}", Helpers.Debug.ToDebugString(e));

			// If the app was already running, we skip just about all of this and get to the UI immediately.
			if (e.PreviousExecutionState != ApplicationExecutionState.Running)
			{
				// The startup lifetime seems pretty complicated in UWP.
				// Unclear when this can happen; let's just throw if it does, to find out!
				if (MediaStorageSession != null)
					throw new Exception("WTF just happened? Tell Sander when you encounter this situation and describe how it happened, please!");

				MediaStorageSession = new TransientMediaStorageSession();
				ContentCatalog = new ContentCatalog(MediaStorageSession);

				EnsureRootFrame();
				UpdateBackButtonVisibility();

				// Always go to main page on launch, to keep the implementation of this sample app very simple.
				_rootFrame.Navigate(typeof(MainPage), new MainPageVm(ContentCatalog));
			}

			_log.Debug("Displaying UI now.");

			// This displays our window to the user and thus we want to get here ASAP.
			Window.Current.Activate();

			if (e.PreviousExecutionState != ApplicationExecutionState.Running)
			{
				_log.Debug("Executing post-launch initialization.");

				var uiThreadTaskScheduler = TaskScheduler.FromCurrentSynchronizationContext();

				// Kick off some initialization on a background thread, to not hog the UI thread so much.
				Helpers.Async.BackgroundThreadInvoke(async delegate
				{
					// Note that this may not immediately terminate the task if it is running!
					// Resources may remain locked for some seconds, still, and we need to account for this.
					BackgroundTaskManager.UnregisterAndTerminateBackgroundDownloadTask();

					try
					{
						// Activate PlayReady if required.
						await Helpers.PlayReady.EnsureActivatedAsync().IgnoreContext();
					}
					catch (Exception ex)
					{
						_log.Error("PlayReady activation startup logic failed: " + ex.Message);
					}

					// Now we allow access to PlayReady. This may have deferred some license status refreshes already.
					PlayReadyInitialized = true;

					// Kick off an update of the license acquisition statuses, now that PlayReady is known to be activated.
					// We need to do this on the UI thread, so the changed events are raised on the UI thread.
					Task.Factory.StartNew(async delegate { await ContentCatalog.RefreshLicenseStatusesAsync(); }, CancellationToken.None, TaskCreationOptions.None, uiThreadTaskScheduler).Forget();
				});

				// This will acquire the storage session soon, waiting for the background task to finish if it is running.
				// This must be done on the UI thread, to ensure that the MediaAgents also get created on the UI thread.
				await MediaStorageSession.StartAcquireAsync();
			}

			_log.Debug("Post-launch logic complete.");
		}

		private void OnSuspending(object sender, SuspendingEventArgs e)
		{
			_log.Debug("Suspending.");

			MediaStorageSession.Release();

			// Before the app closes, register the download task, so downloads can continue even in background.
			// The background downloads should begin 15 minutes after this call.
			BackgroundTaskManager.RegisterBackgroundDownloadTask();
		}

		private async void OnResuming(object sender, object e)
		{
			_log.Debug("Resuming.");

			await MediaStorageSession.StartAcquireAsync();

			// Kick off some initialization on a background thread, to not hog the UI thread so much.
			// Note that this may not immediately terminate the task if it is running!
			// Resources may remain locked for some seconds, still, and we need to account for this.
			Helpers.Async.BackgroundThreadInvoke(BackgroundTaskManager.UnregisterAndTerminateBackgroundDownloadTask);
		}

		private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
		}

		#region Navigation
		private Frame _rootFrame;

		private void WireUpBackButtonEventHandler()
		{
			SystemNavigationManager.GetForCurrentView().BackRequested += (s, ee) =>
			{
				if (_rootFrame.CanGoBack)
				{
					ee.Handled = true;
					_rootFrame.GoBack();
				}
			};
		}

		private void EnsureRootFrame()
		{
			_rootFrame = Window.Current.Content as Frame;

			if (_rootFrame == null)
			{
				_rootFrame = new Frame();
				_rootFrame.Navigated += OnRootFrameNavigated;

				Window.Current.Content = _rootFrame;

				WireUpBackButtonEventHandler();
			}
		}

		private void OnRootFrameNavigated(object sender, NavigationEventArgs e)
		{
			UpdateBackButtonVisibility();
		}

		private void UpdateBackButtonVisibility()
		{
			var navigationManager = SystemNavigationManager.GetForCurrentView();

			if (_rootFrame.CanGoBack)
				navigationManager.AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;
			else
				navigationManager.AppViewBackButtonVisibility = AppViewBackButtonVisibility.Collapsed;
		}
		#endregion

		private static readonly LogSource _log = Log.Default.CreateChildSource(nameof(App));
	}
}