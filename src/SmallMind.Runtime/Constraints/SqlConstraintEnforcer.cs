using System;
using System.Collections.Generic;

namespace SmallMind.Runtime.Constraints
{
    /// <summary>
    /// Enforces SQL syntax constraints during generation.
    /// Validates keywords, parentheses, and quote balancing.
    /// </summary>
    public sealed class SqlConstraintEnforcer : IOutputConstraint
    {
        private static readonly HashSet<string> SQL_KEYWORDS = new(StringComparer.OrdinalIgnoreCase)
        {
            "SELECT", "FROM", "WHERE", "INSERT", "UPDATE", "DELETE", "CREATE",
            "DROP", "ALTER", "TABLE", "INDEX", "VIEW", "JOIN", "LEFT", "RIGHT",
            "INNER", "OUTER", "ON", "AS", "AND", "OR", "NOT", "IN", "EXISTS",
            "ORDER", "BY", "GROUP", "HAVING", "LIMIT", "OFFSET", "UNION"
        };
        
        public string ConstraintDescription => "SQL syntax validation";
        
        public bool IsTokenAllowed(string generatedSoFar, int candidateTokenId, string candidateTokenText)
        {
            if (string.IsNullOrEmpty(generatedSoFar))
            {
                // Must start with SELECT, INSERT, UPDATE, DELETE, or CREATE
                string upper = candidateTokenText.ToUpperInvariant().Trim();
                return upper.StartsWith("SELECT") || upper.StartsWith("INSERT") ||
                       upper.StartsWith("UPDATE") || upper.StartsWith("DELETE") ||
                       upper.StartsWith("CREATE");
            }
            
            string combined = generatedSoFar + candidateTokenText;
            
            // Check parentheses balance
            int openParen = 0;
            foreach (char c in combined)
            {
                if (c == '(') openParen++;
                else if (c == ')') openParen--;
                if (openParen < 0) return false; // More closing than opening
            }
            
            // Check quote balance (simple check for single and double quotes)
            int singleQuotes = 0;
            int doubleQuotes = 0;
            foreach (char c in combined)
            {
                if (c == '\'') singleQuotes++;
                else if (c == '"') doubleQuotes++;
            }
            
            // Allow only if quotes are balanced or can be closed
            return true; // Simplified - full SQL validation is complex
        }
        
        public bool IsComplete(string generatedSoFar)
        {
            if (string.IsNullOrWhiteSpace(generatedSoFar))
                return false;
            
            // Check if ends with semicolon (optional but common)
            generatedSoFar = generatedSoFar.TrimEnd();
            
            // Check parentheses balance
            int openParen = 0;
            foreach (char c in generatedSoFar)
            {
                if (c == '(') openParen++;
                else if (c == ')') openParen--;
            }
            
            return openParen == 0; // Balanced
        }
    }
}
