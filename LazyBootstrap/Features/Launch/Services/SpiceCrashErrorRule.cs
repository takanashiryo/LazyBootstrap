using System;
using System.Collections.Generic;

namespace LazyBootstrap.Features.Launch.Services
{
    public sealed class SpiceCrashErrorRule
    {
        public SpiceCrashErrorRule(string id, string reasonText, params string[] markers)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Rule id must not be empty.", nameof(id));
            }

            if (string.IsNullOrWhiteSpace(reasonText))
            {
                throw new ArgumentException("Reason text must not be empty.", nameof(reasonText));
            }

            Id = id;
            ReasonText = reasonText;
            Markers = markers ?? Array.Empty<string>();
        }

        public string Id { get; }

        public string ReasonText { get; }

        public IReadOnlyList<string> Markers { get; }
    }
}
