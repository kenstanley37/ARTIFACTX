using System;
using System.Collections.Generic;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One project or person credited, shown as a card (CreditsPage.xaml).</summary>
public sealed record Credit(string Name, string Who, string What, string Licence, string Url, string LinkText = "Open on GitHub")
{
    public bool HasLicence => Licence.Length > 0;
    public string LicenceTip => $"Licence: {Licence}";
    public Uri Link => new(Url);
    public string LinkName => $"{LinkText}: {Name}";
}

/// <summary>A group of credits with its heading and a line about it.</summary>
public sealed record CreditSection(string Title, string Blurb, IReadOnlyList<Credit> Items)
{
    public bool HasBlurb => Blurb.Length > 0;
}
