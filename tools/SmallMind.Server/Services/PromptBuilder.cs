using SmallMind.Server.Models;
using System.Text;

namespace SmallMind.Server.Services;

public static class PromptBuilder
{
    public static string BuildPrompt(List<ChatMessage> messages)
    {
        if (messages.Count == 0)
            return string.Empty;

        var sb = new StringBuilder(capacity: 512);
        
        for (int i = 0; i < messages.Count; i++)
        {
            var msg = messages[i];
            
            switch (msg.Role.ToLowerInvariant())
            {
                case "system":
                    sb.Append("System: ");
                    sb.Append(msg.Content);
                    sb.Append("\n\n");
                    break;
                    
                case "user":
                    sb.Append("User: ");
                    sb.Append(msg.Content);
                    sb.Append("\n\n");
                    break;
                    
                case "assistant":
                    sb.Append("Assistant: ");
                    sb.Append(msg.Content);
                    sb.Append("\n\n");
                    break;
                    
                default:
                    sb.Append(msg.Role);
                    sb.Append(": ");
                    sb.Append(msg.Content);
                    sb.Append("\n\n");
                    break;
            }
        }
        
        sb.Append("Assistant:");
        
        return sb.ToString();
    }
}
