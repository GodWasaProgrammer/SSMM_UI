using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SSMM_UI.Services;
using System.Collections.ObjectModel;
using System;
using System.Text.Json.Serialization;

namespace SSMM_UI.ViewModel;

public partial class WebhooksViewModel : ObservableObject
{
    public WebhooksViewModel(StateService _state)
    {
        _stateService = _state;
        Webhooks = _stateService.Webhooks;
        DisplayWebhooks = true;
    }

    public ObservableCollection<KeyValueItem> Webhooks { get; } = [];
    readonly StateService _stateService;

    [ObservableProperty] bool displayWebhooks;
    partial void OnDisplayWebhooksChanged(bool value)
    {
        OnPropertyChanged(nameof(ToggleWebhooksButtonText));
    }

    public string ToggleWebhooksButtonText => DisplayWebhooks ? "Hide Webhooks" : "Show Webhooks";

    [ObservableProperty] string? feedbackMessage;
    partial void OnFeedbackMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(HasFeedbackMessage));
    }

    public bool HasFeedbackMessage => !string.IsNullOrWhiteSpace(FeedbackMessage);

    [RelayCommand]
    private void ToggleWebHooks()
    {
        DisplayWebhooks = !DisplayWebhooks;
    }

    [ObservableProperty] bool isAddingNewWebHook;

    [RelayCommand]
    private void AddWebhook()
    {
        IsAddingNewWebHook = true;
        FeedbackMessage = null;
    }

    [RelayCommand]
    private void CancelNewWebHook()
    {
        IsAddingNewWebHook = false;
        NameofWebHook = string.Empty;
        LinkOfWebhook = string.Empty;
        FeedbackMessage = null;
    }

    [ObservableProperty] string nameofWebHook = string.Empty;
    [ObservableProperty] string linkOfWebhook = string.Empty;

    [RelayCommand]
    private void SaveNewWebHook()
    {
        var name = NameofWebHook?.Trim();
        var link = LinkOfWebhook?.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            FeedbackMessage = "Webhook name is required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(link))
        {
            FeedbackMessage = "Webhook URL is required.";
            return;
        }

        if (!Uri.TryCreate(link, UriKind.Absolute, out _))
        {
            FeedbackMessage = "Webhook URL is invalid.";
            return;
        }

        IsAddingNewWebHook = false;
        Webhooks.Add(new KeyValueItem
        {
            Key = name,
            Value = link,
            IsRevealed = false
        });
        _stateService.SerializeWebhooks();
        FeedbackMessage = $"Saved webhook '{name}'.";
        NameofWebHook = string.Empty;
        LinkOfWebhook = string.Empty;
    }

    [RelayCommand]
    private void ToggleWebhookReveal(KeyValueItem? item)
    {
        if (item is null)
        {
            return;
        }

        item.IsRevealed = !item.IsRevealed;
    }

    [RelayCommand]
    private void RemoveWebhook(KeyValueItem? item)
    {
        if (item is null)
        {
            return;
        }

        if (Webhooks.Remove(item))
        {
            _stateService.SerializeWebhooks();
            FeedbackMessage = $"Removed webhook '{item.Key}'.";
        }
    }
}

public partial class KeyValueItem : ObservableObject
{
    [ObservableProperty] private bool isRevealed;

    public string? Key { get; set; }
    public string? Value { get; set; }

    [JsonIgnore]
    public string DisplayValue => IsRevealed ? Value ?? string.Empty : MaskedValue;

    [JsonIgnore]
    public string RevealButtonText => IsRevealed ? "Hide" : "Reveal";

    [JsonIgnore]
    public string MaskedValue
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Value))
            {
                return string.Empty;
            }

            var source = Value!;
            if (source.Length <= 12)
            {
                return new string('*', source.Length);
            }

            return $"{source[..6]}...{source[^6..]}";
        }
    }

    partial void OnIsRevealedChanged(bool value)
    {
        OnPropertyChanged(nameof(DisplayValue));
        OnPropertyChanged(nameof(RevealButtonText));
    }
}
