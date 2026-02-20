using System;

namespace FileSearchTool.Services.Ranking;

public static class IdfApprox
{
    // klassisk smooth: log((N+1)/(df+1)) + 1
    public static float Compute(int N, int df)
    {
        if (N <= 0) return 0f;
        var v = Math.Log((N + 1.0) / (df + 1.0)) + 1.0;
        return (float)v;
    }
}