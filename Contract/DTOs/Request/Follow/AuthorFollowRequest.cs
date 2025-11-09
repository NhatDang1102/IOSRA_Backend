namespace Contract.DTOs.Request.Follow
{
    public class AuthorFollowRequest
    {
        /// <summary>
        /// Optional flag to immediately opt into notifications; defaults to true.
        /// </summary>
        public bool? EnableNotifications { get; set; }
    }
}
