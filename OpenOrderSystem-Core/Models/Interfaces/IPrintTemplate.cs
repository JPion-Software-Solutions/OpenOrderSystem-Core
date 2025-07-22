namespace OpenOrderSystem.Core.Models.Interfaces
{
    public interface IPrintTemplate
    {
        public byte[] Instructions { get; protected set; }
    }
}
