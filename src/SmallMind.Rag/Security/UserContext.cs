namespace SmallMind.Rag.Security;

/// <summary>
/// Represents the security context for a user accessing RAG resources.
/// </summary>
public sealed class UserContext
{
    /// <summary>
    /// Gets or sets the unique identifier for the user.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets the set of security labels the user is authorized to access.
    /// </summary>
    public HashSet<string> AllowedLabels { get; }

    /// <summary>
    /// Gets the set of tags the user is authorized to access.
    /// </summary>
    public HashSet<string> AllowedTags { get; }

    /// <summary>
    /// Gets custom claims for extensibility.
    /// </summary>
    public Dictionary<string, string> CustomClaims { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="UserContext"/> class.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    public UserContext(string userId)
    {
        UserId = userId;
        AllowedLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AllowedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CustomClaims = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines whether the user can access the specified chunk based on security labels and tags.
    /// </summary>
    /// <param name="chunk">The chunk to check access for.</param>
    /// <returns>True if the user can access the chunk; otherwise, false.</returns>
    public bool CanAccess(Chunk chunk)
    {
        // If chunk has a security label, check if user has access
        if (!string.IsNullOrEmpty(chunk.SecurityLabel))
        {
            if (AllowedLabels.Contains(chunk.SecurityLabel))
            {
                return true;
            }
        }
        else if (AllowedLabels.Count == 0 || string.IsNullOrEmpty(chunk.SecurityLabel))
        {
            // No security label on chunk - check tags
            if (chunk.Tags != null && chunk.Tags.Length > 0)
            {
                // Check if user has any matching tag
                for (int i = 0; i < chunk.Tags.Length; i++)
                {
                    if (AllowedTags.Contains(chunk.Tags[i]))
                    {
                        return true;
                    }
                }
            }
            else
            {
                // No restrictions - allow access
                return true;
            }
        }

        // If we have restrictions but no match, check if no restrictions apply
        if (AllowedLabels.Count == 0 && AllowedTags.Count == 0)
        {
            return true;
        }

        return false;
    }
}
