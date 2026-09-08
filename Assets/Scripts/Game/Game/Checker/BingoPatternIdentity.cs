using System;

[Serializable]
public class BingoPatternIdentity : IEquatable<BingoPatternIdentity>
{
    public BingoPatternType patternType;
    public BingoLineType primaryLine;
    public BingoLineType secondaryLine;

    public BingoPatternIdentity()
    {
        patternType = BingoPatternType.SingleLine;
        primaryLine = BingoLineType.None;
        secondaryLine = BingoLineType.None;
    }

    public BingoPatternIdentity(
        BingoPatternType patternType,
        BingoLineType primaryLine,
        BingoLineType secondaryLine)
    {
        this.patternType = patternType;

        if (patternType == BingoPatternType.TwoLines &&
            (int)primaryLine > (int)secondaryLine)
        {
            this.primaryLine = secondaryLine;
            this.secondaryLine = primaryLine;
        }
        else
        {
            this.primaryLine = primaryLine;
            this.secondaryLine = secondaryLine;
        }
    }

    public BingoPatternIdentity(BingoPatternIdentity identity)
        : this(
            identity?.patternType ?? BingoPatternType.SingleLine,
            identity?.primaryLine ?? BingoLineType.None,
            identity?.secondaryLine ?? BingoLineType.None)
    {
    }

    public static BingoPatternIdentity FromResult(BingoPatternCheckResult patternResult)
    {
        return patternResult == null
            ? null
            : new BingoPatternIdentity(
                patternResult.patternType,
                patternResult.primaryLine,
                patternResult.secondaryLine);
    }

    public bool Matches(BingoPatternCheckResult patternResult)
    {
        return patternResult != null && Equals(FromResult(patternResult));
    }

    public bool Equals(BingoPatternIdentity other)
    {
        return other != null &&
               patternType == other.patternType &&
               primaryLine == other.primaryLine &&
               secondaryLine == other.secondaryLine;
    }

    public override bool Equals(object obj)
    {
        return Equals(obj as BingoPatternIdentity);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(patternType, primaryLine, secondaryLine);
    }
}

public static class BingoPatternIdentityList
{
    public static bool Contains(
        System.Collections.Generic.IReadOnlyList<BingoPatternIdentity> identities,
        BingoPatternIdentity target)
    {
        if (identities == null || target == null)
        {
            return false;
        }

        for (int i = 0; i < identities.Count; i++)
        {
            if (target.Equals(identities[i]))
            {
                return true;
            }
        }

        return false;
    }

    public static bool AddUnique(
        System.Collections.Generic.List<BingoPatternIdentity> identities,
        BingoPatternIdentity target)
    {
        if (identities == null || target == null || Contains(identities, target))
        {
            return false;
        }

        identities.Add(new BingoPatternIdentity(target));
        return true;
    }

    public static bool Remove(
        System.Collections.Generic.List<BingoPatternIdentity> identities,
        BingoPatternIdentity target)
    {
        if (identities == null || target == null)
        {
            return false;
        }

        for (int i = identities.Count - 1; i >= 0; i--)
        {
            if (target.Equals(identities[i]))
            {
                identities.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    public static System.Collections.Generic.List<BingoPatternIdentity> Clone(
        System.Collections.Generic.IReadOnlyList<BingoPatternIdentity> identities)
    {
        System.Collections.Generic.List<BingoPatternIdentity> copy =
            new System.Collections.Generic.List<BingoPatternIdentity>();

        if (identities == null)
        {
            return copy;
        }

        for (int i = 0; i < identities.Count; i++)
        {
            AddUnique(copy, identities[i]);
        }

        return copy;
    }
}
