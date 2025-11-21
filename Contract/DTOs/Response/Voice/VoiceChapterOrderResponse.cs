namespace Contract.DTOs.Response.Voice
{
    public class VoiceChapterOrderResponse : VoiceChapterStatusResponse
    {
        public long CharactersCharged { get; set; }
        public long WalletBalance { get; set; }
    }
}
