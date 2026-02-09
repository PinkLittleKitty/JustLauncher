using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using JustLauncher.Utils;

namespace JustLauncher.Controls;

public partial class FaceTracker : UserControl
{
    private Control? _headContainer;
    private SkewTransform? _faceSkew;
    private TranslateTransform? _faceTranslate;
    private AsyncImage? _faceImage;

    public static readonly StyledProperty<string?> UsernameProperty =
        AvaloniaProperty.Register<FaceTracker, string?>(nameof(Username));

    public string? Username
    {
        get => GetValue(UsernameProperty);
        set => SetValue(UsernameProperty, value);
    }

    static FaceTracker()
    {
        UsernameProperty.Changed.AddClassHandler<FaceTracker>((x, e) => x.OnUsernameChanged(e));
    }

    private void OnUsernameChanged(AvaloniaPropertyChangedEventArgs e)
    {
        UpdateActiveUserFace();
    }

    public static event Action? ActiveAccountChanged;

    public static void NotifyAccountChanged() => ActiveAccountChanged?.Invoke();

    public FaceTracker()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        _headContainer = this.FindControl<Control>("HeadContainer");
        _faceImage = this.FindControl<AsyncImage>("FaceImage");

        if (_headContainer != null)
        {
            var group = _headContainer.RenderTransform as TransformGroup;
            if (group != null && group.Children.Count >= 2)
            {
                _faceSkew = group.Children[0] as SkewTransform;
                _faceTranslate = group.Children[1] as TranslateTransform;
            }
        }

        UpdateActiveUserFace();
    }

    private void UpdateActiveUserFace()
    {
        string username = Username;
        
        if (string.IsNullOrEmpty(username))
        {
            var accounts = ConfigManager.LoadAccounts();
            var active = accounts.Accounts.FirstOrDefault(a => a.IsActive) ?? 
                         accounts.Accounts.FirstOrDefault(a => a.Id == accounts.SelectedAccountId) ?? 
                         accounts.Accounts.FirstOrDefault();
            
            username = active?.Username ?? "Steve";
        }
        
        if (_faceImage != null)
        {
            _faceImage.SourceUrl = $"https://minotar.net/avatar/{username}/64";
            _faceImage.SourceRect = default;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        var window = VisualRoot as Window;
        if (window != null)
        {
            window.PointerMoved += Window_PointerMoved;
        }
        
        ActiveAccountChanged += UpdateActiveUserFace;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        var window = VisualRoot as Window;
        if (window != null)
        {
            window.PointerMoved -= Window_PointerMoved;
        }
        
        ActiveAccountChanged -= UpdateActiveUserFace;
    }

    private void Window_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_headContainer == null || _faceSkew == null || _faceTranslate == null) return;

        var window = VisualRoot as Window;
        if (window == null) return;

        var pointerPos = e.GetPosition(window);
        var windowCenter = new Point(window.Bounds.Width / 2, window.Bounds.Height / 2);

        double dx = pointerPos.X - windowCenter.X;
        double dy = pointerPos.Y - windowCenter.Y;

        double maxDist = Math.Max(window.Bounds.Width, window.Bounds.Height) / 2;
        double pullX = Math.Clamp(dx / maxDist, -1.0, 1.0);
        double pullY = Math.Clamp(dy / maxDist, -1.0, 1.0);

        _faceSkew.AngleX = -pullY * 15;
        _faceSkew.AngleY = pullX * 15;
        
        _faceTranslate.X = pullX * 8;
        _faceTranslate.Y = pullY * 8;
    }
}
