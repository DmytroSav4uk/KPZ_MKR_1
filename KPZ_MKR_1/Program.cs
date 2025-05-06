using System.Text;


class Program
{
    static async Task Main(string[] args)
    {
        string url = "https://www.gutenberg.org/cache/epub/1513/pg1513.txt";
        string bookText = await DownloadBookTextAsync(url);

        bookText += "\nbutton:Click me!";

        string htmlContent = ConvertToPagedHtml(bookText, 70);
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

    static string ConvertToPagedHtml(string text, int elementsPerPage)
{
    List<string> renderedElements = new List<string>();
    string[] lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

    foreach (var line in lines)
    {
        string strippedLine = line.Trim();
        if (string.IsNullOrEmpty(strippedLine))
            continue;

        IHtmlElement htmlElement = HtmlElementFactory.CreateHtmlElement(strippedLine, line);
        if (htmlElement != null)
        {
            renderedElements.Add(htmlElement.Render());
        }
    }

    int totalPages = (int)Math.Ceiling(renderedElements.Count / (double)elementsPerPage);
    StringBuilder pagedHtml = new StringBuilder();

    // Додаємо стилі для елементів page
    string pageStyles = @"
    <style>
        .page {
            width: 75%;
            height: 80vh;
            page-break-before: always;
            overflow-y: auto;
        }
        .contentText {
            font-family: Arial, sans-serif;
            line-height: 1.5;
        }
    </style>";

    pagedHtml.Append(pageStyles);  

    for (int pageIndex = 0; pageIndex < totalPages; pageIndex++)
    {
        pagedHtml.AppendLine($"<div class='page' id='page{pageIndex}' style='display:{(pageIndex == 0 ? "block" : "none")}'>");
        int start = pageIndex * elementsPerPage;
        int end = Math.Min(start + elementsPerPage, renderedElements.Count);
        for (int i = start; i < end; i++)
        {
            pagedHtml.AppendLine(renderedElements[i]);
        }
        pagedHtml.AppendLine("</div>");
    }

    pagedHtml.AppendLine(@"
<div style='margin-top:20px'>
    <button onclick='prevPage()'>Previous Page</button>
    <span id='pageInfo'></span>
    <button onclick='nextPage()'>Next Page</button>
</div>

<script>
    let currentPage = 0;
    const totalPages = " + totalPages + @";

    function showPage(index) {
        for (let i = 0; i < totalPages; i++) {
            document.getElementById('page' + i).style.display = i === index ? 'block' : 'none';
        }
        document.getElementById('pageInfo').innerText = `Page: ${index + 1} / ${totalPages}`;
        currentPage = index;
    }

    function nextPage() {
        if (currentPage < totalPages - 1) {
            showPage(currentPage + 1);
        }
    }

    function prevPage() {
        if (currentPage > 0) {
            showPage(currentPage - 1);
        }
    }

    showPage(currentPage);
</script>
");

    return pagedHtml.ToString();
}


    static long GetMemorySize(string content)
    {
        return Encoding.UTF8.GetByteCount(content);
    }

    static void SaveToHtmlFile(string html)
    {
        File.WriteAllText("output.html", $"<html><head><meta charset='UTF-8'></head><body style='display:flex;flex-direction:column;align-items:center;'>{html}</body></html>");
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
    protected Dictionary<string, Action> eventListeners = new();
    protected string Style = "";
    protected string ClassList = "";
    public virtual void AddEventListener(string eventType, Action handler)
    {
        eventListeners[eventType] = handler;
    }

    // 🔷 Template Method
    public string Render()
    {
        OnCreated();
        OnStylesApplied();
        OnClassListApplied();
        string html = RenderContent(); 
        OnTextRendered();
        OnInserted();

        return html;
    }

    // 🔹 Абстрактна частина шаблону — реалізується підкласами
    protected abstract string RenderContent();

    // 🔹 Хуки життєвого циклу (можна перевизначити в підкласах)
    protected virtual void OnCreated() => Console.WriteLine($"{GetType().Name}: OnCreated");
    protected virtual void OnInserted() => Console.WriteLine($"{GetType().Name}: OnInserted");
    protected virtual void OnRemoved() => Console.WriteLine($"{GetType().Name}: OnRemoved");
    protected virtual void OnStylesApplied() => Console.WriteLine($"{GetType().Name}: OnStylesApplied");
    protected virtual void OnClassListApplied() 
    {
        ClassList += " contentText"; 
    }

    protected virtual void OnTextRendered() => Console.WriteLine($"{GetType().Name}: OnTextRendered");

    protected void TriggerEvent(string eventType)
    {
        if (eventListeners.TryGetValue(eventType, out var handler))
            handler.Invoke();
    }
}


class H1Element : HtmlElementBase
{
    private string _content;

    public H1Element(string content) => _content = content;

    
    protected override void OnStylesApplied()
    {
        Style = "font-size: 32px; color: darkblue; margin-top: 20px;";
    }
    
    protected override string RenderContent()
    {
        return $"<h1 style=\"{Style}\" class='{ClassList}'>{_content}</h1>";
    }
}

class H2Element : HtmlElementBase
{
    private string _content;

    public H2Element(string content) => _content = content;

    protected override void OnStylesApplied()
    {
        Style = "font-size: 24px; color: darkgreen; margin-top: 16px;";
    }
    
    protected override string RenderContent()
    {
        return $"<h2 style=\"{Style}\" class='{ClassList}'>{_content}</h2>"; 
    }
}

class BlockquoteElement : HtmlElementBase
{
    private string _content;

    public BlockquoteElement(string content) => _content = content;

    protected override void OnStylesApplied()
    {
        Style = "margin-left: 20px; font-style: italic; color: gray;";
    }
    
    protected override string RenderContent()
    {
        return $"<blockquote class='{ClassList}' style=\"{Style}\">{_content}</blockquote>";  
    }
}

class PElement : HtmlElementBase
{
    private string _content;

    public PElement(string content) => _content = content;

    protected override void OnStylesApplied()
    {
        Style = "font-size: 16px; line-height: 1.5; margin: 10px 0;";
    }
    
    protected override string RenderContent()
    {
        return $"<p class='{ClassList}'>{_content}</p>";  
    }
}

class ButtonElement : HtmlElementBase
{
    private string _label;

    public ButtonElement(string label) => _label = label;

    protected override void OnStylesApplied()
    {
        Style = "padding: 10px 20px; background-color: #007BFF; color: white; border: none; border-radius: 5px;";
    }
    
    protected override string RenderContent()
    {
        return $"<button class='{ClassList}' style=\"{Style}\" onclick=\"alert('Button clicked: {_label}')\">{_label}</button>";  
    }
}




static class HtmlElementFactory
{
    public static IHtmlElement CreateHtmlElement(string strippedLine, string originalLine)
    {
        Console.OutputEncoding = Encoding.UTF8;

        if (originalLine.StartsWith("image:"))
        {
            return null; // Ігноруємо зображення
        }

        IHtmlElement element;

        if (originalLine.StartsWith("button:"))
        {
            return null;
            // string label = originalLine.Substring("button:".Length).Trim();
            // element = new ButtonElement(label);
            // element.AddEventListener("click", () => Console.WriteLine($"Button '{label}' clicked (C# event triggered)"));
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
