namespace SmallMind.Runtime.Constraints
{
    /// <summary>
    /// Enforces XML syntax constraints during generation.
    /// Validates tag structure, attributes, and well-formedness.
    /// </summary>
    internal sealed class XmlConstraintEnforcer : IOutputConstraint
    {
        public string ConstraintDescription => "XML well-formedness validation";

        public bool IsTokenAllowed(string generatedSoFar, int candidateTokenId, string candidateTokenText)
        {
            if (string.IsNullOrEmpty(generatedSoFar))
            {
                // Must start with < for opening tag or <?xml for declaration
                return candidateTokenText.TrimStart().StartsWith("<");
            }

            string combined = generatedSoFar + candidateTokenText;

            // Basic validation: count opening/closing tags
            var tagStack = new Stack<string>();
            int i = 0;

            while (i < combined.Length)
            {
                if (combined[i] == '<')
                {
                    // Find end of tag
                    int end = combined.IndexOf('>', i);
                    if (end == -1) return true; // Incomplete tag is ok during generation

                    string tag = combined.Substring(i + 1, end - i - 1).Trim();

                    if (tag.StartsWith("?")) // XML declaration
                    {
                        i = end + 1;
                        continue;
                    }

                    if (tag.StartsWith("/")) // Closing tag
                    {
                        string tagName = tag.Substring(1).Split(' ')[0];
                        if (tagStack.Count == 0) return false; // Closing without opening

                        string expected = tagStack.Pop();
                        if (!tagName.Equals(expected, StringComparison.Ordinal))
                            return false; // Mismatched tags
                    }
                    else if (tag.EndsWith("/")) // Self-closing tag
                    {
                        // Valid, no stack change
                    }
                    else // Opening tag
                    {
                        string tagName = tag.Split(' ', '>')[0];
                        tagStack.Push(tagName);
                    }

                    i = end + 1;
                }
                else
                {
                    i++;
                }
            }

            return true; // Allow during generation
        }

        public bool IsComplete(string generatedSoFar)
        {
            if (string.IsNullOrWhiteSpace(generatedSoFar))
                return false;

            // Check all tags are closed
            var tagStack = new Stack<string>();
            int i = 0;

            while (i < generatedSoFar.Length)
            {
                if (generatedSoFar[i] == '<')
                {
                    int end = generatedSoFar.IndexOf('>', i);
                    if (end == -1) return false; // Incomplete tag

                    string tag = generatedSoFar.Substring(i + 1, end - i - 1).Trim();

                    if (tag.StartsWith("?"))
                    {
                        i = end + 1;
                        continue;
                    }

                    if (tag.StartsWith("/"))
                    {
                        if (tagStack.Count == 0) return false;
                        tagStack.Pop();
                    }
                    else if (!tag.EndsWith("/"))
                    {
                        string tagName = tag.Split(' ', '>')[0];
                        tagStack.Push(tagName);
                    }

                    i = end + 1;
                }
                else
                {
                    i++;
                }
            }

            return tagStack.Count == 0; // All tags closed
        }
    }
}
