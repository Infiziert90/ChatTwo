using System.Numerics;

namespace ChatTwo.Util;

public abstract class ImGuiDrawable
{
    public bool Failed = false;
    public bool IsLoaded = false;

    public virtual void Draw(Vector2 size)
    {
        throw new NotImplementedException();
    }
}