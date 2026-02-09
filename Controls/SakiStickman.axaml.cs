using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using System.Diagnostics;

namespace JustLauncher.Controls;

public partial class SakiStickman : UserControl
{
    private bool _isDragging;
    private Point _clickOffset;
    private DispatcherTimer _physicsTimer;
    private Random _random = new();

    private double _vx, _vy;
    private double _gravity = 0.5;
    private double _friction = 0.98;
    private double _bounce = -0.6;
    private Point _lastPos;
    private Stopwatch _dragStopwatch = new();

    private bool _isWalking;
    private double _targetX;
    private double _walkSpeed = 2.0;

    public SakiStickman()
    {
        InitializeComponent();

        _physicsTimer = new DispatcherTimer();
        _physicsTimer.Interval = TimeSpan.FromSeconds(0.032);
        _physicsTimer.Tick += Physics_Tick;
        _physicsTimer.Start();

        PointerPressed += Saki_PointerPressed;
        PointerMoved += Saki_PointerMoved;
        PointerReleased += Saki_PointerReleased;
        
        UpdateSkin();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public void UpdateSkin()
    {
        var settings = ConfigManager.LoadSettings();
        var head = this.FindControl<FaceTracker>("SakiHead");
        if (head != null) head.Username = settings.SakiSkin;
    }

    private void Saki_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _isDragging = true;
        _isWalking = false;
        _vx = 0;
        _vy = 0;
        _clickOffset = e.GetPosition(this);
        _lastPos = e.GetPosition(Parent as Visual);
        _dragStopwatch.Restart();
        e.Pointer.Capture(this);
    }

    private void Saki_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isDragging)
        {
            var parent = Parent as Canvas;
            if (parent != null)
            {
                var currentPos = e.GetPosition(parent);
                double newX = currentPos.X - _clickOffset.X;
                double newY = currentPos.Y - _clickOffset.Y;

                Canvas.SetLeft(this, newX);
                Canvas.SetTop(this, newY);
                
                double dt = _dragStopwatch.Elapsed.TotalSeconds;
                if (dt > 0)
                {
                    _vx = (currentPos.X - _lastPos.X) / (dt * 60);
                    _vy = (currentPos.Y - _lastPos.Y) / (dt * 60);
                }
                _lastPos = currentPos;
                _dragStopwatch.Restart();
            }
        }
    }

    private void Saki_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isDragging = false;
        e.Pointer.Capture(null);
    }

    private void Physics_Tick(object? sender, EventArgs e)
    {
        if (!IsVisible) return;
        if (_isDragging) return;

        var parent = Parent as Canvas;
        if (parent == null) return;

        double x = Canvas.GetLeft(this);
        double y = Canvas.GetTop(this);

        _vy += _gravity;

        _vx *= _friction;
        _vy *= _friction;

        if (!_isWalking)
        {
            if (_random.NextDouble() < 0.005)
            {
                _targetX = _random.Next(0, (int)Math.Max(100, parent.Bounds.Width - 100));
                _isWalking = true;
            }
        }
        else
        {
            double dx = _targetX - x;
            if (Math.Abs(dx) < 10)
            {
                _isWalking = false;
            }
            else
            {
                _vx += (dx > 0 ? 0.3 : -0.3);
                _vx = Math.Clamp(_vx, -_walkSpeed, _walkSpeed);
            }
        }

        double nextX = x + _vx;
        double nextY = y + _vy;

        if (nextX < 0) { nextX = 0; _vx *= _bounce; }
        if (nextX > parent.Bounds.Width - Bounds.Width) { nextX = parent.Bounds.Width - Bounds.Width; _vx *= _bounce; }
        
        if (nextY < 0) { nextY = 0; _vy *= _bounce; }
        if (nextY > parent.Bounds.Height - Bounds.Height) 
        { 
            nextY = parent.Bounds.Height - Bounds.Height; 
            _vy *= _bounce; 
            if (Math.Abs(_vy) < 1) _vy = 0;
            if (Math.Abs(_vx) < 0.1) _vx = 0;
        }

        Canvas.SetLeft(this, nextX);
        Canvas.SetTop(this, nextY);

        if (Math.Abs(_vx) > 0.5)
        {
            if (RenderTransform is ScaleTransform scale)
            {
                scale.ScaleX = _vx < 0 ? -1 : 1;
            }
        }
    }
}
