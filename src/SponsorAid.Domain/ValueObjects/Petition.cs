using System;
using System.Collections.Generic;
using System.Text;

namespace SponsorAid.Domain.ValueObjects;

public record Petition
{
    public string Content { get; }

    public Petition(string content)
    {
        // Dilekçe en az 300 karakter olmak zorundadır[cite: 1]
        if (string.IsNullOrWhiteSpace(content) || content.Trim().Length < 300)
            throw new ArgumentException("Dilekçe içeriği en az 300 karakter olmak zorundadır.");

        Content = content.Trim();
    }
}