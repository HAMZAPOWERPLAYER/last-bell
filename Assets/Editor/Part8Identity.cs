using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// PART 8: application identity. Renames the Android package id off the base project's
/// (com.yurifarion.*) onto HAMZA POWERPLAYER's, and aligns the display identity with it.
///
/// MENU: Tools/Part8/Set Package Identity
/// </summary>
public static class Part8Identity
{
    const string AndroidId = "com.hamzapowerplayer.lastbell";
    const string ProductName = "Last Bell";
    const string Company = "HAMZA POWERPLAYER";

    [MenuItem("Tools/Part8/Set Package Identity")]
    public static void SetIdentity()
    {
        var log = new StringBuilder();
        log.AppendLine("  BEFORE");
        log.AppendLine("    productName   = " + PlayerSettings.productName);
        log.AppendLine("    companyName   = " + PlayerSettings.companyName);
        log.AppendLine("    bundleVersion = " + PlayerSettings.bundleVersion);
        log.AppendLine("    Android id    = " + PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android));
        log.AppendLine("    Standalone id = " + PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Standalone));

        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, AndroidId);
        PlayerSettings.productName = ProductName;
        PlayerSettings.companyName = Company;
        AssetDatabase.SaveAssets();

        log.AppendLine();
        log.AppendLine("  AFTER");
        log.AppendLine("    productName   = " + PlayerSettings.productName);
        log.AppendLine("    companyName   = " + PlayerSettings.companyName);
        log.AppendLine("    bundleVersion = " + PlayerSettings.bundleVersion);
        log.AppendLine("    Android id    = " + PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android));
        log.AppendLine("    Standalone id = " + PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Standalone));
        Debug.Log("[PART8 IDENTITY]\n" + log);
    }
}
