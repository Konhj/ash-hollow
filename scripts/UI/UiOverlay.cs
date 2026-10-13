using Godot;

namespace AshHollow;

/// <summary>屏幕层 UI 绘制(CanvasLayer,对应旧 pygame 的 canvas 叠加层)。</summary>
public partial class UiOverlay : Control
{
    MainGame _game;

    public override void _Ready()
    {
        _game = FindGame();
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Draw()
    {
        _game?.DrawUi(this);
    }

    MainGame FindGame()
    {
        Node n = this;
        while (n != null)
        {
            if (n is MainGame g)
            {
                return g;
            }
            n = n.GetParent();
        }
        return null;
    }
}
