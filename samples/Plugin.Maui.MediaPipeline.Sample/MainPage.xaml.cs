using Plugin.Maui.MediaPipeline;

namespace Plugin.Maui.MediaPipeline.Sample;

public partial class MainPage : ContentPage
{
    readonly IMediaPipeline _pipeline;

    public MainPage(IMediaPipeline pipeline)
    {
        InitializeComponent();
        _pipeline = pipeline;
        _pipeline.Progress += (_, e) => MainThread.BeginInvokeOnMainThread(() =>
            StatusLabel.Text = $"{e.Stage}… {e.Progress:P0}");
    }

    async void OnBundledClicked(object? sender, EventArgs e) =>
        await RunAsync("Bundled", await BundledBuilderAsync(encrypt: false));

    async void OnBundledEncryptClicked(object? sender, EventArgs e) =>
        await RunAsync("Bundled+encrypt", await BundledBuilderAsync(encrypt: true));

    async void OnBundledUploadClicked(object? sender, EventArgs e)
    {
        try
        {
            var builder = ApplySteps(await BundledBuilderAsync(encrypt: false));
            var result = await builder.UploadAsync(async (_, _) =>
                new MediaUploadResult
                {
                    RemoteUrl = "https://example.invalid/mock-upload",
                    StatusCode = 201,
                    SessionId = "mock-session"
                });
            StatusLabel.Text =
                $"Uploaded mock {result.Width}×{result.Height}  {result.ByteCount:N0} bytes" +
                $"{Environment.NewLine}{result.Upload?.RemoteUrl}";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Upload failed: {ex.Message}";
        }
    }

    async void OnCameraClicked(object? sender, EventArgs e) =>
        await RunAsync("Camera", _pipeline.FromCamera());

    async void OnGalleryClicked(object? sender, EventArgs e) =>
        await RunAsync("Gallery", _pipeline.FromGallery());

    async Task<IMediaPipelineBuilder> BundledBuilderAsync(bool encrypt)
    {
        await using var stream = await FileSystem.OpenAppPackageFileAsync("sample-photo.png");
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory);
        EncryptCheck.IsChecked = encrypt;
        return _pipeline.FromBytes(memory.ToArray(), "sample-photo.png");
    }

    IMediaPipelineBuilder ApplySteps(IMediaPipelineBuilder builder)
    {
        if (ResizeCheck.IsChecked)
        {
            builder = builder.Resize(1920);
        }

        if (CompressCheck.IsChecked)
        {
            builder = builder.Compress(80);
        }

        if (ExifCheck.IsChecked)
        {
            builder = builder.RemoveExif();
        }

        if (WatermarkCheck.IsChecked && !string.IsNullOrWhiteSpace(WatermarkEntry.Text))
        {
            builder = builder.Watermark(WatermarkEntry.Text, new WatermarkOptions
            {
                Position = WatermarkPosition.BottomRight,
                Opacity = 0.7f
            });
        }

        if (BlurCheck.IsChecked)
        {
            builder = builder.BlurRegion(MediaRegion.Relative(0.08f, 0.78f, 0.36f, 0.14f), sigma: 14);
        }

        if (EncryptCheck.IsChecked)
        {
            builder = builder.Encrypt();
        }

        return builder;
    }

    async Task RunAsync(string action, IMediaPipelineBuilder builder)
    {
        try
        {
            builder = ApplySteps(builder);

            var result = await builder.SaveAsync();

            if (result.IsEncrypted)
            {
                PreviewImage.Source = null;
            }
            else
            {
                PreviewImage.Source = ImageSource.FromStream(() => new MemoryStream(result.Data));
            }

            StatusLabel.Text =
                $"{result.Width}×{result.Height}  {result.ByteCount:N0} bytes  {result.ContentType}" +
                $"{Environment.NewLine}EXIF removed: {result.ExifRemoved}  encrypted: {result.IsEncrypted}" +
                $"{Environment.NewLine}{result.FilePath}";
        }
        catch (MediaPipelineException ex)
        {
            StatusLabel.Text = $"{action} failed ({ex.Error}): {ex.Message}";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"{action} failed: {ex.Message}";
        }
    }
}
