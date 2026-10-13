using Godot;

namespace AshHollow;

/// <summary>弹丸层(小怪之上、玩家之下)。</summary>
public partial class ProjLayer : Node2D
{
    MainGame _game;

    public override void _Ready()
    {
        _game = FindGame();
    }

    public override void _Draw()
    {
        _game?.DrawProjectiles(this);
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
