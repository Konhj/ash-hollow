using Godot;

namespace AshHollow;

/// <summary>世界层特效(粒子/交互提示),Z 序在实体之上。</summary>
public partial class FxLayer : Node2D
{
    MainGame _game;

    public override void _Ready()
    {
        _game = FindGame();
    }

    public override void _Draw()
    {
        _game?.DrawFx(this);
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
