namespace SmallMind.Rag.Security;

/// <summary>
/// Default implementation of <see cref="IAuthorizer"/> that uses <see cref="UserContext.CanAccess"/>.
/// </summary>
internal sealed class DefaultAuthorizer : IAuthorizer
{
    /// <summary>
    /// Determines whether the specified user is authorized to access the chunk.
    /// </summary>
    /// <param name="user">The user context.</param>
    /// <param name="chunk">The chunk to authorize.</param>
    /// <returns>True if authorized; otherwise, false.</returns>
    public bool IsAuthorized(UserContext user, Chunk chunk)
    {
        return user.CanAccess(chunk);
    }

    /// <summary>
    /// Filters a list of chunks to only those the user is authorized to access.
    /// </summary>
    /// <param name="user">The user context.</param>
    /// <param name="chunks">The chunks to filter.</param>
    /// <returns>A filtered list containing only authorized chunks.</returns>
    public List<Chunk> FilterChunks(UserContext user, List<Chunk> chunks)
    {
        var result = new List<Chunk>(chunks.Count);

        for (int i = 0; i < chunks.Count; i++)
        {
            if (IsAuthorized(user, chunks[i]))
            {
                result.Add(chunks[i]);
            }
        }

        return result;
    }
}
