using System.Text;

class Program
{
    static async Task Main(string[] args)
    {
        string url = "https://www.gutenberg.org/cache/epub/1513/pg1513.txt";
        string bookText = await DownloadBookTextAsync(url);

        // image adding
        bookText += "\nimage:https://plus.unsplash.com/premium_photo-1664474619075-644dd191935f?fm=jpg&q=60&w=3000&ixlib=rb-4.0.3&ixid=M3wxMjA3fDB8MHxzZWFyY2h8MXx8aW1hZ2V8ZW58MHx8MHx8fDA%3D/150x150,page=2,after=5";

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
        List<string> lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).ToList();
        List<IHtmlElement> parsedElements = new();
        List<(int page, int after, IHtmlElement element)> insertInstructions = new();

      
        foreach (var line in lines)
        {
            if (line.StartsWith("image:") && line.Contains(",page="))
            {
                var parts = line.Substring(6).Split(',');
                string src = parts[0];
                int page = int.Parse(parts[1].Split('=')[1]);
                int after = int.Parse(parts[2].Split('=')[1]);
                insertInstructions.Add((page, after, new ImageElement(src)));
            }
            else
            {
                var strippedLine = line.Trim();
                if (!string.IsNullOrEmpty(strippedLine))
                {
                    var element = HtmlElementFactory.CreateHtmlElement(strippedLine, line);
                    if (element != null)
                    {
                        parsedElements.Add(element);
                    }
                }
            }
        }

      
        List<List<IHtmlElement>> pages = new();
        for (int i = 0; i < parsedElements.Count; i += elementsPerPage)
        {
            pages.Add(parsedElements.Skip(i).Take(elementsPerPage).ToList());
        }

       
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

        StringBuilder pagedHtml = new StringBuilder();
        pagedHtml.Append(pageStyles);

        var insertContext = new InsertContext();

        for (int pageIndex = 0; pageIndex < pages.Count; pageIndex++)
        {
            var pageDiv = new DivElement();

            foreach (var element in pages[pageIndex])
            {
                pageDiv.AddChild(element);
            }

            foreach (var (targetPage, afterIndex, elementToInsert) in insertInstructions)
            {
                if (targetPage == pageIndex + 1)
                {
                    insertContext.InsertElement(pageDiv, afterIndex, elementToInsert);
                }
            }

            pagedHtml.AppendLine($"<div class='page' id='page{pageIndex}' style='display:{(pageIndex == 0 ? "block" : "none")}'>");
            pagedHtml.AppendLine(pageDiv.Render());
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
            const totalPages = " + pages.Count + @";

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
    IEnumerator<IHtmlElement> GetEnumerator();
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

    protected abstract string RenderContent();

    protected virtual void OnCreated() => Console.WriteLine($"{GetType().Name}: OnCreated");
    protected virtual void OnInserted() => Console.WriteLine($"{GetType().Name}: OnInserted");
    protected virtual void OnRemoved() => Console.WriteLine($"{GetType().Name}: OnRemoved");
    protected virtual void OnStylesApplied() => Console.WriteLine($"{GetType().Name}: OnStylesApplied");
    protected virtual void OnClassListApplied() => ClassList += " contentText";
    protected virtual void OnTextRendered() => Console.WriteLine($"{GetType().Name}: OnTextRendered");

    protected void TriggerEvent(string eventType)
    {
        if (eventListeners.TryGetValue(eventType, out var handler))
            handler.Invoke();
    }

    public virtual IEnumerator<IHtmlElement> GetEnumerator()
    {
        yield break;
    }
}

class DivElement : HtmlElementBase
{
    public List<IHtmlElement> Children { get; } = new List<IHtmlElement>();

    public void AddChild(IHtmlElement child)
    {
        Children.Add(child);
    }

    public void InsertAfter(int index, IHtmlElement element)
    {
        if (index >= 0 && index < Children.Count)
            Children.Insert(index + 1, element);
        else
            Children.Add(element);
    }

    protected override string RenderContent()
    {
        StringBuilder content = new StringBuilder();
        foreach (var child in Children)
        {
            content.Append(child.Render());
        }
        return $"<div class='{ClassList}'>{content}</div>";
    }

    public override IEnumerator<IHtmlElement> GetEnumerator()
    {
        foreach (var child in Children)
            yield return child;
    }
}

class H1Element : HtmlElementBase
{
    private string _content;
    public H1Element(string content) => _content = content;
    protected override void OnStylesApplied() => Style = "font-size: 32px; color: darkblue; margin-top: 20px;";
    protected override string RenderContent() => $"<h1 style=\"{Style}\" class='{ClassList}'>{_content}</h1>";
}

class H2Element : HtmlElementBase
{
    private string _content;
    public H2Element(string content) => _content = content;
    protected override void OnStylesApplied() => Style = "font-size: 24px; color: darkgreen; margin-top: 16px;";
    protected override string RenderContent() => $"<h2 style=\"{Style}\" class='{ClassList}'>{_content}</h2>";
}

class BlockquoteElement : HtmlElementBase
{
    private string _content;
    public BlockquoteElement(string content) => _content = content;
    protected override void OnStylesApplied() => Style = "margin-left: 20px; font-style: italic; color: gray;";
    protected override string RenderContent() => $"<blockquote class='{ClassList}' style=\"{Style}\">{_content}</blockquote>";
}

class ImageElement : HtmlElementBase
{
    private string _src;
    public ImageElement(string src) => _src = src;
    protected override void OnStylesApplied() => Style = "max-width:50%; margin:10px 0;";
    protected override string RenderContent() => $"<img src=\"{_src}\" style=\"{Style}\" class=\"{ClassList}\" />";
}

class HtmlElementFactory
{
    public static IHtmlElement CreateHtmlElement(string strippedLine, string originalLine)
    {
        if (originalLine.StartsWith("button:"))
            return null;

        if (originalLine.StartsWith("image:"))
            return null;

        if (originalLine.StartsWith(" "))
            return new BlockquoteElement(originalLine);

        if (strippedLine.Length < 20)
            return new H2Element(originalLine);

        return new H1Element(originalLine);
    }
}



interface IInsertState
{
    void Insert(DivElement page, int afterIndex, IHtmlElement element);
}

class NormalInsertState : IInsertState
{
    public void Insert(DivElement page, int afterIndex, IHtmlElement element)
    {
        page.InsertAfter(afterIndex, element);
    }
}

class InsertContext
{
    private IInsertState _state = new NormalInsertState();
    public void SetState(IInsertState state) => _state = state;
    public void InsertElement(DivElement page, int afterIndex, IHtmlElement element)
    {
        _state.Insert(page, afterIndex, element);
    }
}
