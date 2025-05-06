using System.Text;


class Program
{
    static async Task Main(string[] args)
    {
        string url = "https://www.gutenberg.org/cache/epub/1513/pg1513.txt";
        string bookText = await DownloadBookTextAsync(url);

        bookText += "\nimage:https://plus.unsplash.com/premium_photo-1664474619075-644dd191935f?fm=jpg&q=60&w=3000";
        bookText += "\nimage:local-image.png";
        bookText += "\nbutton:Click me!";

        string htmlContent = ConvertToHtml(bookText);
        Console.WriteLine(htmlContent);

        long memorySize = GetMemorySize(htmlContent);
        Console.WriteLine($"Пам'ять, яку займає HTML верстка: {memorySize} байт");

        SaveToHtmlFile(htmlContent);
    }

    static async Task<string> DownloadBookTextAsync(string url)
    {
        using (HttpClient client = new HttpClient())
        {
            return await client.GetStringAsync(url);
        }
    }

    static string ConvertToHtml(string text)
    {
        StringBuilder htmlOutput = new StringBuilder();
        string[] lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

        foreach (var line in lines)
        {
            string strippedLine = line.Trim();
            if (string.IsNullOrEmpty(strippedLine))
                continue;

            IHtmlElement htmlElement = HtmlElementFactory.CreateHtmlElement(strippedLine, line);
            htmlOutput.AppendLine(htmlElement.Render());
        }

        return htmlOutput.ToString();
    }

    static long GetMemorySize(string content)
    {
        return Encoding.UTF8.GetByteCount(content);
    }

    static void SaveToHtmlFile(string html)
    {
        File.WriteAllText("output.html", $"<html><body>{html}</body></html>");
        Console.WriteLine("Збережено до output.html");
    }
}



interface IHtmlElement
{
    string Render();
    void AddEventListener(string eventType, Action handler);
}



abstract class HtmlElementBase : IHtmlElement
{
    protected Dictionary<string, Action> eventListeners = new Dictionary<string, Action>();

    public virtual void AddEventListener(string eventType, Action handler)
    {
        eventListeners[eventType] = handler;
    }

    public abstract string Render();

    protected void TriggerEvent(string eventType)
    {
        if (eventListeners.TryGetValue(eventType, out var handler))
        {
            handler.Invoke();
        }
    }
}



class H1Element : HtmlElementBase
{
    private string _content;
    public H1Element(string content) => _content = content;
    public override string Render() => $"<h1>{_content}</h1>";
}

class H2Element : HtmlElementBase
{
    private string _content;
    public H2Element(string content) => _content = content;
    public override string Render() => $"<h2>{_content}</h2>";
}

class BlockquoteElement : HtmlElementBase
{
    private string _content;
    public BlockquoteElement(string content) => _content = content;
    public override string Render() => $"<blockquote>{_content}</blockquote>";
}

class PElement : HtmlElementBase
{
    private string _content;
    public PElement(string content) => _content = content;
    public override string Render() => $"<p>{_content}</p>";
}

class ButtonElement : HtmlElementBase
{
    private string _label;
    public ButtonElement(string label) => _label = label;

    public override string Render()
    {
        return $"<button onclick=\"alert('Button clicked: {_label}')\">{_label}</button>";
    }
}



class ImageElement : HtmlElementBase
{
    private readonly string _href;
    private readonly IImageLoaderStrategy _strategy;

    public ImageElement(string href, IImageLoaderStrategy strategy)
    {
        _href = href;
        _strategy = strategy;
    }

    public override string Render()
    {
        return _strategy.LoadImage(_href);
    }
}

interface IImageLoaderStrategy
{
    string LoadImage(string href);
}

class NetworkImageLoader : IImageLoaderStrategy
{
    public string LoadImage(string href)
    {
        return $"<img src=\"{href}\" alt=\"Image from web\" />";
    }
}

class FileSystemImageLoader : IImageLoaderStrategy
{
    public string LoadImage(string href)
    {
        try
        {
            string base64 = Convert.ToBase64String(File.ReadAllBytes(href));
            string extension = Path.GetExtension(href).TrimStart('.').ToLower();
            return $"<img src=\"data:image/{extension};base64,{base64}\" alt=\"Image from file\" />";
        }
        catch
        {
            return $"<p>Image '{href}' not found or could not be loaded.</p>";
        }
    }
}



static class HtmlElementFactory
{
    public static IHtmlElement CreateHtmlElement(string strippedLine, string originalLine)
    {
        Console.OutputEncoding = Encoding.UTF8;

        IHtmlElement element;

        if (originalLine.StartsWith("image:"))
        {
            string href = originalLine.Substring("image:".Length).Trim();
            IImageLoaderStrategy strategy = href.StartsWith("http")
                ? new NetworkImageLoader()
                : new FileSystemImageLoader();
            element = new ImageElement(href, strategy);
        }
        else if (originalLine.StartsWith("button:"))
        {
            string label = originalLine.Substring("button:".Length).Trim();
            element = new ButtonElement(label);
            element.AddEventListener("click", () => Console.WriteLine($"Button '{label}' clicked (C# event triggered)"));
        }
        else if (originalLine.StartsWith(" "))
        {
            element = new BlockquoteElement(originalLine);
        }
        else if (strippedLine.Length < 20)
        {
            element = new H2Element(originalLine);
        }
        else
        {
            element = new H1Element(originalLine);
        }

        return element;
    }
}