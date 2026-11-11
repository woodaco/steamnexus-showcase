namespace SteamProject.Models.DTO
{
    /// <summary>Request body for sending a chat message (the body avoids querystring corruption of &/#).</summary>
    public class SendMessageRequest
    {
        public int ToId { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
