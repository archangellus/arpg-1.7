namespace ShatterStone
{
    /// <summary>
    /// Implement this on anything that can be hit by the mining pickaxe.
    /// </summary>
    public interface IMineableNode
    {
        void Interact();
        void Interact(int hits);
    }
}
