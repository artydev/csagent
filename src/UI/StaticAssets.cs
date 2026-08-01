using System.Reflection;

namespace CsAgentUI;

public static class StaticAssets
{
    public static string HtmlUI => LoadEmbeddedResource("CsAgentUI.src.UI.assets.index.html");
    
    public static string ReadmeMd => LoadEmbeddedResource("CsAgentUI.src.UI.assets.readme.md");
    
    private static string LoadEmbeddedResource(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
            throw new FileNotFoundException($"Embedded resource not found: {resourceName}");
            
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}