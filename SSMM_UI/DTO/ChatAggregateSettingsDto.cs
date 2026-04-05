using System;

namespace SSMM_UI.DTO;

public sealed record ChatAggregateSettingsDto(
    bool EnableConcatenation,
    TimeSpan ConcatenationWindow,
    int MaxMessages,
    int MaxConcatenatedLines);
