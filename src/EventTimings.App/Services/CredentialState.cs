using System.Text.RegularExpressions;
using EventTimings.Contracts;

namespace EventTimings.App.Services;

public sealed class CredentialState
{
    private string officialName = string.Empty;
    private string pin = string.Empty;
    private bool isAuthenticated;
    private OfficialDto? authenticatedOfficial;

    public string OfficialName
    {
        get => officialName;
        set
        {
            if (officialName == value) return;
            officialName = value;
            NotifyChanged();
        }
    }

    public string Pin
    {
        get => pin;
        set
        {
            if (pin == value) return;
            pin = value;
            NotifyChanged();
        }
    }

    public bool AreCredentialsValid =>
        !string.IsNullOrWhiteSpace(OfficialName) && Regex.IsMatch(Pin ?? string.Empty, "^\\d{4}$");

    public bool IsAuthenticated => isAuthenticated;

    public OfficialDto? AuthenticatedOfficial => authenticatedOfficial;

    public void MarkAuthenticated(OfficialDto official, string? retainedPin = null)
    {
        if (official is null) return;
        isAuthenticated = true;
        authenticatedOfficial = official;
        OfficialName = official.FullName;
        // retain the PIN for the session if provided (in-memory only)
        Pin = retainedPin ?? string.Empty;
        NotifyChanged();
    }

    public void ClearAuthentication()
    {
        isAuthenticated = false;
        authenticatedOfficial = null;
        OfficialName = string.Empty;
        Pin = string.Empty;
        NotifyChanged();
    }

    public event Action? OnChange;

    private void NotifyChanged() => OnChange?.Invoke();
}
