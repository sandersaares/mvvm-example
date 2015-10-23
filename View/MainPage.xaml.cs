namespace DevApp.View
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using Windows.Storage;
	using Windows.Storage.Pickers;
	using Windows.UI.Xaml;
	using Windows.UI.Xaml.Controls;
	using Windows.UI.Xaml.Navigation;
	using Axinom.Toolkit;
	using Viewmodel;

	public sealed partial class MainPage : Page
	{
		private MainPageVm Viewmodel => (MainPageVm)DataContext;

		public MainPage()
		{
			this.InitializeComponent();
		}

		protected override void OnNavigatedTo(NavigationEventArgs e)
		{
			DataContext = (MainPageVm)e.Parameter;

			foreach (var item in Viewmodel.CatalogItems)
				item.RequestPlaybackPage += OnRequestPlayback;

			base.OnNavigatedTo(e);
		}

		protected override void OnNavigatedFrom(NavigationEventArgs e)
		{
			foreach (var item in Viewmodel.CatalogItems)
				item.RequestPlaybackPage -= OnRequestPlayback;

			base.OnNavigatedFrom(e);
		}

		private void OnRequestPlayback(object source, PlaybackPageVm e)
		{
			_log.Debug($"Playback requested: {e.MediaUrl}");

			Frame.Navigate(typeof(PlaybackPage), e);
		}

		private static readonly LogSource _log = Log.Default.CreateChildSource(nameof(MainPage));

		private async void OnSaveLogClick(object sender, RoutedEventArgs e)
		{
			await SaveLogFile(App.Current.LogFilePath, "ApplicationLog");
		}

		private async void OnSaveBackgroundLogClick(object sender, RoutedEventArgs e)
		{
			var logFilePath = Path.Combine(ApplicationData.Current.TemporaryFolder.Path, Constants.BackgroundTaskLogFilename);

			await SaveLogFile(logFilePath, "BackgroundTaskLog");
		}

		private async Task SaveLogFile(string sourcePath, string suggestedFilename)
		{
			var picker = new FileSavePicker
			{
				SuggestedFileName = suggestedFilename,
				SuggestedStartLocation = PickerLocationId.DocumentsLibrary
			};
			picker.FileTypeChoices.Add("Plain Text", new[] { ".txt" });

			var file = await picker.PickSaveFileAsync();

			if (file != null)
			{
				byte[] bytes;

				if (!File.Exists(sourcePath))
				{
					bytes = new byte[0];
				}
				else
				{
					// This way, we can read the file at the same time as we are logging to it.
					using (var fs = File.Open(sourcePath, FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite))
					using (var reader = new BinaryReader(fs))
						bytes = reader.ReadBytesAndVerify((int)fs.Length);
				}

				await FileIO.WriteBytesAsync(file, bytes);
			}
		}
	}
}