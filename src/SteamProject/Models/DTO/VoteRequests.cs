namespace SteamProject.Models.DTO
{
    /// <summary>Request body for casting/updating a "play again" competition vote.</summary>
    public class CompetitionVoteRequest
    {
        public int Id { get; set; }
        public int CompetitionId { get; set; }
        public bool WantsToPlayAgain { get; set; }
    }
}
