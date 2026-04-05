using SSMM_UI.DTO;
using System;

namespace SSMM_UI.Services;

public sealed class ChatConcatenationPolicy
{
    public bool TryConcatenate(ChatMessageDto previous, ChatMessageDto incoming, TimeSpan window, int maxLines, out ChatMessageDto merged)
    {
        merged = previous;

        if (incoming.IsSystem || previous.IsSystem)
        {
            return false;
        }

        if (incoming.Provider != previous.Provider || !string.Equals(incoming.Author, previous.Author, StringComparison.Ordinal))
        {
            return false;
        }

        var delta = incoming.TimestampUtc - previous.TimestampUtc;
        if (delta < TimeSpan.Zero || delta > window)
        {
            return false;
        }

        var existingLines = previous.Message.Split(Environment.NewLine).Length;
        if (existingLines >= maxLines)
        {
            return false;
        }

        merged = previous with
        {
            Message = $"{previous.Message}{Environment.NewLine}{incoming.Message}",
            TimestampUtc = incoming.TimestampUtc,
            MessageId = incoming.MessageId
        };

        return true;
    }
}
