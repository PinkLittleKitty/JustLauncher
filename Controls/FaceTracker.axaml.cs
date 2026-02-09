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
    private Canvas? _cubeCanvas;
    private Border? _frontFace;
    private Border? _backFace;
    private Border? _leftFace;
    private Border? _rightFace;
    private Border? _topFace;
    private Border? _bottomFace;
    private AsyncImage? _faceImage;
    private AsyncImage? _backImage;
    private AsyncImage? _leftImage;
    private AsyncImage? _rightImage;
    private AsyncImage? _topImage;
    private AsyncImage? _bottomImage;

    private double _rotationX = 0;
    private double _rotationY = 0;
    private const double RotationSpeed = 0.15;
    private const double MaxRotation = 35;

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
        _cubeCanvas = this.FindControl<Canvas>("CubeCanvas");
        _frontFace = this.FindControl<Border>("FrontFace");
        _backFace = this.FindControl<Border>("BackFace");
        _leftFace = this.FindControl<Border>("LeftFace");
        _rightFace = this.FindControl<Border>("RightFace");
        _topFace = this.FindControl<Border>("TopFace");
        _bottomFace = this.FindControl<Border>("BottomFace");
        _faceImage = this.FindControl<AsyncImage>("FaceImage");
        _backImage = this.FindControl<AsyncImage>("BackImage");
        _leftImage = this.FindControl<AsyncImage>("LeftImage");
        _rightImage = this.FindControl<AsyncImage>("RightImage");
        _topImage = this.FindControl<AsyncImage>("TopImage");
        _bottomImage = this.FindControl<AsyncImage>("BottomImage");

        UpdateActiveUserFace();
    }


    private void UpdateActiveUserFace()
    {
        string username = Username ?? "Steve";
        
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
            var accounts = ConfigManager.LoadAccounts();
            var active = accounts.Accounts.FirstOrDefault(a => a.Username == username) ??
                         accounts.Accounts.FirstOrDefault(a => a.IsActive) ?? 
                         accounts.Accounts.FirstOrDefault(a => a.Id == accounts.SelectedAccountId) ?? 
                         accounts.Accounts.FirstOrDefault();

            string skinUrl;
            
            if (active != null)
            {
                if (active.AccountType == "ElyBy")
                {
                    skinUrl = $"http://skinsystem.ely.by/skins/{active.Username}.png";
                }
                else
                {
                    skinUrl = $"https://minotar.net/skin/{active.Username}";
                }
            }
            else
            {
                skinUrl = $"https://minotar.net/skin/{username}";
            }
            
            _faceImage.SourceUrl = skinUrl;
            if (_backImage != null) _backImage.SourceUrl = skinUrl;
            if (_leftImage != null) _leftImage.SourceUrl = skinUrl;
            if (_rightImage != null) _rightImage.SourceUrl = skinUrl;
            if (_topImage != null) _topImage.SourceUrl = skinUrl;
            if (_bottomImage != null) _bottomImage.SourceUrl = skinUrl;
        
            _faceImage.SourceRect = new Rect(8, 8, 8, 8);
            
            if (_backImage != null) 
                _backImage.SourceRect = new Rect(24, 8, 8, 8);
            
            if (_leftImage != null) 
                _leftImage.SourceRect = new Rect(0, 8, 8, 8);
            
            if (_rightImage != null) 
                _rightImage.SourceRect = new Rect(16, 8, 8, 8);
            
            if (_topImage != null) 
                _topImage.SourceRect = new Rect(8, 0, 8, 8);
            
            if (_bottomImage != null) 
                _bottomImage.SourceRect = new Rect(16, 0, 8, 8);
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
        if (_cubeCanvas == null) return;

        var window = VisualRoot as Window;
        if (window == null) return;

        var headPosition = _cubeCanvas.TranslatePoint(new Point(50, 50), window);
        if (headPosition == null) return;

        var pointerPos = e.GetPosition(window);
        
        double dx = pointerPos.X - headPosition.Value.X;
        double dy = pointerPos.Y - headPosition.Value.Y;
        
        double distance = Math.Sqrt(dx * dx + dy * dy);
        double maxDist = Math.Max(window.Bounds.Width, window.Bounds.Height) / 2;
        
        double targetRotationY = Math.Clamp((dx / maxDist) * MaxRotation, -MaxRotation, MaxRotation);
        double targetRotationX = Math.Clamp(-(dy / maxDist) * MaxRotation, -MaxRotation, MaxRotation);
        
        _rotationY += (targetRotationY - _rotationY) * RotationSpeed;
        _rotationX += (targetRotationX - _rotationX) * RotationSpeed;
        
        UpdateCubeRotation();
    }

    private void UpdateCubeRotation()
    {
        if (_frontFace == null || _backFace == null || _leftFace == null || 
            _rightFace == null || _topFace == null || _bottomFace == null) return;

        double radX = _rotationX * Math.PI / 180;
        double radY = _rotationY * Math.PI / 180;
        
        double perspective = 600;
        double centerX = 50;
        double centerY = 50;
        
        UpdateFace(_frontFace, 0, radX, radY, centerX, centerY, perspective);
        
        UpdateFace(_backFace, Math.PI, radX, radY, centerX, centerY, perspective);
        
        UpdateFace(_leftFace, -Math.PI / 2, radX, radY, centerX, centerY, perspective);
        
        UpdateFace(_rightFace, Math.PI / 2, radX, radY, centerX, centerY, perspective);
        
        UpdateTopBottomFace(_topFace, -Math.PI / 2, radX, radY, centerX, centerY, perspective, true);
        
        UpdateTopBottomFace(_bottomFace, Math.PI / 2, radX, radY, centerX, centerY, perspective, false);
    }

    private void UpdateFace(Border face, double baseRotationY, double rotX, double rotY, 
                           double centerX, double centerY, double perspective)
    {
        double totalRotY = baseRotationY + rotY;
        
        double z = Math.Cos(totalRotY) * 32;
        double x = Math.Sin(totalRotY) * 32;
        
        double y = 0;
        double tempZ = z * Math.Cos(rotX) - y * Math.Sin(rotX);
        
        double scale = perspective / (perspective + tempZ);
        double projX = x * scale;
        
        double width = Math.Abs(Math.Cos(totalRotY)) * 64;
        if (width < 1) width = 1;
        
        bool isVisible = tempZ > -10;
        face.IsVisible = isVisible;
        
        if (isVisible)
        {
            face.Width = width;
            Canvas.SetLeft(face, centerX + projX - width / 2);
            face.ZIndex = (int)(-tempZ * 10);
            
            double opacity = Math.Abs(Math.Cos(totalRotY));
            face.Opacity = Math.Max(0.3, opacity);
        }
    }

    private void UpdateTopBottomFace(Border face, double baseRotationX, double rotX, double rotY, 
                                     double centerX, double centerY, double perspective, bool isTop)
    {
        double totalRotX = baseRotationX + rotX;
        
        double z = Math.Cos(totalRotX) * 32;
        double y = Math.Sin(totalRotX) * 32;
        
        double tempZ = z * Math.Cos(rotY);
        
        double scale = perspective / (perspective + tempZ);
        double projY = y * scale;
        
        double height = Math.Abs(Math.Cos(totalRotX)) * 16;
        if (height < 1) height = 1;
        
        bool isVisible = (isTop && totalRotX < 0) || (!isTop && totalRotX > 0);
        face.IsVisible = isVisible && tempZ > -10;
        
        if (face.IsVisible)
        {
            face.Height = height;
            Canvas.SetTop(face, centerY + projY - (isTop ? height : 0));
            face.ZIndex = (int)(-tempZ * 10);
            
            double opacity = Math.Abs(Math.Cos(totalRotX));
            face.Opacity = Math.Max(0.3, opacity);
        }
    }
}
