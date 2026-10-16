using System.Diagnostics;
using UnityEngine;

/// <summary>
/// Development-only tracing.
///
/// <see cref="ConditionalAttribute"/> removes every call site at compile time when neither
/// UNITY_EDITOR nor DEVELOPMENT_BUILD is defined, so the "[DIAG]" instrumentation used while
/// building the game costs nothing and allocates nothing in a release player - unlike a plain
/// Debug.Log, which ships the string concatenation and the log entry into the Android build.
/// </summary>
public static class Diag
{
    [Conditional("UNITY_EDITOR")]
    [Conditional("DEVELOPMENT_BUILD")]
    public static void Log(string message)
    {
        // Fully qualified: this file imports System.Diagnostics, so a bare `Debug` is ambiguous
        // between System.Diagnostics.Debug and UnityEngine.Debug.
        UnityEngine.Debug.Log("[DIAG] " + message);
    }
}
