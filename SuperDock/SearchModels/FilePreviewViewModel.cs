using System.IO;
using System.Windows.Media.Imaging;
using SuperDock.Search;

namespace SuperDock.SearchModels;

/// <summary>右侧预览面板的显示状态；图片在 UI 线程构造并 Freeze 后交给 Image 控件。</summary>
public sealed class FilePreviewViewModel : ObservableObject
{
    private string _title = string.Empty;
    private string _meta = string.Empty;
    private string _text = string.Empty;
    private string _hint = "选择一个结果查看预览";
    private BitmapImage? _image;

    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    public string Meta
    {
        get => _meta;
        private set => SetProperty(ref _meta, value);
    }

    public string Text
    {
        get => _text;
        private set
        {
            if (SetProperty(ref _text, value))
            {
                OnPropertyChanged(nameof(HasText));
            }
        }
    }

    public string Hint
    {
        get => _hint;
        private set
        {
            if (SetProperty(ref _hint, value))
            {
                OnPropertyChanged(nameof(HasHint));
            }
        }
    }

    public BitmapImage? Image
    {
        get => _image;
        private set
        {
            if (SetProperty(ref _image, value))
            {
                OnPropertyChanged(nameof(HasImage));
            }
        }
    }

    public bool HasImage => _image is not null;

    public bool HasText => !string.IsNullOrEmpty(_text);

    public bool HasHint => !string.IsNullOrEmpty(_hint);

    public void Apply(string fileName, FilePreviewResult result)
    {
        Title = fileName;
        Meta = result.Meta;
        Text = result.Text;
        Hint = result.Hint;
        Image = CreateImage(result.ImageData);
    }

    public void Clear()
    {
        Title = string.Empty;
        Meta = string.Empty;
        Text = string.Empty;
        Hint = "选择一个结果查看预览";
        Image = null;
    }

    private static BitmapImage? CreateImage(byte[]? data)
    {
        if (data is null || data.Length == 0)
        {
            return null;
        }

        try
        {
            var bitmap = new BitmapImage();
            using (var stream = new MemoryStream(data))
            {
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.DecodePixelWidth = 720;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
            }

            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            // 损坏或不受支持的图片格式，降级为文字提示
            return null;
        }
    }
}
