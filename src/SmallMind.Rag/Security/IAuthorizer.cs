namespace SmallMind.Rag.Security;

/// <summary>
/// Defines the contract for authorization of chunk access.
/// </summary>
public interface IAuthorizer
{
    /// <summary>
    /// Determines whether the specified user is authorized to access the chunk.
    /// </summary>
    /// <param name="user">The user context.</param>
    /// <param name="chunk">The chunk to authorize.</param>
    /// <returns>True if authorized; otherwise, false.</returns>
    bool IsAuthorized(UserContext user, Chunk chunk);

    /// <summary>
    /// Filters a list of chunks to only those the user is authorized to access.
    /// </summary>
    /// <param name="user">The user context.</param>
    /// <param name="chunks">The chunks to filter.</param>
    /// <returns>A filtered list containing only authorized chunks.</returns>
    List<Chunk> FilterChunks(UserContext user, List<Chunk> chunks);
}
