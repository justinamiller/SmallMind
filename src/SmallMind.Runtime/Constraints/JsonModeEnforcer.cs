using System;

namespace SmallMind.Runtime.Constraints
{
    /// <summary>
    /// Enforces JSON structural validity during generation.
    /// Implements a simplified state machine that ensures balanced braces/brackets.
    /// </summary>
    public sealed class JsonModeEnforcer : IOutputConstraint
    {
        private enum State
        {
            Start,
            InObject,
            InArray,
            InString,
            AfterKey,
            AfterValue
        }

        public string ConstraintDescription => "JSON mode (structural validation only)";

        public bool IsTokenAllowed(string generatedSoFar, int candidateTokenId, string candidateTokenText)
        {
            if (string.IsNullOrEmpty(candidateTokenText))
                return false;

            string combined = generatedSoFar + candidateTokenText;
            
            // Basic structural checks
            int braceDepth = 0;
            int bracketDepth = 0;
            bool inString = false;
            bool escaped = false;

            for (int i = 0; i < combined.Length; i++)
            {
                char c = combined[i];

                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (c == '\\' && inString)
                {
                    escaped = true;
                    continue;
                }

                if (c == '"')
                {
                    inString = !inString;
                    continue;
                }

                if (inString)
                    continue;

                switch (c)
                {
                    case '{':
                        braceDepth++;
                        break;
                    case '}':
                        braceDepth--;
                        if (braceDepth < 0) return false;
                        break;
                    case '[':
                        bracketDepth++;
                        break;
                    case ']':
                        bracketDepth--;
                        if (bracketDepth < 0) return false;
                        break;
                }
            }

            // Must start with { or [
            if (generatedSoFar.Length == 0)
            {
                char first = candidateTokenText.TrimStart()[0];
                return first == '{' || first == '[';
            }

            return true;
        }

        public bool IsComplete(string generatedSoFar)
        {
            if (string.IsNullOrWhiteSpace(generatedSoFar))
                return false;

            string trimmed = generatedSoFar.Trim();
            if (trimmed.Length == 0)
                return false;

            // Must start with { or [
            char first = trimmed[0];
            if (first != '{' && first != '[')
                return false;

            // Check balanced braces/brackets
            int braceDepth = 0;
            int bracketDepth = 0;
            bool inString = false;
            bool escaped = false;

            foreach (char c in trimmed)
            {
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (c == '\\' && inString)
                {
                    escaped = true;
                    continue;
                }

                if (c == '"')
                {
                    inString = !inString;
                    continue;
                }

                if (inString)
                    continue;

                switch (c)
                {
                    case '{':
                        braceDepth++;
                        break;
                    case '}':
                        braceDepth--;
                        break;
                    case '[':
                        bracketDepth++;
                        break;
                    case ']':
                        bracketDepth--;
                        break;
                }
            }

            return braceDepth == 0 && bracketDepth == 0 && !inString;
        }
    }
}
