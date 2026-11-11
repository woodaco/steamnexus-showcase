namespace SteamProject.Models.DTO;

/// <summary>
/// A minimal (Id, SteamId) projection of a registered user, returned only for the
/// SteamIds a caller passes in. Used to resolve messenger contacts without
/// materialising the whole users table (and its lazy-loading proxies) per request.
/// </summary>
public record UserSummary(int Id, string SteamId);
