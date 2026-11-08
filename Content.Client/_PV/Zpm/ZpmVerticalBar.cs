using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Maths;

namespace Content.Client._PV.Zpm;

/// <summary>
/// A compact vertical meter used by the ZPM console. It can either show a filled value
/// or a colored scale with a movable indicator. Interactive meters snap to fixed increments.
/// </summary>
public sealed class ZpmVerticalBar : Range
{
    private bool _dragging;
    private bool _interactive;

    public bool Interactive
    {
        get => _interactive;
        set
        {
            _interactive = value;
            MouseFilter = value ? MouseFilterMode.Stop : MouseFilterMode.Ignore;
        }
    }

    public bool Segmented { get; set; }
    public float Step { get; set; } = 5f;
    public Color FillColor { get; set; } = Color.Orange;
    public Color TrackColor { get; set; } = Color.FromHex("#17191F");
    public Color MarkerColor { get; set; } = Color.White;
    public Color BorderColor { get; set; } = Color.Black;
    public List<Color> SegmentColors { get; } = new();
    public event Action<int>? IntegerValueChanged;

    public ZpmVerticalBar()
    {
        MouseFilter = MouseFilterMode.Ignore;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var width = PixelWidth;
        var height = PixelHeight;
        if (width <= 0 || height <= 0)
            return;

        handle.DrawRect(new UIBox2(0, 0, width, height), TrackColor);

        if (Segmented && SegmentColors.Count > 0)
        {
            var segmentHeight = height / (float) SegmentColors.Count;
            for (var i = 0; i < SegmentColors.Count; i++)
            {
                var bottom = height - i * segmentHeight;
                var top = MathF.Max(0f, bottom - segmentHeight + 1f);
                handle.DrawRect(new UIBox2(1, top, width - 1, bottom), SegmentColors[i]);
            }
        }
        else
        {
            var fillHeight = height * GetAsRatio();
            if (fillHeight > 0f)
                handle.DrawRect(new UIBox2(1, height - fillHeight, width - 1, height), FillColor);
        }

        var markerY = height * (1f - GetAsRatio());
        handle.DrawRect(new UIBox2(0, markerY - 2f, width, markerY + 2f), MarkerColor);
        handle.DrawRect(new UIBox2(0, 0, width, height), BorderColor, filled: false);
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Handled || !Interactive || args.Function != EngineKeyFunctions.UIClick)
            return;

        _dragging = true;
        SetValueFromY(args.RelativePosition.Y);
        args.Handled = true;
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);
        if (args.Function == EngineKeyFunctions.UIClick)
            _dragging = false;
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        if (_dragging && Interactive)
            SetValueFromY(args.RelativePosition.Y);
    }

    private void SetValueFromY(float y)
    {
        if (Size.Y <= 0f)
            return;

        var ratio = Math.Clamp(1f - y / Size.Y, 0f, 1f);
        var value = MinValue + ratio * (MaxValue - MinValue);
        if (Step > 0f)
            value = MinValue + MathF.Round((value - MinValue) / Step) * Step;

        var previous = Value;
        Value = Math.Clamp(value, MinValue, MaxValue);
        if (Value != previous)
            IntegerValueChanged?.Invoke((int) MathF.Round(Value));
    }
}
