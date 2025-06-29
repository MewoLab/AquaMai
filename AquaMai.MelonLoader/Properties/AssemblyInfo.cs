using System.Reflection;
using AquaMai.Common;

[assembly: AssemblyTitle(BuildInfo.Description)]
[assembly: AssemblyDescription(BuildInfo.Description)]
[assembly: AssemblyCompany(BuildInfo.Company)]
[assembly: AssemblyProduct(BuildInfo.Name)]
[assembly: AssemblyCopyright("Created by " + BuildInfo.Author)]
[assembly: AssemblyTrademark(BuildInfo.Company)]
[assembly: AssemblyVersion(BuildInfo.Version)]
[assembly: AssemblyFileVersion(BuildInfo.GitVersion)]
[assembly: MelonLoader.MelonInfo(typeof(AquaMai.MelonLoader.AquaMai), BuildInfo.Name, BuildInfo.GitVersion, BuildInfo.Author, BuildInfo.DownloadLink)]
[assembly: MelonLoader.MelonColor()]
[assembly: MelonLoader.HarmonyDontPatchAll]

// Create and Setup a MelonGame Attribute to mark a Melon as Universal or Compatible with specific Games.
// If no MelonGame Attribute is found or any of the Values for any MelonGame Attribute on the Melon is null or empty it will be assumed the Melon is Universal.
// Values for MelonGame Attribute can be found in the Game's app.info file or printed at the top of every log directly beneath the Unity version.
[assembly: MelonLoader.MelonGame(null, null)]
