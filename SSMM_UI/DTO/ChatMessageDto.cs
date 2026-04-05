using SSMM_UI.Enums;
using System;

namespace SSMM_UI.DTO;

public sealed record ChatMessageDto(
    AuthProvider Provider,
    string Author,
    string Message,
    DateTime TimestampUtc,
    bool IsSystem,
    string MessageId);
