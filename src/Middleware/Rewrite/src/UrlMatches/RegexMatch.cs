// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Microsoft.AspNetCore.Rewrite.UrlMatches;

internal class RegexMatch : UrlMatch
{
    private readonly Regex _match;

    public RegexMatch(Regex match, bool negate)
    {
        _match = match;
        Negate = negate;
    }

    public override MatchResults Evaluate([StringSyntax(StringSyntaxAttribute.Regex)] string pattern, RewriteContext context)
    {
        var res = _match.Match(pattern);
        return new MatchResults(success: res.Success != Negate, new BackReferenceCollection(res.Groups));
    }
}
