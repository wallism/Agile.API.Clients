namespace Agile.API.Clients
{
    public enum MethodPriority
    {
        Normal = 1,

        /// <summary>
        ///     High priority calls are not blocked by the RateGate (still counted)
        /// </summary>
        High = 2
    }
}