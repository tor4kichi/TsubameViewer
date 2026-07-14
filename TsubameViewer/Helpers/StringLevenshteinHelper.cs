using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CommunityToolkit.WinUI.Animations.Expressions.ExpressionValues;

namespace TsubameViewer.Helpers;

public static class StringLevenshteinHelper
{
    public static double GetSimilarityNormalized(string source, string target)
    {
        int dist = Quickenshtein.Levenshtein.GetDistance(source, target);
        double length = Math.Max(source.Length, target.Length);
        return 1d - dist / length;
    }

    public static int GetDistance(string source, string target)
    {
        return Quickenshtein.Levenshtein.GetDistance(source, target);
    }
}
