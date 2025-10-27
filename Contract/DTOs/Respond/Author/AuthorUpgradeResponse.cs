namespace Contract.DTOs.Respond.Author
{
    public class AuthorUpgradeResponse
    {
        public ulong RequestId { get; set; }
        public string Status { get; set; } = "pending";
        public ulong? AssignedOmodId { get; set; }
    }
}
